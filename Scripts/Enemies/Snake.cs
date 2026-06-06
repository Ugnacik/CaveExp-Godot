using Godot;

public partial class Snake : Enemy
{
    [Export] public float PatrolSpeed = 50f;

    public override void _Ready()
    {
        base._Ready();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // 1. Apply gravity
        if (!IsOnFloor())
            Velocity += new Vector2(0, 900f * dt);

        // 2. Set horizontal movement based on current direction
        Velocity = new Vector2(_direction * PatrolSpeed, Velocity.Y);

        // 3. Call base to execute MoveAndSlide() and HandlePlayerCollision()
        // This ensures the snake moves with the newly calculated Velocity
        base._PhysicsProcess(delta);

        // 4. Update visuals
        UpdateAnimation();

        // 5. Handle turning logic AFTER moving and resolving collisions
        if (IsOnWall())
        {
            TurnAround();
        }
        // Use 'else if' to prevent double-flipping if the snake gets stuck in a corner
        // (where it touches both a wall and a ledge at the same time)
        else if (IsOnFloor() && IsAtLedge())
        {
            TurnAround();
        }
    }

    private void UpdateAnimation()
    {
        if (_isAttacking)
        {
            if (_animatedSprite.Animation != "Attack")
                _animatedSprite.Play("Attack");
        }
        else
        {
            _animatedSprite.Play("Walk");
        }
    }
}
