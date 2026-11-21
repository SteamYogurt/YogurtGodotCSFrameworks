using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;

public class LanRoomBroadcastData
{
    public string GameId { get; set; } = "";
    public int ProtocolVersion { get; set; }
    public string RoomName { get; set; } = "";
    public string HostAddress { get; set; } = "";
    public int Port { get; set; } = LanTransport.DefaultPort;

    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; }
    public bool Joinable { get; set; }
    public bool MidJoinable { get; set; } = true;
    public bool InMatch { get; set; }
    public string MatchId { get; set; } = "";
}

public class LanDiscoveredRoomInfo : LanRoomBroadcastData
{
    public long LastSeenUnixMs { get; set; }
}

public partial class LanDiscoveryService : Node
{
    public const int DiscoveryPort = 7778;
    private const int BroadcastIntervalMs = 1000;
    private const int RoomTimeoutMs = 3500;
    private const int CleanupIntervalMs = 1000;
    private const int GamePort = LanTransport.DefaultPort;
    private const int MaxDiscoveryBytes = 4096;
    private const int MaxDiscoveredRooms = 256;
    private static readonly IPEndPoint BroadcastEndpoint = new(IPAddress.Broadcast, DiscoveryPort);

    public event Action RoomsChanged;

    private readonly object roomsLock = new();
    private readonly Dictionary<string, LanDiscoveredRoomInfo> rooms = new();

    private UdpClient browseClient;

    private UdpClient hostClient;
    private long nextBroadcastAt;

    private int roomsDirty;
    private long nextCleanupAtUnixMs;

    private string hostRoomName = "LAN Room";

    public bool IsHosting => hostClient != null;
    public string CurrentHostRoomName => hostRoomName;

    public override void _ExitTree()
    {
        StopAll();
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        if (IsHosting && System.Environment.TickCount64 >= nextBroadcastAt)
        {
            nextBroadcastAt = System.Environment.TickCount64 + BroadcastIntervalMs;
            BroadcastRoom();
        }
        bool changed = CleanupExpiredRooms();
        if (Interlocked.Exchange(ref roomsDirty, 0) != 0 || changed)
            RoomsChanged?.Invoke();
    }

    public void StartBrowsing()
    {
        if (browseClient != null) return;

        ClearRooms();

        var client = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            client.Client.ExclusiveAddressUse = false;
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
        }
        catch (SocketException e)
        {
            client.Dispose();
            GD.PrintErr($"[LanDiscovery] 无法监听发现端口: {e.Message}");
            return;
        }

        lock (roomsLock)
            browseClient = client;
        new Thread(() => BrowseLoop(client)) { IsBackground = true }.Start();

