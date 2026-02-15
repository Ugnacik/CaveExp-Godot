using Godot;

public partial class Bat : Enemy
{
    [Export] public float FlySpeed = 120f;
    [Export] public float Acceleration = 500f;
    private Area2D _detectionArea;
    private Player _target;

    private bool _isActivated = false;

    public override void _Ready()
    {
        base._Ready();
        _detectionArea = GetNode<Area2D>("DetectionArea");
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        float dt = (float)delta;

        if (_isActivated && _target != null)
        {
            ChasePlayer(dt);
        }
        else
        {
            _animatedSprite.Play("Hang");
        }
        
    }

    private void _on_detection_area_body_entered(Node body)
    {
        if (body is Player player)
        {
            GD.Print($"{Name} chasing the {player}.");
            _target = player;
            _isActivated = true;
            _animatedSprite.Play("Fly");

            // Disable detection area
            _detectionArea.SetDeferred("monitoring", false);
            _detectionArea.SetDeferred("monitorable", false);

        }
    }

    private void ChasePlayer(float dt)
    {
        Vector2 direction = (_target.GlobalPosition - GlobalPosition).Normalized();

        Velocity = Velocity.MoveToward(
            direction * FlySpeed,
            Acceleration * dt
        );

        // Face movement direction
        if (Velocity.X != 0)
            _animatedSprite.FlipH = Velocity.X > 0;
    }

}
