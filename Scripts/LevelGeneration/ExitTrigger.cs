using Godot;

public partial class ExitTrigger : Area2D
{
    private bool _hasTriggered = false;

    public override void _Ready()
    {
        // Connect signal in code for better organization than editor connections
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        // Prevent double-triggering during scene transition
        if (_hasTriggered) return;

        // Check group instead of hard type reference
        if (!body.IsInGroup("player")) return;

        _hasTriggered = true;

        // Disable monitoring immediately to prevent physics edge cases
        SetDeferred(Area2D.PropertyName.Monitoring, false);

        GD.Print("Exit reached! Reloading level...");

        // Safe reload - GetTree() is valid here because this node
        // is still active in the tree when the signal fires
        var tree = GetTree();
        if (tree != null)
        {
            tree.ReloadCurrentScene();
        }
    }
}
