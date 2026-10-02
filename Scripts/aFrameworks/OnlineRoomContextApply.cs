using Godot;

/// <summary>
/// 同一份 <see cref="GameOnlineContext"/> 写到 Steam 或 LAN。房间编排只走这里。
/// </summary>
public static class OnlineRoomContextApply
{
    public static GameOnlineContext Normalize(GameOnlineContext context, ENetLobbyDisplayType displayType)
    {
        context ??= new GameOnlineContext();
        context.MaxPlayers = Mathf.Max(context.MaxPlayers, 1);
        context.RoomName = OnlineRoomListing.ResolveRoomName(context.RoomName, displayType);
        return context;
    }

    public static void ApplyAfterRoomCreated(GameOnlineContext context, ENetLobbyDisplayType displayType)
    {
        context = Normalize(context, displayType);
        if (displayType == ENetLobbyDisplayType.Lan)
            Main.Instance?.lanDiscoveryService?.StartHosting(context.RoomName);
        else
            SteamManager.Instance?.ApplyCurrentLobbyMetadata(context);
    }
}
