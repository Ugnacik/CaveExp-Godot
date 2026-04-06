using Godot;
using System;
using System.Collections.Generic;

public static class RoomSelector
{
    public static RoomData GetRandomRoom(List<RoomData> rooms, Random rng)
    {
        if (rooms.Count == 0)
        {
            GD.PrintErr("No rooms loaded!");
            return null;
        }

        return rooms[rng.Next(rooms.Count)];
    }
}
