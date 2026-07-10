using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class LevelGenerator : Node2D
{
    [Export] public PackedScene PlayerScene;

    private TileMapLayer dirtLayer;
    private TileMapLayer spikeLayer;
    private Sprite2D entranceSprite;
    private Sprite2D exitSprite;
    private Random rng = new Random();

    private List<RoomData> entranceRooms;
    private List<RoomData> exitRooms;
    private List<RoomData> standardRooms;

    private Vector2I entranceRoom;
    private Vector2I exitRoom;
    private RoomData selectedEntranceRoom;
    private RoomData selectedExitRoom;

    private Vector2I selectedEntranceMarkerShift;
    private Vector2I selectedExitMarkerShift;

    // Door position cache: stores where doors were actually placed for each room
    private Dictionary<Vector2I, Dictionary<Direction, int>> _doorPositions = new();

    public override void _Ready()
    {
        dirtLayer = GetNode<TileMapLayer>("Dirt");
        spikeLayer = GetNode<TileMapLayer>("Spikes");
        entranceSprite = GetNode<Sprite2D>("EntranceSprite");
        exitSprite = GetNode<Sprite2D>("ExitSprite");

        entranceRooms = RoomLoader.Load("res://Scenes/Rooms/entrance_rooms.json");
        exitRooms = RoomLoader.Load("res://Scenes/Rooms/exit_rooms.json");
        standardRooms = RoomLoader.Load("res://Scenes/Rooms/standard_rooms.json");

        GD.Print($"Entrance rooms: {entranceRooms.Count}, Exit rooms: {exitRooms.Count}, Standard rooms: {standardRooms.Count}");

        TilePlacer.EntityContainer = GetNode("Entities");
        TilePlacer.SpawnerPool = new Dictionary<int, List<PackedScene>>
        {
            [3] = new List<PackedScene> { GD.Load<PackedScene>("res://Scenes/Entities/bat.tscn") },
            [4] = new List<PackedScene> { GD.Load<PackedScene>("res://Scenes/Entities/snake.tscn") }
        };

        GenerateLevel();
    }

    private void GenerateLevel()
    {
        dirtLayer.Clear();
        TilePlacer.ClearSpawnedPositions();
        _doorPositions.Clear();

        var connections = new HashSet<Direction>[Constants.GRID_WIDTH, Constants.GRID_HEIGHT];
        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
                connections[x, y] = new HashSet<Direction>();

        // --- Trace the solution path ---
        int col = rng.Next(Constants.GRID_WIDTH);
        entranceRoom = new Vector2I(col, 0);

        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
        {
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
                if (connections[col, y].Contains(Direction.Top))
                {
                    int dir = (col == 0) ? 1 : (col == Constants.GRID_WIDTH - 1 ? -1 : (rng.Next(2) == 0 ? -1 : 1));
                    int nextCol = col + dir;
                    AddConnection(connections, col, y, nextCol, y,
                        dir == 1 ? Direction.Right : Direction.Left,
                        dir == 1 ? Direction.Left : Direction.Right);
                    col = nextCol;
                }
                AddConnection(connections, col, y, col, y + 1, Direction.Bottom, Direction.Top);
            }
        }

        exitRoom = new Vector2I(col, Constants.GRID_HEIGHT - 1);
        GD.Print($"Entrance: {entranceRoom}, Exit: {exitRoom}");

        // --- Count vertical drops per column ---
        int[] verticalDropsInColumn = new int[Constants.GRID_WIDTH];
        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
                if (connections[x, y].Contains(Direction.Bottom))
                    verticalDropsInColumn[x]++;

        // --- Off-path drop budget ---
        int[] offPathDropsAllowed = new int[Constants.GRID_WIDTH];
        for (int x = 0; x < Constants.GRID_WIDTH; x++)
        {
            if (verticalDropsInColumn[x] >= 1) { offPathDropsAllowed[x] = 0; continue; }

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

                bool hasTop = connections[x, y].Contains(Direction.Top);
                if (y < Constants.GRID_HEIGHT - 1
                    && !connections[x, y + 1].Contains(Direction.Top)
                    && offPathDropsAllowed[x] > 0
                    && rng.Next(6) == 0
                    && !hasTop)
                {
                    AddConnection(connections, x, y, x, y + 1, Direction.Bottom, Direction.Top);
                    offPathDropsAllowed[x]--;
                    verticalDropsInColumn[x]++;
                }
            }
        }

        // --- Place rooms with dynamic door alignment ---
        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
        {

            for (int x = 0; x < Constants.GRID_WIDTH; x++)
            {
                Direction[] needed = connections[x, y].ToArray();
                RoomData room;
                bool isEntrance = (x == entranceRoom.X && y == entranceRoom.Y);
                bool isExit = (x == exitRoom.X && y == exitRoom.Y);

                if (isEntrance)
                {
                    room = entranceRooms.FirstOrDefault(r =>
                        r.connections.Length == needed.Length &&
                        needed.All(c => r.connections.Contains(c)));

                    if (room == null)
                    {
                        GD.PrintErr($"No entrance template for [{string.Join(", ", needed)}] at ({x},{y}). Using closed entrance room.");
                        room = entranceRooms.FirstOrDefault(r => r.connections.Length == 0);
                    }

                    selectedEntranceRoom = room;
                    GD.Print($"Entrance tile ({x},{y}) needs [{string.Join(",", needed)}] -> picked '{room.name}'");
                }
                else if (isExit)
                {
                    room = exitRooms.FirstOrDefault(r =>
                        r.connections.Length == needed.Length &&
                        needed.All(c => r.connections.Contains(c)));

                    if (room == null)
                    {
                        GD.PrintErr($"No exit template for [{string.Join(", ", needed)}] at ({x},{y}). Using closed exit room.");
                        room = exitRooms.FirstOrDefault(r => r.connections.Length == 1 && r.connections.Contains(Direction.Top));
                    }

                    selectedExitRoom = room;
                    GD.Print($"Exit tile ({x},{y}) needs [{string.Join(",", needed)}] -> picked '{room.name}'");
                }
                else
                {
                    room = GetRoomByConnections(needed);

                    if (room == null)
                    {
                        GD.PrintErr($"No standard template for [{string.Join(", ", needed)}] at ({x},{y}). Using closed room.");
                        room = GetRoomByConnections(Array.Empty<Direction>());
                    }
                }

                if (room == null)
                {
                    GD.PrintErr($"No closed-room fallback available at ({x},{y}). Skipping room placement.");
                    continue;
                }

                // Calculate door offsets so openings align between adjacent rooms
                var doorOffsets = CalculateDoorOffsets(room, needed, x, y, connections);
                _doorPositions[new Vector2I(x, y)] = doorOffsets;

                // Shift the layout so doors line up with neighbors
                int[][] shiftedLayout = ShiftLayoutForDoors(room.layout, doorOffsets, out int shiftX, out int shiftY);

                if (isEntrance) selectedEntranceMarkerShift = new Vector2I(shiftX, shiftY);
                if (isExit) selectedExitMarkerShift = new Vector2I(shiftX, shiftY);

                Vector2I offset = new Vector2I(
                    x * (Constants.ROOM_WIDTH - 1),
                    y * (Constants.ROOM_HEIGHT - 1)
                );

                SpawnFlags allowedSpawns = SpawnFlags.All;
                if (isEntrance) allowedSpawns = SpawnFlags.None;

                TilePlacer.PlaceRoom(shiftedLayout, offset, dirtLayer, spikeLayer, rng, allowedSpawns);
            }
        }

        PlaceMarkers();
        SpawnPlayer();

        GD.Print($"Level generated. Entrance: {entranceRoom}, Exit: {exitRoom}");
    }

    /// For each required connection direction, find where the neighbor's door is
    /// and record that position so this room's door can be shifted to match.
    private Dictionary<Direction, int> CalculateDoorOffsets(
            RoomData room, Direction[] needed, int x, int y,
            HashSet<Direction>[,] connections)
        {
            var offsets = new Dictionary<Direction, int>();

            foreach (var dir in needed)
            {
                int targetPos = GetDefaultDoorPosition(dir);

                // If the neighbor already exists and has a recorded door position, match it
                Vector2I neighbor = dir switch
                {
                    Direction.Top => new Vector2I(x, y - 1),
                    Direction.Bottom => new Vector2I(x, y + 1),
                    Direction.Left => new Vector2I(x - 1, y),
                    Direction.Right => new Vector2I(x + 1, y),
                    _ => new Vector2I(x, y)
                };

                Direction oppositeDir = dir switch
                {
                    Direction.Top => Direction.Bottom,
                    Direction.Bottom => Direction.Top,
                    Direction.Left => Direction.Right,
                    Direction.Right => Direction.Left,
                    _ => dir
                };

                if (_doorPositions.TryGetValue(neighbor, out var neighborDoors)
                    && neighborDoors.TryGetValue(oppositeDir, out int neighborDoorPos))
                {
                    targetPos = neighborDoorPos;
                }
                else
                {
                    // Neighbor not yet placed: pick a random valid door position on this edge
                    targetPos = PickRandomDoorPosition(room.layout, dir);
                }

                offsets[dir] = targetPos;
            }

            return offsets;
        }


    /// Returns the default center-door position for a given edge.
    /// Horizontal edges use X center, vertical edges use Y center.
    /// </summary>
    private int GetDefaultDoorPosition(Direction dir)
    {
        return dir switch
        {
            Direction.Top or Direction.Bottom => Constants.ROOM_WIDTH / 2,
            Direction.Left or Direction.Right => Constants.ROOM_HEIGHT / 2,
            _ => 0
        };
    }


    /// Scans the room layout for existing openings on the specified edge
    /// and returns one at random. Falls back to center if none found.
    /// </summary>
    private int PickRandomDoorPosition(int[][] layout, Direction dir)
    {
        var validPositions = new List<int>();

        switch (dir)
        {
            case Direction.Top:
                for (int x = 0; x < layout[0].Length; x++)
                    if (layout[0][x] == 0) validPositions.Add(x);
                break;
            case Direction.Bottom:
                int bottomRow = layout.Length - 1;
                for (int x = 0; x < layout[bottomRow].Length; x++)
                    if (layout[bottomRow][x] == 0) validPositions.Add(x);
                break;
            case Direction.Left:
                for (int y = 0; y < layout.Length; y++)
                    if (layout[y][0] == 0) validPositions.Add(y);
                break;
            case Direction.Right:
                int rightCol = layout[0].Length - 1;
                for (int y = 0; y < layout.Length; y++)
                    if (layout[y][rightCol] == 0) validPositions.Add(y);
                break;
        }

        if (validPositions.Count == 0)
            return GetDefaultDoorPosition(dir);

        return validPositions[rng.Next(validPositions.Count)];
    }


    /// Creates a copy of the layout with rows/columns cyclically shifted
    /// so that door openings align with the target positions.
    /// Only shifts the axis relevant to each direction.
    private int[][] ShiftLayoutForDoors(int[][] original, Dictionary<Direction, int> doorOffsets, out int shiftX, out int shiftY)
    {
        shiftX = 0;
        shiftY = 0;

        int height = original.Length;
        int width = original[0].Length;
        int[][] result = new int[height][];

        for (int y = 0; y < height; y++)
            result[y] = (int[])original[y].Clone();

        if (doorOffsets.TryGetValue(Direction.Top, out int topTarget) ||
            doorOffsets.TryGetValue(Direction.Bottom, out _))
        {
            int targetX = doorOffsets.ContainsKey(Direction.Top) ? topTarget : doorOffsets[Direction.Bottom];
            int currentCenter = width / 2;
            shiftX = targetX - currentCenter;

            if (shiftX != 0)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    int[] newRow = new int[width];
                    for (int x = 0; x < width; x++)
                    {
                        int srcX = ((x - shiftX) % width + width) % width;
                        newRow[x] = result[y][srcX];
                    }
                    newRow[0] = 1;
                    newRow[width - 1] = 1;
                    result[y] = newRow;
                }
            }
        }

        if (doorOffsets.TryGetValue(Direction.Left, out int leftTarget) ||
            doorOffsets.TryGetValue(Direction.Right, out _))
        {
            int targetY = doorOffsets.ContainsKey(Direction.Left) ? leftTarget : doorOffsets[Direction.Right];
            int currentCenter = height / 2;
            shiftY = targetY - currentCenter;

            if (shiftY != 0)
            {
                int[][] vertBuffer = new int[height][];
                for (int y = 0; y < height; y++)
                    vertBuffer[y] = new int[width];

                for (int x = 1; x < width - 1; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        int srcY = ((y - shiftY) % height + height) % height;
                        vertBuffer[y][x] = result[srcY][x];
                    }
                }

                for (int y = 1; y < height - 1; y++)
                    for (int x = 1; x < width - 1; x++)
                        result[y][x] = vertBuffer[y][x];
            }
        }

        foreach (var kvp in doorOffsets)
        {
            switch (kvp.Key)
            {
                case Direction.Top:
                    result[0][kvp.Value] = 0;
                    if (kvp.Value + 1 < width) result[0][kvp.Value + 1] = 0;
                    break;
                case Direction.Bottom:
                    result[height - 1][kvp.Value] = 0;
                    if (kvp.Value + 1 < width) result[height - 1][kvp.Value + 1] = 0;
                    break;
                case Direction.Left:
                    result[kvp.Value][0] = 0;
                    if (kvp.Value + 1 < height) result[kvp.Value + 1][0] = 0;
                    break;
                case Direction.Right:
                    result[kvp.Value][width - 1] = 0;
                    if (kvp.Value + 1 < height) result[kvp.Value + 1][width - 1] = 0;
                    break;
            }
        }

        return result;
    }

    private void SpawnPlayer()
    {
        if (PlayerScene == null || selectedEntranceRoom == null) return;

        var playerInstance = PlayerScene.Instantiate<Node2D>();

        int tileX = entranceRoom.X * (Constants.ROOM_WIDTH - 1) + selectedEntranceRoom.markerPos[0] + selectedEntranceMarkerShift.X;
        int tileY = entranceRoom.Y * (Constants.ROOM_HEIGHT - 1) + selectedEntranceRoom.markerPos[1] + selectedEntranceMarkerShift.Y;

        Vector2 worldPos = dirtLayer.MapToLocal(new Vector2I(tileX, tileY));
        playerInstance.GlobalPosition = worldPos + dirtLayer.GlobalPosition;

        AddChild(playerInstance);
        GD.Print($"Player spawned at {playerInstance.GlobalPosition}");
    }

    private void PlaceMarkers()
    {
        Vector2 offset = dirtLayer.Position;

        if (selectedEntranceRoom == null) GD.PrintErr("Missing entrance room data!");
        if (selectedExitRoom == null) GD.PrintErr("Missing exit room data!");

        Vector2I entranceTile;
        if (selectedEntranceRoom?.markerPos != null)
            entranceTile = new Vector2I(
                entranceRoom.X * (Constants.ROOM_WIDTH - 1) + selectedEntranceRoom.markerPos[0] + selectedEntranceMarkerShift.X,
                entranceRoom.Y * (Constants.ROOM_HEIGHT - 1) + selectedEntranceRoom.markerPos[1] + selectedEntranceMarkerShift.Y);
        else
            entranceTile = new Vector2I(
                entranceRoom.X * (Constants.ROOM_WIDTH - 1) + Constants.ROOM_WIDTH / 2,
                entranceRoom.Y * (Constants.ROOM_HEIGHT - 1) + 1);

        entranceSprite.Position = dirtLayer.MapToLocal(entranceTile) + offset;

        Vector2I exitTile;
        if (selectedExitRoom?.markerPos != null)
            exitTile = new Vector2I(
                exitRoom.X * (Constants.ROOM_WIDTH - 1) + selectedExitRoom.markerPos[0] + selectedExitMarkerShift.X,
                exitRoom.Y * (Constants.ROOM_HEIGHT - 1) + selectedExitRoom.markerPos[1] + selectedExitMarkerShift.Y);
        else
            exitTile = new Vector2I(
                exitRoom.X * (Constants.ROOM_WIDTH - 1) + Constants.ROOM_WIDTH / 2,
                exitRoom.Y * (Constants.ROOM_HEIGHT - 1) + 1);

        exitSprite.Position = dirtLayer.MapToLocal(exitTile) + offset;
    }

    private void AddConnection(
        HashSet<Direction>[,] connections,
        int ax, int ay, int bx, int by,
        Direction aDir, Direction bDir)
    {
        connections[ax, ay].Add(aDir);
        connections[bx, by].Add(bDir);
    }

    private RoomData GetRoomByConnections(params Direction[] requiredConnections)
    {
        var matchingRooms = standardRooms.Where(room =>
            room.connections.Length == requiredConnections.Length &&
            requiredConnections.All(c => room.connections.Contains(c))).ToList();

        if (matchingRooms.Count == 0) return null;
        return matchingRooms[rng.Next(matchingRooms.Count)];
    }

    private void _on_exit_body_entered(Node2D body)
    {
        // Verify it's the player using a group check (cleaner than type checking)
        if (!body.IsInGroup("player")) return;

        GD.Print("Exit reached! Reloading level...");

        // Disable the exit immediately to prevent double-triggering during transition
        var exitArea = GetNode<Area2D>("ExitSprite/Area2D");
        exitArea.SetDeferred(Area2D.PropertyName.Monitoring, false);

        // Reload the current scene
        GetTree().ReloadCurrentScene();
    }
}
