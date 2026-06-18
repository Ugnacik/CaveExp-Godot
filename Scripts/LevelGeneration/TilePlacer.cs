using Godot;
using System;
using System.Collections.Generic;

public static class TilePlacer
{
    const int DIRT_SOURCE_ID = 11;
    const int SPIKE_SOURCE_ID = 0; // Already adjusted

    static readonly Vector2I[] DIRT_TILES = new Vector2I[]
    {
        new Vector2I(0, 0),
        new Vector2I(1, 0),
        new Vector2I(2, 0),
        new Vector2I(3, 0),
        new Vector2I(4, 0)
    };

    // Spawner registry: maps layout ID to a list of possible scenes
    public static Dictionary<int, List<PackedScene>> SpawnerPool { get; set; } = new();

    // Node container for spawned entities
    public static Node EntityContainer { get; set; }

    private static HashSet<Vector2> _occupiedSpawnPositions = new();

    public static void ClearSpawnedPositions() => _occupiedSpawnPositions.Clear();




    public static void PlaceRoom(
        int[][] layout,
        Vector2I offset,
        TileMapLayer dirtLayer,
        TileMapLayer spikeLayer,
        Random rng,
        SpawnFlags allowedSpawns = SpawnFlags.All) // Default: everything allowed
    {
        for (int y = 0; y < layout.Length; y++)
        {
            for (int x = 0; x < layout[y].Length; x++)
            {
                int tile = layout[y][x];
                Vector2I pos = new Vector2I(x + offset.X, y + offset.Y);

                switch (tile)
                {
                    case 0: // AIR
                        if (allowedSpawns.HasFlag(SpawnFlags.GroundEnemy)
                            && y < layout.Length - 1
                            && layout[y + 1][x] == 1
                            && GetLedgeWidth(layout, x, y) >= 3)
                        {
                            TrySpawnGroundEnemy(pos, rng);
                        }
                        break;

                    case 1: // DIRT
                        PlaceDirt(pos, dirtLayer, rng);

                        // Only spawn bats if the flag allows it
                        if (allowedSpawns.HasFlag(SpawnFlags.Bat)
                            && y < layout.Length - 1
                            && layout[y + 1][x] == 0)
                        {
                            TrySpawnBat(pos, rng);
                        }
                        break;

                    case 2: // SPIKE
                        PlaceSpike(pos, spikeLayer);
                        break;
                }
            }
        }
    }

    public static void PlaceDirt(Vector2I pos, TileMapLayer dirtLayer, Random rng)
    {
        var tile = DIRT_TILES[rng.Next(DIRT_TILES.Length)];
        dirtLayer.SetCell(pos, DIRT_SOURCE_ID, tile);
    }

    public static void PlaceSpike(Vector2I pos, TileMapLayer spikeLayer)
    {
        spikeLayer.SetCell(pos, SPIKE_SOURCE_ID, new Vector2I(0, 0));
    }

    private static void TrySpawnBat(Vector2I tilePos, Random rng)
    {
        // 5% chance per valid ceiling tile
        if (rng.NextDouble() > 0.05) return;
        TrySpawnEntity(3, tilePos, rng); // ID 3 = Bat
    }

    private static void TrySpawnGroundEnemy(Vector2I tilePos, Random rng)
    {
        // 5% chance per valid floor tile
        if (rng.NextDouble() > 0.05) return;
        TrySpawnEntity(4, tilePos, rng); // ID 4 = Ground Enemy
    }

    private static void TrySpawnEntity(int spawnerId, Vector2I tilePos, Random rng)
    {
        if (EntityContainer == null || !SpawnerPool.TryGetValue(spawnerId, out var pool) || pool.Count == 0)
            return;

        Vector2 basePos = new Vector2(tilePos.X * 16 + 8, tilePos.Y * 16 + 8);

        if (_occupiedSpawnPositions.Contains(basePos - new Vector2(16, 0))
         || _occupiedSpawnPositions.Contains(basePos)
         || _occupiedSpawnPositions.Contains(basePos + new Vector2(16, 0)))
            return;

        _occupiedSpawnPositions.Add(basePos);

        var scene = pool[rng.Next(pool.Count)];
        var instance = scene.Instantiate<Node2D>();
        instance.Position = basePos;
        EntityContainer.AddChild(instance);
    }
    private static int GetLedgeWidth(int[][] layout, int x, int y)
    {
        int width = 1;
        for (int lx = x - 1; lx >= 0 && layout[y][lx] == 0 && layout[y + 1][lx] == 1; lx--)
            width++;
        for (int rx = x + 1; rx < layout[y].Length && layout[y][rx] == 0 && layout[y + 1][rx] == 1; rx++)
            width++;
        return width;
    }
}
