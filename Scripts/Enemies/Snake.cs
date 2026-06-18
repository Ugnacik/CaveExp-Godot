using Godot;

public partial class Snake : Enemy
{
    [Export] public float PatrolSpeed = 50f;

    /// <summary>
    /// Snakes require at least one open horizontal neighbor to patrol.
    /// Uses TestMove to respect actual collision shape size.
    /// </summary>
    protected override bool ValidateSpawnPosition()
    {
        bool wallLeft = TestMove(GlobalTransform, new Vector2(-2f, 0));
        bool wallRight = TestMove(GlobalTransform, new Vector2(2f, 0));
        return !(wallLeft && wallRight);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // Always apply gravity so trapped snakes don't float
        if (!IsOnFloor())
        {
            Velocity += new Vector2(0, 900f * dt);
        }
        else if (!_canPatrol)
        {
            // Stop horizontal movement but preserve vertical (gravity)
            Velocity = new Vector2(0, Velocity.Y);
        }
        else
        {
            Velocity = new Vector2(_direction * PatrolSpeed, Velocity.Y);
        }

        // Execute movement and player collision checks
        base._PhysicsProcess(delta);

        // Only handle patrol logic when we have space
        if (_canPatrol)
        {
            UpdateAnimation();

            if (IsOnWall())
                TurnAround();
            else if (IsOnFloor() && IsAtLedge())
                TurnAround();
        }
        else
        {
            // Idle visual state
            if (_animatedSprite.Animation != "Walk")
                _animatedSprite.Play("Walk");
            _animatedSprite.Pause();
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