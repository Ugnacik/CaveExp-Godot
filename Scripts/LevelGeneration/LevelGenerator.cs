using Godot;
using System;
using Godot.Collections;

public partial class LevelGenerator : Node2D
{
    [Export] public Array<PackedScene> EntranceRooms = new();
    [Export] public Array<PackedScene> RoomPool = new();
    [Export] public Vector2I GridSize = new Vector2I(4, 4);
    [Export] public Vector2 RoomSize = new Vector2(12, 8);

    private Room[,] _placedRooms;

    private Random _random = new Random();

    public override void _Ready()
    {
        GenerateLevel();
    }

    private void GenerateLevel()
    {
        _placedRooms = new Room[GridSize.X, GridSize.Y];

        for (int y = 0; y < GridSize.Y; y++)
        {
            for (int x = 0; x < GridSize.X; x++)
            {
                SpawnRoom(new Vector2I(x, y));
            }
        }
    }

    private void SpawnRoom(Vector2I pos)
    {
        var validRooms = GetValidRooms(pos);

        if (validRooms.Count == 0)
        {
            GD.Print($"No valid rooms at {pos} — fallback used");
            validRooms = RoomPool; // fallback so generation doesn’t break
        }

        var scene = validRooms[_random.Next(validRooms.Count)];
        var room = scene.Instantiate<Room>();

        room.Position = new Vector2(
            pos.X * RoomSize.X * 16,
            pos.Y * RoomSize.Y * 16
        );

        room.Name = $"Room_{pos.X}_{pos.Y}";

        AddChild(room);
        _placedRooms[pos.X, pos.Y] = room;
    }
    private Godot.Collections.Array<PackedScene> GetValidRooms(Vector2I pos)
    {
        var valid = new Godot.Collections.Array<PackedScene>();

        foreach (var scene in RoomPool)
        {
            var temp = scene.Instantiate<Room>();

            bool fits = true;

            // 🔹 Check LEFT neighbor
            if (pos.X > 0 && _placedRooms[pos.X - 1, pos.Y] != null)
            {
                var left = _placedRooms[pos.X - 1, pos.Y];

                if (left.OpeningRight != temp.OpeningLeft)
                    fits = false;
            }

            // 🔹 Check TOP neighbor
            if (pos.Y > 0 && _placedRooms[pos.X, pos.Y - 1] != null)
            {
                var top = _placedRooms[pos.X, pos.Y - 1];

                if (top.OpeningBottom != temp.OpeningTop)
                    fits = false;
            }

            if (fits)
                valid.Add(scene);

            temp.QueueFree(); // cleanup
        }

        return valid;
    }
}