        GD.Print("[LanDiscovery] Start browsing.");
    }

    public void StopBrowsing()
    {
        UdpClient previous;
        lock (roomsLock)
        {
            previous = browseClient;
            browseClient = null;
        }
        previous?.Dispose();

        ClearRooms();
        GD.Print("[LanDiscovery] Stop browsing.");
    }

    public void StartHosting(string roomName)
    {
        hostRoomName = OnlineRoomListing.ResolveRoomName(roomName, ENetLobbyDisplayType.Lan);

        if (IsHosting) return;

        hostClient = new UdpClient();
        hostClient.EnableBroadcast = true;

        hostClient.Client.Blocking = false;
        nextBroadcastAt = 0;

        GD.Print("[LanDiscovery] Start hosting broadcast.");
    }

    public void UpdateHostRoomName(string roomName)
    {
        hostRoomName = OnlineRoomListing.ResolveRoomName(roomName, ENetLobbyDisplayType.Lan);
    }

    public void StopHosting()
    {
        hostClient?.Dispose();
        hostClient = null;

        GD.Print("[LanDiscovery] Stop hosting broadcast.");
    }

    public void StopAll()
    {
        StopHosting();
        StopBrowsing();
    }

    public List<LanDiscoveredRoomInfo> GetRooms()
    {
        lock (roomsLock)
        {
            return rooms.Values
                .OrderBy(r => r.RoomName)
                .ThenBy(r => r.HostAddress)
                .ToList();
        }
    }

    public void ClearRooms()
    {
        lock (roomsLock)
        {
            rooms.Clear();
        }
        Interlocked.Exchange(ref roomsDirty, 1);
    }

    private void BroadcastRoom()
    {
        if (!TryBuildBroadcastData(out var data))
            return;
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(data);
        if (bytes.Length > MaxDiscoveryBytes)
            return;
        try
        {
            hostClient.Send(bytes, bytes.Length, BroadcastEndpoint);
        }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.WouldBlock) { }
        catch (SocketException e)
        {
            GD.PrintErr($"[LanDiscovery] 房间广播失败: {e.Message}");
        }
    }

    private void BrowseLoop(UdpClient client)
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (true)
        {
            try
            {
                byte[] bytes = client.Receive(ref remote);
                if (!TryParseBroadcast(bytes, remote, out var room))
                    continue;

                lock (roomsLock)
                {
                    if (!ReferenceEquals(browseClient, client))
                        return;
                    string key = GetRoomKey(room.HostAddress, room.Port);
                    if (rooms.Count >= MaxDiscoveredRooms && !rooms.ContainsKey(key))
                        continue;
                    rooms[key] = room;
                }

                Interlocked.Exchange(ref roomsDirty, 1);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException e)
            {
                lock (roomsLock)
                {
                    if (!ReferenceEquals(browseClient, client))
                        return;
                    browseClient = null;
                }
                client.Dispose();
                GD.PrintErr($"[LanDiscovery] 发现端口已关闭: {e.Message}");
                return;
            }
        }
    }

    private bool TryBuildBroadcastData(out LanRoomBroadcastData data)
    {
        data = null;

        var transport = TransportManager.Instance?.Current;
        if (transport == null || !transport.InRoom || !transport.AmIHost())
            return false;

        var presence = NetRoomPresenceSource.Read?.Invoke();
        int playerCount = presence?.PlayerCount ?? transport.GetTempNetPlayerInfos()?.Count ?? 1;

        data = new LanRoomBroadcastData
        {
            GameId = OnlineRoomListing.GameId,
            ProtocolVersion = NetWireLimits.ProtocolVersion,
            RoomName = hostRoomName,
            HostAddress = "",
            Port = GamePort,
            PlayerCount = playerCount,
            MaxPlayers = presence?.MaxPlayers ?? 0,
            Joinable = presence?.AllowsJoining ?? true,
            MidJoinable = presence?.MidJoinable ?? true,
            InMatch = presence?.InMatch ?? false,
            MatchId = presence?.MatchId ?? "",
        };

        return true;
    }

    private bool TryParseBroadcast(byte[] bytes, IPEndPoint remote, out LanDiscoveredRoomInfo room)
    {
        room = null;
        if (bytes.Length > MaxDiscoveryBytes)
            return false;

        try
        {
            var data = JsonSerializer.Deserialize<LanRoomBroadcastData>(bytes);
            if (data == null)
                return false;

            if (!OnlineRoomListing.HasUsableRoomName(data.RoomName))
                return false;

            if (!OnlineRoomListing.IsOurGame(data.GameId) || data.ProtocolVersion != NetWireLimits.ProtocolVersion
                || data.Port != GamePort)
                return false;

            room = new LanDiscoveredRoomInfo
            {
                GameId = data.GameId ?? "",
                ProtocolVersion = data.ProtocolVersion,
                RoomName = data.RoomName.Trim(),
                HostAddress = remote.Address.ToString(),
                Port = data.Port <= 0 ? GamePort : data.Port,
                PlayerCount = data.PlayerCount,
                MaxPlayers = data.MaxPlayers,
                Joinable = data.Joinable,
                MidJoinable = data.MidJoinable,
                InMatch = data.InMatch,
                MatchId = data.MatchId ?? "",
                LastSeenUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool CleanupExpiredRooms()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (now < nextCleanupAtUnixMs)
            return false;

        nextCleanupAtUnixMs = now + CleanupIntervalMs;

        bool changed = false;

        lock (roomsLock)
        {
            var expiredKeys = rooms
                .Where(kv => now - kv.Value.LastSeenUnixMs > RoomTimeoutMs)
                .Select(kv => kv.Key)
                .ToList();

            for (int i = 0; i < expiredKeys.Count; i++)
            {
                rooms.Remove(expiredKeys[i]);
                changed = true;
            }
        }

        return changed;
    }

    private string GetRoomKey(string hostAddress, int port)
    {
        return hostAddress + ":" + port;
    }
}
