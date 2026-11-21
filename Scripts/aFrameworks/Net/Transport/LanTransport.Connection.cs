#nullable enable
using System;
using System.Buffers.Binary;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

public partial class LanTransport
{
    // TCP framing is independent of the inner NetManager messages. A zero-length
    // frame is a heartbeat. Peer identity comes from the connection itself.
    private static byte[] Frame(ReadOnlySpan<byte> data, int? lengthOverride = null)
    {
        var frame = new byte[sizeof(int) + data.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, lengthOverride ?? data.Length);
        data.CopyTo(frame.AsSpan(sizeof(int)));
        return frame;
    }

    private sealed class Connection
    {
        private readonly TcpClient tcp;
        private readonly CancellationTokenSource lifetime = new();
        private readonly Channel<byte[]> incoming = NewQueue();
        private readonly Channel<byte[]> outgoing = NewQueue();
        private int incomingBytes;
        private int outgoingBytes;
        private int closed;
        private int kicked;
        private long lastSend = Environment.TickCount64;

        public Connection(TcpClient tcp)
        {
            this.tcp = tcp;
            tcp.NoDelay = true;
            RemoteAddress = (tcp.Client.RemoteEndPoint as IPEndPoint)?.Address;
        }

        public bool IsClosed => Volatile.Read(ref closed) != 0;
        public bool WasKicked => Volatile.Read(ref kicked) != 0;
        public IPAddress? RemoteAddress { get; }

        public async Task<ulong> ConnectAndReadId(string address, int port)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            timeout.CancelAfter(JoinTimeoutMs);
            try
            {
                await tcp.ConnectAsync(address, port, timeout.Token).ConfigureAwait(false);
                var stream = tcp.GetStream();
                var hello = new byte[8];
                BinaryPrimitives.WriteUInt64LittleEndian(hello, HandshakeVersion);
                await stream.WriteAsync(hello, timeout.Token).ConfigureAwait(false);
                var handshake = new byte[16];
                await stream.ReadExactlyAsync(handshake, timeout.Token).ConfigureAwait(false);
                if (BinaryPrimitives.ReadUInt64LittleEndian(handshake) == HandshakeVersion)
                {
                    ulong peerId = BinaryPrimitives.ReadUInt64LittleEndian(handshake.AsSpan(8));
                    if (peerId > LanHostId)
                        return peerId;
                }
            }
            catch (Exception e) when (IsConnectionError(e) || e is ArgumentException) { }

            Close();
            return 0;
        }

        public async Task<bool> ReadHello()
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            timeout.CancelAfter(JoinTimeoutMs);
            try
            {
                var hello = new byte[8];
                await tcp.GetStream().ReadExactlyAsync(hello, timeout.Token).ConfigureAwait(false);
                return BinaryPrimitives.ReadUInt64LittleEndian(hello) == HandshakeVersion;
            }
            catch (Exception e) when (IsConnectionError(e))
            {
                return false;
            }
        }

        public void Start(bool acceptKick = false)
        {
            NetworkStream stream = tcp.GetStream();
            _ = Task.Run(() => ReadLoop(stream, acceptKick));
            _ = Task.Run(() => WriteLoop(stream));
        }

        public void SendAndClose(byte[] finalFrame)
        {
            Send(finalFrame);
            lifetime.CancelAfter(2000);
            outgoing.Writer.TryComplete();
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref closed, 1) != 0)
                return;
            lifetime.Cancel();
            outgoing.Writer.TryComplete();
            tcp.Dispose();
        }

        public void Send(byte[] frame)
        {
            if (IsClosed)
                return;
            if (!TryEnqueue(outgoing.Writer, frame, ref outgoingBytes))
            {
                Close();
                return;
            }
            lastSend = Environment.TickCount64;
        }

        public void SendHeartbeatIfIdle(long now)
        {
            if (now - lastSend >= HeartbeatIntervalMs)
                Send(HeartbeatFrame);
        }

        public bool TryReceive([NotNullWhen(true)] out byte[]? data)
        {
            if (!incoming.Reader.TryRead(out data))
                return false;
            Interlocked.Add(ref incomingBytes, -data.Length);
            return true;
        }

        private async Task ReadLoop(NetworkStream stream, bool acceptKick)
        {
            var header = new byte[sizeof(int)];
            try
            {
                while (!IsClosed)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    timeout.CancelAfter(IoTimeoutMs);
                    await stream.ReadExactlyAsync(header, timeout.Token).ConfigureAwait(false);
                    int length = BinaryPrimitives.ReadInt32LittleEndian(header);
                    if (length == 0)
                        continue;
                    if (acceptKick && length == KickFrameLength)
                        Volatile.Write(ref kicked, 1);
                    if (length < 0 || length > NetWireLimits.MaxPacketBytes)
                        break;

                    var data = GC.AllocateUninitializedArray<byte>(length);
                    await stream.ReadExactlyAsync(data, timeout.Token).ConfigureAwait(false);
                    if (!TryEnqueue(incoming.Writer, data, ref incomingBytes))
                        break;
                }
            }
            catch (Exception e) when (IsConnectionError(e)) { }
            finally
            {
                Close();
            }
        }

        private async Task WriteLoop(NetworkStream stream)
        {
            try
            {
                await foreach (var frame in outgoing.Reader.ReadAllAsync(lifetime.Token).ConfigureAwait(false))
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    timeout.CancelAfter(IoTimeoutMs);
                    await stream.WriteAsync(frame, timeout.Token).ConfigureAwait(false);
                    Interlocked.Add(ref outgoingBytes, -frame.Length);
                }
            }
            catch (Exception e) when (IsConnectionError(e)) { }
            finally
            {
                Close();
            }
        }

        private static Channel<byte[]> NewQueue() =>
            Channel.CreateBounded<byte[]>(new BoundedChannelOptions(NetWireLimits.MaxQueuedPackets)
            {
                SingleReader = true,
            });

        private static bool TryEnqueue(ChannelWriter<byte[]> writer, byte[] data, ref int queuedBytes)
        {
            if (Interlocked.Add(ref queuedBytes, data.Length) <= NetWireLimits.MaxQueuedBytes
                && writer.TryWrite(data))
                return true;
            Interlocked.Add(ref queuedBytes, -data.Length);
            return false;
        }

        private static bool IsConnectionError(Exception e) =>
            e is IOException or SocketException or ObjectDisposedException or OperationCanceledException;
    }
}
