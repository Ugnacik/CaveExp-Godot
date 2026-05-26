using Godot;

public partial class Snake : Enemy
{
    [Export] public float PatrolSpeed = 50f;

    //Shape to be flipped

    public override void _Ready()
    {
        base._Ready();
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        float dt = (float)delta;

        // Apply gravity
        if (!IsOnFloor())
            Velocity += new Vector2(0, 900f * dt);

        // Horizontal movement
        Velocity = new Vector2(_direction * PatrolSpeed, Velocity.Y);
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


