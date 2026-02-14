using Godot;

public partial class Bat : Enemy
{
    [Export] public float FlySpeed = 120f;
    [Export] public float Acceleration = 500f;

    private Player _target;

    private bool _isActivated = false;

    public override void _Ready()
    {
        base._Ready();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (_isActivated && _target != null)
        {
            ChasePlayer(dt);
        }
        else
        {
            _animatedSprite.Play("Hang");
        }
        MoveAndSlide();
    }

    private void _on_detection_area_body_entered(Node body)
    {
        if (body is Player player)
        {
            GD.Print($"{Name} chasing the {player}.");
            _target = player;
            _isActivated = true;
            _animatedSprite.Play("Fly");
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
