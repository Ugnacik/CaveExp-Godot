using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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

    public static RoomData GetCompatibleRoom(
    List<RoomData> rooms,
    Direction nextOpening,
    Random rng)
    {
        if (rooms == null || rooms.Count == 0)
        {
            GD.PrintErr("No rooms loaded!");
            return null;
        }

        var compatibleRooms = rooms
            .Where(room =>
                room.connections.Contains(nextOpening) &&
                room.connections.Any(c => c != nextOpening)
            )
            .ToList();

        if (compatibleRooms.Count == 0)
        {
            GD.PrintErr($"No rooms found with opening {nextOpening}");
            return null;
        }

        return compatibleRooms[rng.Next(compatibleRooms.Count)];
    }
}