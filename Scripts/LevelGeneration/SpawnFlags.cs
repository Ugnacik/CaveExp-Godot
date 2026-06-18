using System;

[Flags]
public enum SpawnFlags
{
    None        = 0,
    Bat         = 1 << 0, // 1
    GroundEnemy = 1 << 1, // 2
    
    // Add new enemy types here as you expand the game:
    // FlyingEnemy = 1 << 2, // 4
    // Trap        = 1 << 3, // 8
    
    All = Bat | GroundEnemy
}