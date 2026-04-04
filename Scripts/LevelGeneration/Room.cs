using Godot;
using System;

public partial class Room : Node2D
{
    [Export] public bool OpeningLeft = false;
    [Export] public bool OpeningRight = false;
    [Export] public bool OpeningTop = false;
    [Export] public bool OpeningBottom = false;

    public override void _Draw()
    {
        Color color = Colors.White;

        if (OpeningLeft) color = Colors.Red;
        if (OpeningRight) color = Colors.Green;
        if (OpeningTop) color = Colors.Blue;
        if (OpeningBottom) color = Colors.Yellow;

        DrawRect(new Rect2(Vector2.Zero, new Vector2(192, 192)), color, false);
    }
}
