using Godot;
using System;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        GetNode<Button>("PlayButton").Pressed += OnPlayPressed;

        GetNode<Button>("QuitButton").Pressed += OnQuitPressed;
    }

    private void OnPlayPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/main.tscn");
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }
}
