using Godot;
using System;
using System.Collections.Generic;


public partial class LevelGenerator : Node2D
{
    TileMapLayer dirtLayer;
    TileMapLayer spikeLayer;
    private Random rng = new Random();
    private List<RoomData> rooms;

    public override void _Ready()
    {
        dirtLayer = GetNode<TileMapLayer>("Dirt");
        spikeLayer = GetNode<TileMapLayer>("Spikes");

        // Load rooms from JSON
        rooms = RoomLoader.Load("res://CaveExp-Godot/Scenes/Rooms/basic_rooms.json");
        GD.Print($"Rooms loaded: {rooms.Count}");

        GenerateLevel();
    }

    void GenerateLevel()
    {
        // Generate a 4x4 (GRID_HEIGHT x GRID_WIDTH) grid of Rooms
        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
            {
                RoomData room = RoomSelector.GetRandomRoom(rooms, rng);
                if (room == null)
                    return;

                Vector2I offset = new Vector2I(x * (Constants.ROOM_WIDTH - 1), y * (Constants.ROOM_HEIGHT - 1));
                PlaceRoom(room.layout, offset);
            }
        }
    }
    void PlaceRoom(int[][] layout, Vector2I offset)
{
    for (int y = 0; y < Constants.ROOM_HEIGHT; y++)
    {
        for (int x = 0; x < Constants.ROOM_WIDTH; x++)
        {
            int tile = layout[y][x];
            Vector2I pos = new Vector2I(x + offset.X, y + offset.Y);

            switch (tile)
            {
                case Constants.EMPTY:
                    break;

                case Constants.DIRT:
                    TilePlacer.PlaceDirt(pos, dirtLayer, rng);
                    break;

                case Constants.SPIKE:
                    TilePlacer.PlaceSpike(pos, spikeLayer);
                    break;

                case Constants.ENEMY:
                    TilePlacer.SpawnEnemy(pos);
                    break;
            }
        }
    }
}
}