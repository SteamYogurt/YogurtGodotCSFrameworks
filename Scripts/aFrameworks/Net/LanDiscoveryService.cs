using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

public class LanRoomBroadcastData
{
    public string RoomName { get; set; } = "";
    public string HostAddress { get; set; } = "";
    public int Port { get; set; } = 7777;

    public int PlayerCount { get; set; }
    public bool Joinable { get; set; }
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
    private const int GamePort = 7777;

    public event Action RoomsChanged;

    private readonly object roomsLock = new();
    private readonly Dictionary<string, LanDiscoveredRoomInfo> rooms = new();

    private UdpClient browseClient;
    private Thread browseThread;
    private volatile bool browsing;

    private UdpClient hostClient;
    private Thread hostThread;
    private volatile bool hosting;

    private volatile bool roomsDirty;
    private long nextCleanupAtUnixMs;

    private string hostRoomName = "LAN Room";

    public override void _ExitTree()
    {
        StopAll();
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        bool changed = CleanupExpiredRooms();
        if (roomsDirty || changed)
        {
            roomsDirty = false;
            RoomsChanged?.Invoke();
        }
    }

    public void StartBrowsing()
    {
        if (browsing) return;

        ClearRooms();

        browseClient = new UdpClient(AddressFamily.InterNetwork);
        browseClient.Client.ExclusiveAddressUse = false;
        browseClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        browseClient.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

        browsing = true;
        browseThread = new Thread(BrowseLoop)
        {
            IsBackground = true
        };
        browseThread.Start();

        GD.Print("[LanDiscovery] Start browsing.");
    }

    public void StopBrowsing()
    {
        browsing = false;

        try
        {
            browseClient?.Close();
        }
        catch
        {
        }

        if (browseThread != null && browseThread.IsAlive)
            browseThread.Join(100);

        browseThread = null;
        browseClient = null;

        ClearRooms();
        GD.Print("[LanDiscovery] Stop browsing.");
    }

    public void StartHosting(string roomName)
    {
        hostRoomName = string.IsNullOrWhiteSpace(roomName) ? "LAN Room" : roomName.Trim();

        if (hosting) return;

        hostClient = new UdpClient();
        hostClient.EnableBroadcast = true;

        hosting = true;
        hostThread = new Thread(HostLoop)
        {
            IsBackground = true
        };
        hostThread.Start();

        GD.Print("[LanDiscovery] Start hosting broadcast.");
    }

    public void UpdateHostRoomName(string roomName)
    {
        hostRoomName = string.IsNullOrWhiteSpace(roomName) ? "LAN Room" : roomName.Trim();
    }

    public void StopHosting()
    {
        hosting = false;

        try
        {
            hostClient?.Close();
        }
        catch
        {
        }

        if (hostThread != null && hostThread.IsAlive)
            hostThread.Join(100);

        hostThread = null;
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
        roomsDirty = true;
    }

    private void HostLoop()
    {
        while (hosting)
        {
            try
            {
                if (TryBuildBroadcastData(out var data))
                {
                    string json = JsonSerializer.Serialize(data);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    hostClient.Send(
                        bytes,
                        bytes.Length,
                        new IPEndPoint(IPAddress.Broadcast, DiscoveryPort)
                    );
                }
            }
            catch (Exception e)
            {
                if (hosting)
                    GD.PrintErr("[LanDiscovery] HostLoop Error: " + e.Message);
            }

            Thread.Sleep(BroadcastIntervalMs);
        }
    }

    private void BrowseLoop()
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (browsing)
        {
            try
            {
                byte[] bytes = browseClient.Receive(ref remote);
                if (!TryParseBroadcast(bytes, remote, out var room))
                    continue;

                lock (roomsLock)
                {
                    string key = GetRoomKey(room.HostAddress, room.Port);
                    rooms[key] = room;
                }

                roomsDirty = true;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                if (!browsing) break;
            }
            catch (Exception e)
            {
                if (browsing)
                    GD.PrintErr("[LanDiscovery] BrowseLoop Error: " + e.Message);
            }
        }
    }

    private bool TryBuildBroadcastData(out LanRoomBroadcastData data)
    {
        data = null;

        var transport = TransportManager.Instance?.Current;
        if (transport == null || !transport.InRoom || !transport.AmIHost())
            return false;

        var game = Game.instance;

        int playerCount = transport.GetTempNetPlayerInfos()?.Count ?? 1;
        bool joinable = true;

        if (game != null)
        {
            playerCount = game.Players?.Count ?? playerCount;
            joinable = game.IsInBasement;
        }

        data = new LanRoomBroadcastData
        {
            RoomName = hostRoomName,
            HostAddress = "",
            Port = GamePort,
            PlayerCount = playerCount,
            Joinable = joinable
        };

        return true;
    }

    private bool TryParseBroadcast(byte[] bytes, IPEndPoint remote, out LanDiscoveredRoomInfo room)
    {
        room = null;

        try
        {
            string json = Encoding.UTF8.GetString(bytes);
            var data = JsonSerializer.Deserialize<LanRoomBroadcastData>(json);
            if (data == null)
                return false;

            room = new LanDiscoveredRoomInfo
            {
                RoomName = data.RoomName ?? "",
                HostAddress = remote.Address.ToString(),
                Port = data.Port <= 0 ? GamePort : data.Port,
                PlayerCount = data.PlayerCount,
                Joinable = data.Joinable,
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