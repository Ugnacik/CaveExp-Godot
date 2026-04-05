using Godot;
using System;
using System.Collections.Generic;
using Godot.Collections;


public partial class LevelGenerator : Node2D
{
    [Export] public Array<PackedScene> EntranceRooms = new();
    [Export] public Array<PackedScene> RoomPool = new();
    [Export] public Vector2I GridSize = new Vector2I(4, 4);
    [Export] public Vector2 RoomSize = new Vector2(12, 8);

    private Room[,] _placedRooms;
    private List<Vector2I> _mainPath;

    private Random _random = new Random();

    public override void _Ready()
    {
        GenerateLevel();
    }

    private void GenerateLevel()
    {
        _placedRooms = new Room[GridSize.X, GridSize.Y];
        _mainPath = GeneratePath();

        for (int x = 0; x < GridSize.X; x++)
        {
            for (int y = 0; y < GridSize.Y; y++)
            {
                var pos = new Vector2I(x, y);

                if (_mainPath.Contains(pos))
                    SpawnPathRoom(pos);
                else
                    SpawnRoom(pos);
            }
        }
    }

    private List<Vector2I> GeneratePath()
    {
        var path = new List<Vector2I>();
        var current = new Vector2I(0, 0);

        path.Add(current);

        while (current.Y < GridSize.Y - 1)
        {
            // randomly go right or down
            if (_random.Next(2) == 0 && current.X < GridSize.X - 1)
                current.X += 1;
            else
                current.Y += 1;

            path.Add(current);
        }

        return path;
    }
    private void SpawnPathRoom(Vector2I pos)
    {
        var validRooms = GetValidRooms(pos);

        // 🔥 EXTRA FILTER: must connect to path neighbors
        validRooms = FilterPathRooms(validRooms, pos);

        if (validRooms.Count == 0)
            validRooms = RoomPool;

        var scene = validRooms[_random.Next(validRooms.Count)];
        var room = scene.Instantiate<Room>();

        room.Position = new Vector2(
            pos.X * RoomSize.X * 16,
            pos.Y * RoomSize.Y * 16
        );

        AddChild(room);
        _placedRooms[pos.X, pos.Y] = room;
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
    private Godot.Collections.Array<PackedScene> FilterPathRooms(
    Godot.Collections.Array<PackedScene> rooms,
    Vector2I pos)
    {
        var result = new Godot.Collections.Array<PackedScene>();

        foreach (var scene in rooms)
        {
            var temp = scene.Instantiate<Room>();

            bool connectsToPath = false;

            // check neighbors in path
            var directions = new Vector2I[]
            {
            new Vector2I(-1, 0),
            new Vector2I(1, 0),
            new Vector2I(0, -1),
            new Vector2I(0, 1)
            };

            foreach (var dir in directions)
            {
                var neighbor = pos + dir;

                if (_mainPath.Contains(neighbor))
                {
                    // check if openings match direction
                    if (dir == new Vector2I(-1, 0) && temp.OpeningLeft) connectsToPath = true;
                    if (dir == new Vector2I(1, 0) && temp.OpeningRight) connectsToPath = true;
                    if (dir == new Vector2I(0, -1) && temp.OpeningTop) connectsToPath = true;
                    if (dir == new Vector2I(0, 1) && temp.OpeningBottom) connectsToPath = true;
                }
            }

            if (connectsToPath)
                result.Add(scene);

            temp.QueueFree();
        }

        return result;
    }
}