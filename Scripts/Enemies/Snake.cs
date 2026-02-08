using Godot;

public partial class Snake : Enemy
{
    [Export] public float PatrolSpeed = 50f;
    [Export] public float LedgeCheckDistance = 8f;

    private int _direction = 1;
    private AnimatedSprite2D _animatedSprite;

    public override void _Ready()
    {
        base._Ready();
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // Apply gravity
        if (!IsOnFloor())
            Velocity += new Vector2(0, 900f * dt);

        // Horizontal movement
        Velocity = new Vector2(_direction * PatrolSpeed, Velocity.Y);

        MoveAndSlide();
        UpdateAnimation();

        // Turn around if wall
        if (IsOnWall())
        {
            TurnAround();
        }

        // Turn around if ledge
        if (IsOnFloor() && IsAtLedge())
        {
            TurnAround();
        }
    }
    private void UpdateAnimation()
    {
        if (!IsOnFloor())
        {
            _animatedSprite.Play("Idle");
            return;
        }

        if (Mathf.Abs(Velocity.X) > 1f)
        {
            _animatedSprite.Play("Move");
        }
        else
        {
            _animatedSprite.Play("Idle");
        }
    }

    private void TurnAround()
    {
        _direction *= -1;
        _animatedSprite.FlipH = _direction < 0;
    }

    private bool IsAtLedge()
    {
        Vector2 forward = new Vector2(_direction * LedgeCheckDistance, 0);
        Vector2 rayStart = GlobalPosition + forward;

        var space = GetWorld2D().DirectSpaceState;

        var query = PhysicsRayQueryParameters2D.Create(
            rayStart,
            rayStart + Vector2.Down * 16f
        );

        var result = space.IntersectRay(query);

        return result.Count == 0;
    }
}


