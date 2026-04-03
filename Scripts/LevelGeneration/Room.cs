using Godot;
using System;

public partial class Room : Node2D
{
    [Export] public bool OpeningLeft = false;
    [Export] public bool OpeningRight = false;
    [Export] public bool OpeningTop = false;
    [Export] public bool OpeningBottom = false;
}
