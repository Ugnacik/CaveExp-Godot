using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;
using System.Text.Json.Serialization;


public partial class LevelGenerator : Node2D
{
    const int ROOM_WIDTH = 12;
    const int ROOM_HEIGHT = 8;

    const int EMPTY = 0;
    const int DIRT = 1;
    const int SPIKE = 2;
    const int ENEMY = 3;
    TileMapLayer dirtLayer;
    TileMapLayer spikeLayer;
    private Random rng = new Random();

    private List<RoomData> rooms;

    public override void _Ready()
    {
        dirtLayer = GetNode<TileMapLayer>("Dirt");
        spikeLayer = GetNode<TileMapLayer>("Spikes");

        // Load rooms from JSON
        rooms = LoadRooms("res://CaveExp-Godot/Scenes/Rooms/basic_rooms.json");

        // Validate rooms (optional but VERY useful)
        foreach (var room in rooms)
        {
            ValidateRoom(room);
        }
        GD.Print($"Rooms loaded: {rooms.Count}");

        // Generate a 4x4 grid
        for (int j = 0; j < 4; j++)
        {
            for (int i = 0; i < 4; i++)
            {
                RoomData room = GetRandomRoom();
                if (room == null)
                    return;

                Vector2I offset = new Vector2I(i * (ROOM_WIDTH - 1), j * (ROOM_HEIGHT - 1));
                GenerateRoom(room.layout, offset);
            }
        }
    }

    // -------------------------
    // ROOM LOADING
    // -------------------------

    List<RoomData> LoadRooms(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            GD.PrintErr($"File not found: {path}");
            return new List<RoomData>();
        }

        string json = FileAccess.GetFileAsString(path);

        // GD.Print("JSON content:");
        // GD.Print(json);

        var result = JsonSerializer.Deserialize<List<RoomData>>(json);

        if (result == null)
        {
            GD.PrintErr("JSON deserialization failed!");
            return new List<RoomData>();
        }

        return result;
    }

    RoomData GetRandomRoom()
    {
        if (rooms.Count == 0)
        {
            GD.PrintErr("No rooms loaded!");
            return null;
        }

        int index = rng.Next(rooms.Count);
        return rooms[index];
    }

    // -------------------------
    // GENERATION
    // -------------------------

    void GenerateRoom(int[][] layout, Vector2I offset)
    {
        GD.Print("Array2D");
        for (int y = 0; y < layout.Length; y++)
        {
            string row = "";
            for (int x = 0; x < layout[y].Length; x++)
            {
                row += layout[y][x] + " ";
                int tile = layout[y][x];
                Vector2I pos = new Vector2I(x + offset.X, y + offset.Y);

                switch (tile)
                {
                    case EMPTY:
                        break;

                    case DIRT:
                        PlaceTile(pos);
                        break;

                    case SPIKE:
                        PlaceSpike(pos);
                        break;

                    case ENEMY:
                        SpawnEnemy(pos);
                        break;
                }
            }
            GD.Print(row);
        }
    }

    // -------------------------
    // VALIDATION
    // -------------------------

    void ValidateRoom(RoomData room)
    {
        if (room.layout.Length != ROOM_HEIGHT)
        {
            GD.PrintErr($"Room {room.name} has wrong height!");
        }

        for (int y = 0; y < room.layout.Length; y++)
        {
            if (room.layout[y].Length != ROOM_WIDTH)
            {
                GD.PrintErr($"Room {room.name} row {y} has wrong width!");
            }
        }
    }

    // -------------------------
    // PLACE TILES
    // -------------------------

    void PlaceTile(Vector2I pos)
    {
        //GD.Print($"Placing tile at {pos}");
        dirtLayer.SetCell(pos, 11, new Vector2I(0, 0));
    }

    void PlaceSpike(Vector2I pos)
    {
        GD.Print($"Placing spike at {pos}");
        //spikeLayer.SetCell(pos, 0, new Vector2I(0, 0));
    }

    void SpawnEnemy(Vector2I pos)
    {
        GD.Print($"Spawning enemy at {pos}");
    }
}

// -------------------------
// DATA CLASS (IMPORTANT)
// -------------------------

public class RoomData
{
    [JsonPropertyName("name")]
    public string name { get; set; }

    [JsonPropertyName("connections")]
    public string[] connections { get; set; }

    [JsonPropertyName("layout")]
    public int[][] layout { get; set; }
}