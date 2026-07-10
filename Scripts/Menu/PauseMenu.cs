using Godot;
using System;

public partial class PauseMenu : CanvasLayer
{
    private Button _resumeButton;
    private Button _quitButton;

    public override void _Ready()
    {
        // Ensure it starts hidden
        Visible = false;

        // Fetch buttons directly as children of this node
        _resumeButton = GetNodeOrNull<Button>("ResumeButton");
        _quitButton = GetNodeOrNull<Button>("QuitButton");

        // Connect signals safely
        if (_resumeButton != null) _resumeButton.Pressed += ResumeGame;
        if (_quitButton != null) _quitButton.Pressed += QuitToMenu;
    }

    // Godot calls this for input events. We use the built-in "ui_cancel" (Escape key)
    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            TogglePause();
            // Tell Godot we handled this input so it doesn't pass it to the player
            GetViewport().SetInputAsHandled();
        }
    }

    private void TogglePause()
    {
        var tree = GetTree();
        tree.Paused = !tree.Paused; // Toggles the global pause state

        // Show or hide the UI based on the pause state
        Visible = tree.Paused;
    }

    private void ResumeGame()
    {
        GetTree().Paused = false;
        Visible = false;
    }

    private void QuitToMenu()
    {
        // Unpause the game first so the next scene loads normally
        GetTree().Paused = false;

        GetTree().ChangeSceneToFile("res://Scenes/main_menu.tscn");
    }
}
