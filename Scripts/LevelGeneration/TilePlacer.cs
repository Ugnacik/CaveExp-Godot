using Godot;
using System;

public static class TilePlacer
{
    const int DIRT_SOURCE_ID = 11;
    static readonly Vector2I[] DIRT_TILES = new Vector2I[]
    {
        new Vector2I(0, 0),
        new Vector2I(1, 0),
        new Vector2I(2, 0),
        new Vector2I(3, 0),
        new Vector2I(4, 0)
    };

    public static void PlaceRoom(int[][] layout, Vector2I offset, TileMapLayer layer, Random rng)
    {
        for (int y = 0; y < layout.Length; y++)
        {
            for (int x = 0; x < layout[y].Length; x++)
            {
                int tile = layout[y][x];
                Vector2I pos = new Vector2I(x + offset.X, y + offset.Y);

                switch (tile)
                {
                    case 1: // DIRT
                        PlaceDirt(pos, layer, rng);
                        break;

                    case 2: // SPIKE
                        // PlaceSpike(...)
                        break;

                    case 3: // ENEMY
                        // SpawnEnemy(...)
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
        GD.Print($"Placing spike at {pos}");
    }

    public static void SpawnEnemy(Vector2I pos)
    {
        GD.Print($"Spawning enemy at {pos}");
    }
}