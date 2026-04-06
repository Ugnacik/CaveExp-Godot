using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
public static class RoomLoader
{
    public static List<RoomData> Load(string path)
    {
        string json = FileAccess.GetFileAsString(path);
        var rooms = JsonSerializer.Deserialize<List<RoomData>>(json);

        if (rooms == null)
        {
            GD.PrintErr("Failed to load rooms!");
            return new List<RoomData>();
        }

        foreach (var room in rooms)
        {
            ValidateRoom(room);
        }

        return rooms;
    }

    private static void ValidateRoom(RoomData room)
    {
        if (room.layout.Length != Constants.ROOM_HEIGHT)
        {
            GD.PrintErr($"Room {room.name} has wrong height!");
        }

        for (int y = 0; y < room.layout.Length; y++)
        {
            if (room.layout[y].Length != Constants.ROOM_WIDTH)
            {
                GD.PrintErr($"Room {room.name} row {y} has wrong width!");
            }
        }
    }
}