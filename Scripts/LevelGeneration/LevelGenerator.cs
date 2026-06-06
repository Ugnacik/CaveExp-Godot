using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class LevelGenerator : Node2D
{
    TileMapLayer dirtLayer;
    TileMapLayer spikeLayer;
    Sprite2D entranceSprite;
    Sprite2D exitSprite;

    private Random rng = new Random();
    private List<RoomData> entranceRooms;
    private List<RoomData> exitRooms;
    private List<RoomData> standardRooms;
    private Vector2I entranceRoom;
    private Vector2I exitRoom;
    private RoomData selectedEntranceRoom;
    private RoomData selectedExitRoom;

    public override void _Ready()
    {
        dirtLayer = GetNode<TileMapLayer>("Dirt");
        spikeLayer = GetNode<TileMapLayer>("Spikes");

        entranceSprite = GetNode<Sprite2D>("EntranceSprite");
        exitSprite = GetNode<Sprite2D>("ExitSprite");

        entranceRooms = RoomLoader.Load("res://Scenes/Rooms/entrance_rooms.json");
        exitRooms = RoomLoader.Load("res://Scenes/Rooms/exit_rooms.json");
        standardRooms = RoomLoader.Load("res://Scenes/Rooms/basic_rooms.json");
        GD.Print($"Entrance rooms: {entranceRooms.Count}, Exit rooms: {exitRooms.Count}, Standard rooms: {standardRooms.Count}");

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
            // Increased to 90% to naturally reduce straight drops
            bool snake = rng.Next(10) < 9; 
            if (y == 0) snake = true; 

            if (snake)
            {
                int steps = rng.Next(1, 4); 
                int dir = rng.Next(2) == 0 ? -1 : 1;
                if (y == 0)
                {
                    if (col == 0) dir = 1;
                    else if (col == Constants.GRID_WIDTH - 1) dir = -1;
                }

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

            if (y < Constants.GRID_HEIGHT - 1)
            {
                // BETTER IDEA: Prevent solution path from creating consecutive drops
                if (connections[col, y].Contains(Direction.Top))
                {
                    // Force at least one horizontal step to offset the drop
                    int dir = (col == 0) ? 1 : (col == Constants.GRID_WIDTH - 1 ? -1 : (rng.Next(2) == 0 ? -1 : 1));
                    int nextCol = col + dir;
                    AddConnection(connections, col, y, nextCol, y, dir == 1 ? Direction.Right : Direction.Left, dir == 1 ? Direction.Left : Direction.Right);
                    col = nextCol;
                }
                AddConnection(connections, col, y, col, y + 1, Direction.Bottom, Direction.Top);
            }
        }

        exitRoom = new Vector2I(col, Constants.GRID_HEIGHT - 1);
        GD.Print($"Entrance: {entranceRoom}, Exit: {exitRoom}");

        // --- Count vertical drops per column from solution path ---
        int[] verticalDropsInColumn = new int[Constants.GRID_WIDTH];
        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
                if (connections[x, y].Contains(Direction.Bottom))
                    verticalDropsInColumn[x]++;

        // --- Decide upfront how many off-path drops each column gets ---
        int[] offPathDropsAllowed = new int[Constants.GRID_WIDTH];
        for (int x = 0; x < Constants.GRID_WIDTH; x++)
        {
            if (verticalDropsInColumn[x] >= 1)
            {
                offPathDropsAllowed[x] = 0;
                continue;
            }

            int roll = rng.Next(100);
            if (roll < 88) offPathDropsAllowed[x] = 0;  
            else if (roll < 99) offPathDropsAllowed[x] = 1;  
            else offPathDropsAllowed[x] = 2;  

            int cap = 2;
            if (verticalDropsInColumn[x] + offPathDropsAllowed[x] > cap)
                offPathDropsAllowed[x] = Math.Max(0, cap - verticalDropsInColumn[x]);
        }

        // --- Fill off-path rooms ---
        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
            {
                if (connections[x, y].Count > 0) continue;

                bool canGoLeft = x > 0;
                bool canGoRight = x < Constants.GRID_WIDTH - 1;

                if (canGoLeft && canGoRight)
                {
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

                // Vertical — consume from the per-column budget
                bool hasTop = connections[x, y].Contains(Direction.Top);
                if (y < Constants.GRID_HEIGHT - 1
                     && !connections[x, y + 1].Contains(Direction.Top)
                     && offPathDropsAllowed[x] > 0
                     && rng.Next(6) == 0
                     && !hasTop) // BETTER IDEA: Never add Bottom if we already have Top
                {
                    AddConnection(connections, x, y, x, y + 1, Direction.Bottom, Direction.Top);
                    offPathDropsAllowed[x]--;
                    verticalDropsInColumn[x]++;
                }
            }
        }

        // --- Place rooms ---
        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
            {
                Direction[] needed = connections[x, y].ToArray();
                RoomData room;
                if (x == entranceRoom.X && y == entranceRoom.Y)
                {
                    room = entranceRooms.FirstOrDefault(r =>
                        r.connections.Length == needed.Length &&
                        needed.All(c => r.connections.Contains(c)));
                    selectedEntranceRoom = room;
                }
                else if (x == exitRoom.X && y == exitRoom.Y)
                {
                    room = exitRooms.FirstOrDefault(r =>
                        r.connections.Length == needed.Length &&
                        needed.All(c => r.connections.Contains(c)));
                    selectedExitRoom = room;
                }
                else
                    room = GetRoomByConnections(needed);

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

        PlaceMarkers();

        GD.Print($"Level generated. Entrance: {entranceRoom}, Exit: {exitRoom}");
    }

    void PlaceMarkers()
    {
        Vector2 offset = dirtLayer.Position;

        if (selectedEntranceRoom == null)
            GD.PrintErr("Missing entrance room data!");
        if (selectedExitRoom == null)
            GD.PrintErr("Missing exit room data!");

        Vector2I entranceTile;
        if (selectedEntranceRoom?.markerPos != null)
            entranceTile = new Vector2I(
                entranceRoom.X * (Constants.ROOM_WIDTH - 1) + selectedEntranceRoom.markerPos[0],
                entranceRoom.Y * (Constants.ROOM_HEIGHT - 1) + selectedEntranceRoom.markerPos[1]
            );
        else
            entranceTile = new Vector2I( 
                entranceRoom.X * (Constants.ROOM_WIDTH - 1) + Constants.ROOM_WIDTH / 2,
                entranceRoom.Y * (Constants.ROOM_HEIGHT - 1) + 1
            );

        entranceSprite.Position = dirtLayer.MapToLocal(entranceTile) + offset;

        Vector2I exitTile;
        if (selectedExitRoom?.markerPos != null)
            exitTile = new Vector2I(
                exitRoom.X * (Constants.ROOM_WIDTH - 1) + selectedExitRoom.markerPos[0],
                exitRoom.Y * (Constants.ROOM_HEIGHT - 1) + selectedExitRoom.markerPos[1]
            );
        else
            exitTile = new Vector2I(
                exitRoom.X * (Constants.ROOM_WIDTH - 1) + Constants.ROOM_WIDTH / 2,
                exitRoom.Y * (Constants.ROOM_HEIGHT - 1) + 1
            );

        exitSprite.Position = dirtLayer.MapToLocal(exitTile) + offset;
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
        return standardRooms.FirstOrDefault(room =>
            room.connections.Length == requiredConnections.Length &&
            requiredConnections.All(connection => room.connections.Contains(connection)));
    }
}