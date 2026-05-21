using Godot;
using System;
using System.Linq;
using System.Collections.Generic;


public partial class LevelGenerator : Node2D
{
    TileMapLayer dirtLayer;
    TileMapLayer spikeLayer;

    private Random rng = new Random();
    private List<RoomData> rooms;
    private Vector2I entranceRoom;
    private Vector2I exitRoom;

    public override void _Ready()
    {
        dirtLayer = GetNode<TileMapLayer>("Dirt");
        spikeLayer = GetNode<TileMapLayer>("Spikes");

        rooms = RoomLoader.Load("res://CaveExp-Godot/Scenes/Rooms/basic_rooms.json");
        GD.Print($"Rooms loaded: {rooms.Count}");

        GenerateLevel();
    }

    void GenerateLevel()
    {
        dirtLayer.Clear();

        var connections = new HashSet<Direction>[Constants.GRID_WIDTH, Constants.GRID_HEIGHT];
        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
                connections[x, y] = new HashSet<Direction>();

        // --- Trace the solution path ---
        int col = rng.Next(Constants.GRID_WIDTH);
        entranceRoom = new Vector2I(col, 0);

        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
        {
            // 70% chance to snake horizontally before dropping
            bool snake = rng.Next(10) < 7;

            if (snake)
            {
                int steps = rng.Next(1, 4);
                int dir = rng.Next(2) == 0 ? -1 : 1;

                for (int s = 0; s < steps; s++)
                {
                    int nextCol = col + dir;
                    if (nextCol < 0 || nextCol >= Constants.GRID_WIDTH) break;

                    Direction fromDir = dir == 1 ? Direction.Right : Direction.Left;
                    Direction toDir = dir == 1 ? Direction.Left : Direction.Right;
                    AddConnection(connections, col, y, nextCol, y, fromDir, toDir);
                    col = nextCol;
                }
            }

            // Drop down (unless this is the last row)
            if (y < Constants.GRID_HEIGHT - 1)
                AddConnection(connections, col, y, col, y + 1, Direction.Bottom, Direction.Top);
        }

        exitRoom = new Vector2I(col, Constants.GRID_HEIGHT - 1);
        GD.Print($"Entrance: {entranceRoom}, Exit: {exitRoom}");

        // --- Fill off-path rooms ---
        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
            {
                if (connections[x, y].Count > 0) continue;

                // Guarantee at least one horizontal connection by picking a side first
                bool canGoLeft = x > 0;
                bool canGoRight = x < Constants.GRID_WIDTH - 1;

                if (canGoLeft && canGoRight)
                {
                    // Forced: connect toward whichever neighbor is more connected (or random if equal)
                    int leftCount = connections[x - 1, y].Count;
                    int rightCount = connections[x + 1, y].Count;

                    if (leftCount > rightCount)
                        AddConnection(connections, x, y, x - 1, y, Direction.Left, Direction.Right);
                    else if (rightCount > leftCount)
                        AddConnection(connections, x, y, x + 1, y, Direction.Right, Direction.Left);
                    else if (rng.Next(2) == 0)
                        AddConnection(connections, x, y, x - 1, y, Direction.Left, Direction.Right);
                    else
                        AddConnection(connections, x, y, x + 1, y, Direction.Right, Direction.Left);

                    // 75% chance to also connect the other side
                    if (rng.Next(4) != 0)
                    {
                        if (!connections[x, y].Contains(Direction.Left) && canGoLeft)
                            AddConnection(connections, x, y, x - 1, y, Direction.Left, Direction.Right);
                        else if (!connections[x, y].Contains(Direction.Right) && canGoRight)
                            AddConnection(connections, x, y, x + 1, y, Direction.Right, Direction.Left);
                    }
                }
                else if (canGoLeft)
                    AddConnection(connections, x, y, x - 1, y, Direction.Left, Direction.Right);
                else if (canGoRight)
                    AddConnection(connections, x, y, x + 1, y, Direction.Right, Direction.Left);

                // Vertical — 33% chance, unchanged
                if (y < Constants.GRID_HEIGHT - 1 && rng.Next(3) == 0)
                    AddConnection(connections, x, y, x, y + 1, Direction.Bottom, Direction.Top);
            }
        }

        // --- Place rooms ---
        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
            {
                Direction[] needed = connections[x, y].ToArray();
                RoomData room = GetRoomByConnections(needed);

                if (room == null)
                {
                    GD.PrintErr($"No template for [{string.Join(", ", needed)}] at ({x},{y}). Using closed room.");
                    room = GetRoomByConnections(Array.Empty<Direction>());
                }

                Vector2I offset = new Vector2I(
                    x * (Constants.ROOM_WIDTH - 1),
                    y * (Constants.ROOM_HEIGHT - 1)
                );

                TilePlacer.PlaceRoom(room.layout, offset, dirtLayer, rng);
            }
        }

        GD.Print($"Level generated. Entrance: {entranceRoom}, Exit: {exitRoom}");
    }

    void AddConnection(
        HashSet<Direction>[,] connections,
        int ax, int ay,
        int bx, int by,
        Direction aDir,
        Direction bDir)
    {
        connections[ax, ay].Add(aDir);
        connections[bx, by].Add(bDir);
    }

    RoomData GetRoomByConnections(params Direction[] requiredConnections)
    {
        return rooms.FirstOrDefault(room =>
            room.connections.Length == requiredConnections.Length &&
            requiredConnections.All(connection => room.connections.Contains(connection)));
    }
}