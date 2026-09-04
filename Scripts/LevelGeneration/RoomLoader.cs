using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
public static class RoomLoader
{
    public static List<RoomData> Load(string path)
    {
        string json = FileAccess.GetFileAsString(path);
        List<RoomData> rooms;

        try
        {
            rooms = JsonSerializer.Deserialize<List<RoomData>>(json);
        }
        catch (JsonException exception)
        {
            GD.PrintErr($"Failed to parse room file '{path}': {exception.Message}");
            return new List<RoomData>();
        }

        if (rooms == null)
        {
            GD.PrintErr("Failed to load rooms!");
            return new List<RoomData>();
        }

        var validRooms = new List<RoomData>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var room in rooms)
        {
            if (!ValidateRoom(room, out string failure))
            {
                GD.PrintErr($"Rejected room template '{room?.name ?? "<unnamed>"}': {failure}");
                continue;
            }

            if (!names.Add(room.name))
            {
                GD.PrintErr($"Rejected duplicate room template name '{room.name}'.");
                continue;
            }

            validRooms.Add(room);
        }

        return validRooms;
    }

    private static bool ValidateRoom(RoomData room, out string failure)
    {
        if (room == null)
        {
            failure = "template is null";
            return false;
        }

        if (string.IsNullOrWhiteSpace(room.name))
        {
            failure = "name is missing";
            return false;
        }

        if (room.layout == null || room.layout.Length != Constants.ROOM_HEIGHT)
        {
            failure = $"height must be {Constants.ROOM_HEIGHT}";
            return false;
        }

        for (int y = 0; y < room.layout.Length; y++)
        {
            if (room.layout[y] == null || room.layout[y].Length != Constants.ROOM_WIDTH)
            {
                failure = $"row {y} width must be {Constants.ROOM_WIDTH}";
                return false;
            }

            if (room.layout[y].Any(tile => tile < 0 || tile > 4))
            {
                failure = $"row {y} contains an unknown tile ID";
                return false;
            }
        }

        if (room.connectionsRaw == null)
        {
            failure = "connections array is missing";
            return false;
        }

        var parsedConnections = new HashSet<Direction>();
        foreach (string rawConnection in room.connectionsRaw)
        {
            if (!Enum.TryParse(rawConnection, true, out Direction direction))
            {
                failure = $"unknown connection '{rawConnection}'";
                return false;
            }

            if (!parsedConnections.Add(direction))
            {
                failure = $"duplicate connection '{rawConnection}'";
                return false;
            }
        }

        if (room.markerPos != null)
        {
            if (room.markerPos.Length < 2
                || room.markerPos[0] < 1 || room.markerPos[0] >= Constants.ROOM_WIDTH - 1
                || room.markerPos[1] < 1 || room.markerPos[1] >= Constants.ROOM_HEIGHT - 1)
            {
                failure = "markerPos must identify an interior tile";
                return false;
            }
        }

        failure = string.Empty;
        return true;
    }
}
