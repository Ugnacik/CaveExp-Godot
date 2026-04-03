using Godot;
using System;
using Godot.Collections;

public partial class LevelGenerator : Node2D
{
    [Export] public Array<PackedScene> RoomPool = new();
    [Export] public Vector2I GridSize = new Vector2I(4, 4);
    [Export] public Vector2 RoomSize = new Vector2(12, 8);

    private Random _random = new Random();

    public override void _Ready()
    {
        //Spawn the first Room
        
        GenerateLevel();
    }

    private void GenerateLevel()
    {
        for (int y = 0; y < GridSize.Y; y++)
        {
            for (int x = 0; x < GridSize.X; x++)
            {
                SpawnRoom(new Vector2I(x, y));
            }
        }
    }

    private void SpawnRoom(Vector2I gridPos)
    {
        if (RoomPool.Count == 0)
        {
            GD.Print("No rooms assigned!");
            return;
        }

        var scene = RoomPool[_random.Next(RoomPool.Count)];
        var room = scene.Instantiate<Node2D>();

        room.Position = new Vector2(
            gridPos.X * RoomSize.X * 16,
            gridPos.Y * RoomSize.Y * 16
        );

        AddChild(room);
    }
}