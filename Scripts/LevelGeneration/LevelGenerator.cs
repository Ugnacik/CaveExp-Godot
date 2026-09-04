using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class LevelGenerator : Node2D
{
    [Export] public PackedScene PlayerScene;
    [Export] public int GenerationSeed = -1;
    [Export(PropertyHint.Range, "1,100,1")] public int MaxGenerationAttempts = 30;
    [Export(PropertyHint.Range, "0,1,0.01")] public float FullSpanDoorwayChance = 0.18f;
    [Export(PropertyHint.Range, "0,4,1")] public int MaxCriticalPathRiseTiles = 2;

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

    private readonly struct DoorwaySpan
    {
        public int Start { get; }
        public int Length { get; }
        public int End => Start + Length - 1;
        public int ApproachFloorRow => Constants.ROOM_HEIGHT / 2 + 1;

        public DoorwaySpan(int start, int length)
        {
            Start = start;
            Length = length;
        }

        public override string ToString() => $"{Start}..{End}";
    }

    private sealed class PlannedRoom
    {
        public RoomData Template { get; }
        public int[][] Layout { get; }
        public bool IsEntrance { get; }
        public bool IsExit { get; }

        public PlannedRoom(RoomData template, int[][] layout, bool isEntrance, bool isExit)
        {
            Template = template;
            Layout = layout;
            IsEntrance = isEntrance;
            IsExit = isExit;
        }
    }

    private sealed class LevelPlan
    {
        public PlannedRoom[,] Rooms { get; }
        public Vector2I Entrance { get; }
        public Vector2I Exit { get; }
        public IReadOnlyList<Vector2I> SolutionPath { get; }

        public LevelPlan(
            PlannedRoom[,] rooms,
            Vector2I entrance,
            Vector2I exit,
            IReadOnlyList<Vector2I> solutionPath)
        {
            Rooms = rooms;
            Entrance = entrance;
            Exit = exit;
            SolutionPath = solutionPath;
        }
    }

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

        if (!HasRequiredTemplateCoverage())
        {
            GD.PushError("Room templates do not cover every connection shape required by the generator.");
            return;
        }

        GenerateLevel();
    }

    private void GenerateLevel()
    {
        int baseSeed = GenerationSeed >= 0 ? GenerationSeed : Random.Shared.Next();

        for (int attempt = 0; attempt < Math.Max(1, MaxGenerationAttempts); attempt++)
        {
            int attemptSeed = unchecked(baseSeed + attempt * 104729);
            rng = new Random(attemptSeed);

            if (!TryBuildLevelPlan(out LevelPlan plan, out string failure))
            {
                GD.PrintErr($"Rejected level seed {attemptSeed}: {failure}");
                continue;
            }

            CommitLevel(plan);
            GD.Print($"Level generated with seed {attemptSeed} on attempt {attempt + 1}. Entrance: {entranceRoom}, Exit: {exitRoom}");
            GD.Print($"Solution path: {string.Join(" -> ", plan.SolutionPath)}");
            return;
        }

        GD.PushError($"Could not generate a valid level after {MaxGenerationAttempts} attempts. Base seed: {baseSeed}");
    }

    private bool TryBuildLevelPlan(out LevelPlan plan, out string failure)
    {
        plan = null;
        failure = string.Empty;

        var connections = CreateConnectionGrid();
        var solutionPath = TraceSolutionPath(connections, out Vector2I entrance, out Vector2I exit);
        FillOffPathRooms(connections);
        EnsureTopEntrancesHaveEscape(connections);

        var doorwaySpans = CreateDoorwaySpans(connections, solutionPath);
        var rooms = new PlannedRoom[Constants.GRID_WIDTH, Constants.GRID_HEIGHT];

        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
            {
                Vector2I gridPosition = new Vector2I(x, y);
                Direction[] needed = connections[x, y].ToArray();
                bool isEntrance = gridPosition == entrance;
                bool isExit = gridPosition == exit;

                List<RoomData> source = isEntrance ? entranceRooms : (isExit ? exitRooms : standardRooms);
                RoomData room = GetRoomByConnections(source, needed);

                if (room == null)
                {
                    failure = $"No {(isEntrance ? "entrance" : isExit ? "exit" : "standard")} template for [{string.Join(",", needed)}] at {gridPosition}.";
                    return false;
                }

                int[][] layout = PrepareRoomLayout(room, doorwaySpans[x, y]);
                if (!ValidatePreparedRoom(room, layout, doorwaySpans[x, y], out string roomFailure))
                {
                    failure = $"Template '{room.name}' at {gridPosition} failed connector validation: {roomFailure}";
                    return false;
                }

                rooms[x, y] = new PlannedRoom(room, layout, isEntrance, isExit);
            }
        }

        if (!ValidateAssembledLevel(rooms, entrance, exit, out failure))
            return false;

        plan = new LevelPlan(rooms, entrance, exit, solutionPath);
        return true;
    }

    private HashSet<Direction>[,] CreateConnectionGrid()
    {
        var connections = new HashSet<Direction>[Constants.GRID_WIDTH, Constants.GRID_HEIGHT];
        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
                connections[x, y] = new HashSet<Direction>();
        return connections;
    }

    private List<Vector2I> TraceSolutionPath(
        HashSet<Direction>[,] connections,
        out Vector2I entrance,
        out Vector2I exit)
    {
        int col = rng.Next(Constants.GRID_WIDTH);
        entrance = new Vector2I(col, 0);
        var path = new List<Vector2I> { entrance };

        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
        {
            bool snake = y == 0 || rng.Next(10) < 9;
            if (snake)
            {
                int steps = rng.Next(1, 4);
                int horizontalDirection = rng.Next(2) == 0 ? -1 : 1;

                if (y == 0)
                {
                    if (col == 0) horizontalDirection = 1;
                    else if (col == Constants.GRID_WIDTH - 1) horizontalDirection = -1;
                }

                for (int step = 0; step < steps; step++)
                {
                    int nextCol = col + horizontalDirection;
                    if (nextCol < 0 || nextCol >= Constants.GRID_WIDTH) break;

                    Direction from = horizontalDirection == 1 ? Direction.Right : Direction.Left;
                    Direction to = Opposite(from);
                    AddConnection(connections, col, y, nextCol, y, from, to);
                    col = nextCol;
                    path.Add(new Vector2I(col, y));
                }
            }

            if (y >= Constants.GRID_HEIGHT - 1) continue;

            if (connections[col, y].Contains(Direction.Top))
            {
                int horizontalDirection = col == 0 ? 1 :
                    (col == Constants.GRID_WIDTH - 1 ? -1 : (rng.Next(2) == 0 ? -1 : 1));
                int nextCol = col + horizontalDirection;
                Direction from = horizontalDirection == 1 ? Direction.Right : Direction.Left;
                AddConnection(connections, col, y, nextCol, y, from, Opposite(from));
                col = nextCol;
                path.Add(new Vector2I(col, y));
            }

            AddConnection(connections, col, y, col, y + 1, Direction.Bottom, Direction.Top);
            path.Add(new Vector2I(col, y + 1));
        }

        exit = new Vector2I(col, Constants.GRID_HEIGHT - 1);
        return path;
    }

    private void FillOffPathRooms(HashSet<Direction>[,] connections)
    {
        int[] verticalDropsInColumn = new int[Constants.GRID_WIDTH];
        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
                if (connections[x, y].Contains(Direction.Bottom))
                    verticalDropsInColumn[x]++;

        int[] offPathDropsAllowed = new int[Constants.GRID_WIDTH];
        for (int x = 0; x < Constants.GRID_WIDTH; x++)
        {
            if (verticalDropsInColumn[x] >= 1)
            {
                offPathDropsAllowed[x] = 0;
                continue;
            }

            int roll = rng.Next(100);
            offPathDropsAllowed[x] = roll < 88 ? 0 : (roll < 99 ? 1 : 2);
            offPathDropsAllowed[x] = Math.Min(offPathDropsAllowed[x], 2 - verticalDropsInColumn[x]);
        }

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
                    bool connectLeft = leftCount > rightCount || (leftCount == rightCount && rng.Next(2) == 0);

                    if (connectLeft)
                        AddConnection(connections, x, y, x - 1, y, Direction.Left, Direction.Right);
                    else
                        AddConnection(connections, x, y, x + 1, y, Direction.Right, Direction.Left);

                    if (rng.Next(4) != 0)
                    {
                        if (!connections[x, y].Contains(Direction.Left))
                            AddConnection(connections, x, y, x - 1, y, Direction.Left, Direction.Right);
                        else if (!connections[x, y].Contains(Direction.Right))
                            AddConnection(connections, x, y, x + 1, y, Direction.Right, Direction.Left);
                    }
                }
                else if (canGoLeft)
                {
                    AddConnection(connections, x, y, x - 1, y, Direction.Left, Direction.Right);
                }
                else if (canGoRight)
                {
                    AddConnection(connections, x, y, x + 1, y, Direction.Right, Direction.Left);
                }

                bool hasTop = connections[x, y].Contains(Direction.Top);
                if (y < Constants.GRID_HEIGHT - 1
                    && !connections[x, y + 1].Contains(Direction.Top)
                    && offPathDropsAllowed[x] > 0
                    && rng.Next(6) == 0
                    && !hasTop)
                {
                    AddConnection(connections, x, y, x, y + 1, Direction.Bottom, Direction.Top);
                    offPathDropsAllowed[x]--;
                }
            }
        }
    }

    private void EnsureTopEntrancesHaveEscape(HashSet<Direction>[,] connections)
    {
        for (int y = 1; y < Constants.GRID_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
            {
                HashSet<Direction> roomConnections = connections[x, y];
                if (roomConnections.Count != 1 || !roomConnections.Contains(Direction.Top))
                    continue;

                int neighborX;
                if (x == 0)
                    neighborX = 1;
                else if (x == Constants.GRID_WIDTH - 1)
                    neighborX = x - 1;
                else
                    neighborX = connections[x - 1, y].Count <= connections[x + 1, y].Count ? x - 1 : x + 1;

                Direction direction = neighborX < x ? Direction.Left : Direction.Right;
                AddConnection(connections, x, y, neighborX, y, direction, Opposite(direction));
            }
        }
    }

    private Dictionary<Direction, DoorwaySpan>[,] CreateDoorwaySpans(
        HashSet<Direction>[,] connections,
        IReadOnlyList<Vector2I> solutionPath)
    {
        var spans = new Dictionary<Direction, DoorwaySpan>[Constants.GRID_WIDTH, Constants.GRID_HEIGHT];
        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
                spans[x, y] = new Dictionary<Direction, DoorwaySpan>();

        DoorwaySpan? previousHorizontalSpan = null;
        for (int i = 0; i < solutionPath.Count - 1; i++)
        {
            Vector2I from = solutionPath[i];
            Vector2I to = solutionPath[i + 1];
            Direction direction = DirectionFromTo(from, to);

            DoorwaySpan span;
            if (direction == Direction.Left || direction == Direction.Right)
            {
                int minimumFloorRow = previousHorizontalSpan.HasValue
                    ? Math.Max(3, previousHorizontalSpan.Value.ApproachFloorRow - Math.Max(0, MaxCriticalPathRiseTiles))
                    : 5;
                span = CreateHorizontalPathDoorway(minimumFloorRow);
                previousHorizontalSpan = span;
            }
            else
            {
                span = CreateRandomDoorway(direction);
                previousHorizontalSpan = null;
            }

            AssignSharedDoorway(spans, from, to, direction, span);
        }

        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
            {
                Vector2I position = new Vector2I(x, y);

                if (connections[x, y].Contains(Direction.Right)
                    && !spans[x, y].ContainsKey(Direction.Right))
                {
                    Vector2I neighbor = new Vector2I(x + 1, y);
                    AssignSharedDoorway(spans, position, neighbor, Direction.Right, CreateRandomDoorway(Direction.Right));
                }

                if (connections[x, y].Contains(Direction.Bottom)
                    && !spans[x, y].ContainsKey(Direction.Bottom))
                {
                    Vector2I neighbor = new Vector2I(x, y + 1);
                    AssignSharedDoorway(spans, position, neighbor, Direction.Bottom, CreateRandomDoorway(Direction.Bottom));
                }
            }
        }

        return spans;
    }

    private DoorwaySpan CreateHorizontalPathDoorway(int minimumFloorRow)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            DoorwaySpan candidate = CreateRandomDoorway(Direction.Left);
            if (candidate.ApproachFloorRow >= minimumFloorRow)
                return candidate;
        }

        return new DoorwaySpan(1, Constants.ROOM_HEIGHT - 2);
    }

    private DoorwaySpan CreateRandomDoorway(Direction direction)
    {
        int axisLength = direction == Direction.Top || direction == Direction.Bottom
            ? Constants.ROOM_WIDTH
            : Constants.ROOM_HEIGHT;

        // Corners remain structural supports. A full opening spans every usable edge tile.
        int usableLength = axisLength - 2;
        int length;

        if (usableLength <= 2 || rng.NextDouble() < Math.Clamp(FullSpanDoorwayChance, 0f, 1f))
            length = usableLength;
        else
            length = rng.Next(2, usableLength);

        int start;
        if (direction == Direction.Left || direction == Direction.Right)
        {
            int passageTop = Constants.ROOM_HEIGHT / 2 - 1;
            int passageBottom = passageTop + 1;
            int minimumStart = Math.Max(1, passageBottom - length + 1);
            int maximumStart = Math.Min(passageTop, axisLength - length - 1);
            start = rng.Next(minimumStart, maximumStart + 1);
        }
        else
        {
            start = length == usableLength ? 1 : rng.Next(1, axisLength - length);
        }

        return new DoorwaySpan(start, length);
    }

    private void AssignSharedDoorway(
        Dictionary<Direction, DoorwaySpan>[,] spans,
        Vector2I from,
        Vector2I to,
        Direction direction,
        DoorwaySpan span)
    {
        spans[from.X, from.Y][direction] = span;
        spans[to.X, to.Y][Opposite(direction)] = span;
    }

    private int[][] PrepareRoomLayout(RoomData room, Dictionary<Direction, DoorwaySpan> doorways)
    {
        int[][] layout = room.layout.Select(row => (int[])row.Clone()).ToArray();
        SealAllEdges(layout);
        CarveHub(layout);

        foreach (var doorway in doorways)
        {
            CarveDoorway(layout, doorway.Key, doorway.Value);
            CarveDoorwayApproach(layout, doorway.Key, doorway.Value);
        }

        foreach (var doorway in doorways)
            AddSideDoorwaySupport(layout, doorway.Key, doorway.Value, doorways);

        BuildSafeTraversalLane(layout, doorways);

        if (room.markerPos is { Length: >= 2 })
            EnsureMarkerPlatform(layout, room.markerPos);

        RemoveUnsupportedSpikes(layout);

        return layout;
    }

    private void BuildSafeTraversalLane(
        int[][] layout,
        Dictionary<Direction, DoorwaySpan> doorways)
    {
        if (doorways.Count == 0)
            return;

        int passageTop = Constants.ROOM_HEIGHT / 2 - 1;
        int passageBottom = passageTop + 1;
        int floorRow = passageBottom + 1;
        GetTraversalBounds(doorways, out int routeLeft, out int routeRight);
        CarveRectangle(layout, routeLeft, passageTop, routeRight, passageBottom);

        var bottomShaftColumns = new HashSet<int>();
        if (doorways.TryGetValue(Direction.Bottom, out DoorwaySpan bottomDoorway))
        {
            int shaftLeft = GetVerticalCorridorLeft(bottomDoorway);
            bottomShaftColumns.Add(shaftLeft);
            bottomShaftColumns.Add(shaftLeft + 1);
        }

        for (int x = routeLeft; x <= routeRight; x++)
        {
            if (!bottomShaftColumns.Contains(x))
                layout[floorRow][x] = Constants.DIRT;
        }
    }

    private void GetTraversalBounds(
        Dictionary<Direction, DoorwaySpan> doorways,
        out int routeLeft,
        out int routeRight)
    {
        routeLeft = Constants.ROOM_WIDTH / 2 - 2;
        routeRight = Constants.ROOM_WIDTH / 2 + 1;

        if (doorways.ContainsKey(Direction.Left))
            routeLeft = 1;
        if (doorways.ContainsKey(Direction.Right))
            routeRight = Constants.ROOM_WIDTH - 2;
    }

    private void EnsureMarkerPlatform(int[][] layout, int[] markerPosition)
    {
        int markerX = markerPosition[0];
        int markerY = markerPosition[1];
        int platformLeft = GetMarkerPlatformLeft(markerX);

        layout[markerY][platformLeft] = Constants.EMPTY;
        layout[markerY][platformLeft + 1] = Constants.EMPTY;
        layout[markerY + 1][platformLeft] = Constants.DIRT;
        layout[markerY + 1][platformLeft + 1] = Constants.DIRT;
    }

    private int GetMarkerPlatformLeft(int markerX)
        => Math.Clamp(markerX - 1, 1, Constants.ROOM_WIDTH - 3);

    private void RemoveUnsupportedSpikes(int[][] layout)
    {
        for (int y = 0; y < layout.Length; y++)
        {
            for (int x = 0; x < layout[y].Length; x++)
            {
                if (layout[y][x] != Constants.SPIKE)
                    continue;

                bool hasDirtSupport = y + 1 < layout.Length
                    && layout[y + 1][x] == Constants.DIRT;
                if (!hasDirtSupport)
                    layout[y][x] = Constants.EMPTY;
            }
        }
    }

    private void SealAllEdges(int[][] layout)
    {
        int height = layout.Length;
        int width = layout[0].Length;

        for (int x = 0; x < width; x++)
        {
            layout[0][x] = Constants.DIRT;
            layout[height - 1][x] = Constants.DIRT;
        }

        for (int y = 0; y < height; y++)
        {
            layout[y][0] = Constants.DIRT;
            layout[y][width - 1] = Constants.DIRT;
        }
    }

    private void CarveHub(int[][] layout)
    {
        int centerLeft = Constants.ROOM_WIDTH / 2 - 1;
        int centerTop = Constants.ROOM_HEIGHT / 2 - 1;

        for (int y = centerTop; y <= centerTop + 1; y++)
            for (int x = centerLeft; x <= centerLeft + 1; x++)
                layout[y][x] = Constants.EMPTY;
    }

    private void CarveDoorway(int[][] layout, Direction direction, DoorwaySpan span)
    {
        int height = layout.Length;
        int width = layout[0].Length;

        for (int offset = 0; offset < span.Length; offset++)
        {
            int position = span.Start + offset;
            switch (direction)
            {
                case Direction.Top:
                    layout[0][position] = Constants.EMPTY;
                    break;
                case Direction.Bottom:
                    layout[height - 1][position] = Constants.EMPTY;
                    break;
                case Direction.Left:
                    layout[position][0] = Constants.EMPTY;
                    break;
                case Direction.Right:
                    layout[position][width - 1] = Constants.EMPTY;
                    break;
            }
        }
    }

    private void CarveDoorwayApproach(int[][] layout, Direction direction, DoorwaySpan span)
    {
        int height = layout.Length;
        int width = layout[0].Length;
        int hubLeft = width / 2 - 1;
        int hubRight = hubLeft + 1;
        int hubTop = height / 2 - 1;
        int hubBottom = hubTop + 1;

        if (direction == Direction.Top || direction == Direction.Bottom)
        {
            int corridorLeft = GetVerticalCorridorLeft(span);
            int corridorRight = corridorLeft + 1;
            int fromY = direction == Direction.Top ? 0 : hubTop;
            int toY = direction == Direction.Top ? hubBottom : height - 1;

            CarveRectangle(layout, corridorLeft, fromY, corridorRight, toY);
            CarveRectangle(layout, Math.Min(corridorLeft, hubLeft), hubTop, Math.Max(corridorRight, hubRight), hubBottom);
        }
        else
        {
            int corridorTop = Constants.ROOM_HEIGHT / 2 - 1;
            int corridorBottom = corridorTop + 1;
            int fromX = direction == Direction.Left ? 0 : hubLeft;
            int toX = direction == Direction.Left ? hubRight : width - 1;

            CarveRectangle(layout, fromX, corridorTop, toX, corridorBottom);
            CarveRectangle(layout, hubLeft, Math.Min(corridorTop, hubTop), hubRight, Math.Max(corridorBottom, hubBottom));
        }
    }

    private void CarveRectangle(int[][] layout, int left, int top, int right, int bottom)
    {
        for (int y = Math.Max(0, top); y <= Math.Min(layout.Length - 1, bottom); y++)
            for (int x = Math.Max(0, left); x <= Math.Min(layout[y].Length - 1, right); x++)
                layout[y][x] = Constants.EMPTY;
    }

    private void AddSideDoorwaySupport(
        int[][] layout,
        Direction direction,
        DoorwaySpan span,
        Dictionary<Direction, DoorwaySpan> allDoorways)
    {
        if (direction != Direction.Left && direction != Direction.Right)
            return;

        int height = layout.Length;
        int width = layout[0].Length;
        int hubLeft = width / 2 - 1;
        int hubRight = hubLeft + 1;
        int corridorTop = Constants.ROOM_HEIGHT / 2 - 1;
        int supportRow = corridorTop + 2;

        if (supportRow >= height - 1)
            return;

        int supportLeft = direction == Direction.Left ? 1 : hubRight + 1;
        int supportRight = direction == Direction.Left ? hubLeft - 1 : width - 2;
        var reservedColumns = new HashSet<int>();

        foreach (var verticalDoorway in allDoorways)
        {
            if (verticalDoorway.Key != Direction.Top && verticalDoorway.Key != Direction.Bottom)
                continue;

            DoorwaySpan verticalSpan = verticalDoorway.Value;
            int corridorLeft = GetVerticalCorridorLeft(verticalSpan);
            reservedColumns.Add(corridorLeft);
            reservedColumns.Add(corridorLeft + 1);
        }

        for (int x = supportLeft; x <= supportRight; x++)
        {
            if (!reservedColumns.Contains(x))
                layout[supportRow][x] = Constants.DIRT;
        }
    }

    private int GetVerticalCorridorLeft(DoorwaySpan span)
        => Math.Clamp(span.Start + (span.Length - 2) / 2, 1, Constants.ROOM_WIDTH - 3);

    private bool ValidatePreparedRoom(
        RoomData room,
        int[][] layout,
        Dictionary<Direction, DoorwaySpan> doorways,
        out string failure)
    {
        foreach (Direction direction in Enum.GetValues<Direction>())
        {
            var actualOpenings = GetEdgeOpenings(layout, direction);
            if (!doorways.TryGetValue(direction, out DoorwaySpan expected))
            {
                if (actualOpenings.Count > 0)
                {
                    failure = $"undeclared {direction} opening";
                    return false;
                }
                continue;
            }

            if (actualOpenings.Count != expected.Length
                || actualOpenings[0] != expected.Start
                || actualOpenings[^1] != expected.End)
            {
                failure = $"{direction} opening is [{string.Join(",", actualOpenings)}], expected {expected}";
                return false;
            }
        }

        if (doorways.Count > 1 && !AreDoorwaysConnected(layout, doorways))
        {
            failure = "doorways are not connected through safe air";
            return false;
        }

        if (!HasValidTraversalLane(layout, doorways))
        {
            failure = "room does not have a supported two-tile-high traversal lane";
            return false;
        }

        if (room.markerPos is { Length: >= 2 }
            && !HasReachableMarkerPlatform(room, layout, doorways))
        {
            failure = "entrance/exit marker is not reachable on a two-tile platform";
            return false;
        }

        if (TryFindUnsupportedSpike(layout, out Vector2I unsupportedSpike))
        {
            failure = $"spike at {unsupportedSpike} has no dirt directly beneath it";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private bool HasValidTraversalLane(
        int[][] layout,
        Dictionary<Direction, DoorwaySpan> doorways)
    {
        if (doorways.Count == 0)
            return true;

        int passageTop = Constants.ROOM_HEIGHT / 2 - 1;
        int passageBottom = passageTop + 1;
        int floorRow = passageBottom + 1;
        GetTraversalBounds(doorways, out int routeLeft, out int routeRight);
        var bottomShaftColumns = new HashSet<int>();

        if (doorways.TryGetValue(Direction.Bottom, out DoorwaySpan bottomDoorway))
        {
            int shaftLeft = GetVerticalCorridorLeft(bottomDoorway);
            bottomShaftColumns.Add(shaftLeft);
            bottomShaftColumns.Add(shaftLeft + 1);
        }

        for (int x = routeLeft; x <= routeRight; x++)
        {
            if (!IsSafeAir(layout[passageTop][x]) || !IsSafeAir(layout[passageBottom][x]))
                return false;

            if (!bottomShaftColumns.Contains(x) && layout[floorRow][x] != Constants.DIRT)
                return false;
        }

        return true;
    }

    private bool HasReachableMarkerPlatform(
        RoomData room,
        int[][] layout,
        Dictionary<Direction, DoorwaySpan> doorways)
    {
        int markerX = room.markerPos[0];
        int markerY = room.markerPos[1];
        int platformLeft = GetMarkerPlatformLeft(markerX);
        Vector2I marker = new Vector2I(markerX, markerY);

        if (!IsSafeAir(layout[markerY][markerX])
            || !IsSafeAir(layout[markerY][platformLeft])
            || !IsSafeAir(layout[markerY][platformLeft + 1])
            || layout[markerY + 1][platformLeft] != Constants.DIRT
            || layout[markerY + 1][platformLeft + 1] != Constants.DIRT)
            return false;

        HashSet<Vector2I> reachable = FloodSafeAir(layout, marker);
        return doorways.All(pair => reachable.Contains(GetDoorwayCell(layout, pair.Key, pair.Value)));
    }

    private bool TryFindUnsupportedSpike(int[][] layout, out Vector2I position)
    {
        for (int y = 0; y < layout.Length; y++)
        {
            for (int x = 0; x < layout[y].Length; x++)
            {
                if (layout[y][x] == Constants.SPIKE
                    && (y + 1 >= layout.Length || layout[y + 1][x] != Constants.DIRT))
                {
                    position = new Vector2I(x, y);
                    return true;
                }
            }
        }

        position = default;
        return false;
    }

    private List<int> GetEdgeOpenings(int[][] layout, Direction direction)
    {
        var openings = new List<int>();
        int height = layout.Length;
        int width = layout[0].Length;

        int length = direction == Direction.Top || direction == Direction.Bottom ? width : height;
        for (int position = 0; position < length; position++)
        {
            int tile = direction switch
            {
                Direction.Top => layout[0][position],
                Direction.Bottom => layout[height - 1][position],
                Direction.Left => layout[position][0],
                Direction.Right => layout[position][width - 1],
                _ => Constants.DIRT
            };

            if (IsSafeAir(tile)) openings.Add(position);
        }

        return openings;
    }

    private bool AreDoorwaysConnected(int[][] layout, Dictionary<Direction, DoorwaySpan> doorways)
    {
        Vector2I start = GetDoorwayCell(layout, doorways.First().Key, doorways.First().Value);
        HashSet<Vector2I> reachable = FloodSafeAir(layout, start);

        return doorways.All(pair => reachable.Contains(GetDoorwayCell(layout, pair.Key, pair.Value)));
    }

    private Vector2I GetDoorwayCell(int[][] layout, Direction direction, DoorwaySpan span)
    {
        int middle = span.Start + span.Length / 2;
        return direction switch
        {
            Direction.Top => new Vector2I(middle, 0),
            Direction.Bottom => new Vector2I(middle, layout.Length - 1),
            Direction.Left => new Vector2I(0, middle),
            Direction.Right => new Vector2I(layout[0].Length - 1, middle),
            _ => Vector2I.Zero
        };
    }

    private HashSet<Vector2I> FloodSafeAir(int[][] layout, Vector2I start)
    {
        var reachable = new HashSet<Vector2I>();
        var pending = new Queue<Vector2I>();

        if (!IsInside(layout, start) || !IsSafeAir(layout[start.Y][start.X]))
            return reachable;

        reachable.Add(start);
        pending.Enqueue(start);

        Vector2I[] steps =
        {
            Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down
        };

        while (pending.Count > 0)
        {
            Vector2I current = pending.Dequeue();
            foreach (Vector2I step in steps)
            {
                Vector2I next = current + step;
                if (!IsInside(layout, next)
                    || !IsSafeAir(layout[next.Y][next.X])
                    || !reachable.Add(next))
                    continue;

                pending.Enqueue(next);
            }
        }

        return reachable;
    }

    private bool ValidateAssembledLevel(
        PlannedRoom[,] rooms,
        Vector2I entrance,
        Vector2I exit,
        out string failure)
    {
        int worldWidth = Constants.GRID_WIDTH * (Constants.ROOM_WIDTH - 1) + 1;
        int worldHeight = Constants.GRID_HEIGHT * (Constants.ROOM_HEIGHT - 1) + 1;
        int[][] world = new int[worldHeight][];
        bool[][] written = new bool[worldHeight][];

        for (int y = 0; y < worldHeight; y++)
        {
            world[y] = Enumerable.Repeat(Constants.DIRT, worldWidth).ToArray();
            written[y] = new bool[worldWidth];
        }

        for (int roomY = 0; roomY < Constants.GRID_HEIGHT; roomY++)
        {
            for (int roomX = 0; roomX < Constants.GRID_WIDTH; roomX++)
            {
                int offsetX = roomX * (Constants.ROOM_WIDTH - 1);
                int offsetY = roomY * (Constants.ROOM_HEIGHT - 1);
                int[][] layout = rooms[roomX, roomY].Layout;

                for (int y = 0; y < layout.Length; y++)
                {
                    for (int x = 0; x < layout[y].Length; x++)
                    {
                        int worldX = offsetX + x;
                        int worldY = offsetY + y;
                        if (written[worldY][worldX] && world[worldY][worldX] != layout[y][x])
                        {
                            failure = $"rooms disagree about shared tile ({worldX},{worldY})";
                            return false;
                        }

                        written[worldY][worldX] = true;
                        world[worldY][worldX] = layout[y][x];
                    }
                }
            }
        }

        Vector2I start = MarkerWorldTile(rooms[entrance.X, entrance.Y].Template, entrance);
        Vector2I goal = MarkerWorldTile(rooms[exit.X, exit.Y].Template, exit);
        HashSet<Vector2I> reachable = FloodSafeAir(world, start);

        if (!reachable.Contains(goal))
        {
            failure = $"spawn tile {start} cannot reach exit tile {goal} through safe air";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private bool IsInside(int[][] layout, Vector2I position)
    {
        return position.Y >= 0 && position.Y < layout.Length
            && position.X >= 0 && position.X < layout[position.Y].Length;
    }

    private bool IsSafeAir(int tile) => tile != Constants.DIRT && tile != Constants.SPIKE;

    private void CommitLevel(LevelPlan plan)
    {
        dirtLayer.Clear();
        spikeLayer.Clear();
        TilePlacer.ClearSpawnedPositions();

        entranceRoom = plan.Entrance;
        exitRoom = plan.Exit;
        selectedEntranceRoom = null;
        selectedExitRoom = null;

        for (int y = 0; y < Constants.GRID_HEIGHT; y++)
        {
            for (int x = 0; x < Constants.GRID_WIDTH; x++)
            {
                PlannedRoom planned = plan.Rooms[x, y];
                if (planned.IsEntrance) selectedEntranceRoom = planned.Template;
                if (planned.IsExit) selectedExitRoom = planned.Template;

                Vector2I offset = new Vector2I(
                    x * (Constants.ROOM_WIDTH - 1),
                    y * (Constants.ROOM_HEIGHT - 1));

                SpawnFlags allowedSpawns = planned.IsEntrance ? SpawnFlags.None : SpawnFlags.All;
                TilePlacer.PlaceRoom(planned.Layout, offset, dirtLayer, spikeLayer, rng, allowedSpawns);
                GD.Print($"Room ({x},{y}): {planned.Template.name}; openings {DescribeOpenings(planned.Layout)}");
            }
        }

        PlaceMarkers();
        SpawnPlayer();
    }

    private string DescribeOpenings(int[][] layout)
    {
        var descriptions = new List<string>();
        foreach (Direction direction in Enum.GetValues<Direction>())
        {
            List<int> openings = GetEdgeOpenings(layout, direction);
            if (openings.Count > 0)
                descriptions.Add($"{direction}:{openings[0]}..{openings[^1]}");
        }

        return descriptions.Count == 0 ? "none" : string.Join(" ", descriptions);
    }

    private Vector2I MarkerWorldTile(RoomData room, Vector2I gridPosition)
    {
        Vector2I local = room.markerPos is { Length: >= 2 }
            ? new Vector2I(room.markerPos[0], room.markerPos[1])
            : new Vector2I(Constants.ROOM_WIDTH / 2, 1);

        return new Vector2I(
            gridPosition.X * (Constants.ROOM_WIDTH - 1) + local.X,
            gridPosition.Y * (Constants.ROOM_HEIGHT - 1) + local.Y);
    }

    private void SpawnPlayer()
    {
        if (PlayerScene == null || selectedEntranceRoom == null) return;

        Node2D playerInstance = PlayerScene.Instantiate<Node2D>();
        Vector2I spawnTile = MarkerWorldTile(selectedEntranceRoom, entranceRoom);
        playerInstance.GlobalPosition = dirtLayer.MapToLocal(spawnTile) + dirtLayer.GlobalPosition;
        AddChild(playerInstance);
    }

    private void PlaceMarkers()
    {
        if (selectedEntranceRoom == null || selectedExitRoom == null)
        {
            GD.PrintErr("Missing entrance or exit room data.");
            return;
        }

        entranceSprite.Position = dirtLayer.MapToLocal(MarkerWorldTile(selectedEntranceRoom, entranceRoom)) + dirtLayer.Position;
        exitSprite.Position = dirtLayer.MapToLocal(MarkerWorldTile(selectedExitRoom, exitRoom)) + dirtLayer.Position;
    }

    private bool HasRequiredTemplateCoverage()
    {
        for (int mask = 0; mask < 16; mask++)
        {
            Direction[] required = Enum.GetValues<Direction>()
                .Where(direction => (mask & (1 << (int)direction)) != 0)
                .ToArray();

            if (!HasMatchingRoom(standardRooms, required))
            {
                GD.PrintErr($"Missing standard template for [{string.Join(",", required)}].");
                return false;
            }
        }

        Direction[][] entranceShapes =
        {
            new[] { Direction.Left },
            new[] { Direction.Right },
            new[] { Direction.Left, Direction.Right }
        };

        Direction[][] exitShapes =
        {
            new[] { Direction.Top },
            new[] { Direction.Top, Direction.Left },
            new[] { Direction.Top, Direction.Right },
            new[] { Direction.Top, Direction.Left, Direction.Right }
        };

        return entranceShapes.All(shape => HasMatchingRoom(entranceRooms, shape))
            && exitShapes.All(shape => HasMatchingRoom(exitRooms, shape));
    }

    private bool HasMatchingRoom(List<RoomData> rooms, Direction[] required)
        => rooms.Any(room => HasExactConnections(room, required));

    private RoomData GetRoomByConnections(List<RoomData> rooms, Direction[] required)
    {
        List<RoomData> matches = rooms.Where(room => HasExactConnections(room, required)).ToList();
        return matches.Count == 0 ? null : matches[rng.Next(matches.Count)];
    }

    private bool HasExactConnections(RoomData room, Direction[] required)
        => room.connections.Length == required.Length
            && required.All(connection => room.connections.Contains(connection));

    private Direction DirectionFromTo(Vector2I from, Vector2I to)
    {
        Vector2I delta = to - from;
        if (delta == Vector2I.Left) return Direction.Left;
        if (delta == Vector2I.Right) return Direction.Right;
        if (delta == Vector2I.Up) return Direction.Top;
        if (delta == Vector2I.Down) return Direction.Bottom;
        throw new ArgumentException($"Rooms {from} and {to} are not adjacent.");
    }

    private Direction Opposite(Direction direction)
    {
        return direction switch
        {
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            Direction.Top => Direction.Bottom,
            Direction.Bottom => Direction.Top,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
    }

    private void AddConnection(
        HashSet<Direction>[,] connections,
        int ax, int ay, int bx, int by,
        Direction aDirection, Direction bDirection)
    {
        connections[ax, ay].Add(aDirection);
        connections[bx, by].Add(bDirection);
    }
}
