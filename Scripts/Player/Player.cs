using Godot;

public partial class Player : CharacterBody2D
{
    [Signal]
    public delegate void HitEventHandler();

    //Health
    [Export] public int MaxHealth = 4;
    private int _currentHealth;
    private bool _isInvulnerable = false;
    private float _invulTime = 0.6f;
    private float _invulTimer = 0f;

    //KnockBack
    [Export] public float KnockbackForceX = 250f;
    [Export] public float KnockbackForceY = -300f;

    //Movement
    [Export] public int Speed { get; set; } = 400;
    [Export] public float JumpForce = -400f;
    [Export] public float JumpVelocity = -450f;
    [Export] public float FallMultiplier = 1.8f;
    [Export] public float LowJumpMultiplier = 2.5f;
    private float _gravity;


    private AnimatedSprite2D _animatedSprite;
    public Vector2 ScreenSize;

    public override void _Ready()
    {
        ScreenSize = GetViewportRect().Size;
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        _gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
        _currentHealth = MaxHealth;
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 velocity = Velocity;

        // Apply gravity
        if (!IsOnFloor())
        {
            velocity.Y += _gravity * (float)delta;

            // Faster fall
            if (velocity.Y > 0)
            {
                velocity.Y += _gravity * (FallMultiplier - 1) * (float)delta;
            }
            // Short jump if released early
            else if (velocity.Y < 0 && !Input.IsActionPressed("player_jump"))
            {
                velocity.Y += _gravity * (LowJumpMultiplier - 1) * (float)delta;
            }
        }

        // 2️⃣ Horizontal movement
        float direction = Input.GetAxis("player_left", "player_right");
        velocity.X = direction * Speed;

        // 3️⃣ Jump
        if (IsOnFloor() && Input.IsActionJustPressed("player_jump"))
        {
            velocity.Y = JumpForce;
        }

        Velocity = velocity;
        MoveAndSlide();

        // 4️⃣ Animation
        SetAnimation(direction);

        if (_isInvulnerable)
        {
            _invulTimer -= (float)delta;

            if (_invulTimer <= 0)
            {
                _isInvulnerable = false;
            }
        }

    }

    public void TakeDamage(int amount, Vector2 sourcePosition)
    {
        if (_isInvulnerable)
            return;

        _currentHealth -= amount;

        GD.Print("Player HP: " + _currentHealth);

        if (_currentHealth <= 0)
        {
            Die();
            return;
        }

        ApplyKnockback(sourcePosition);

        _isInvulnerable = true;
        _invulTimer = _invulTime;
    }

    private void ApplyKnockback(Vector2 sourcePosition)
    {
        float direction = GlobalPosition.X < sourcePosition.X ? -1 : 1;

        Velocity = new Vector2(
            direction * KnockbackForceX,
            KnockbackForceY
        );
    }

    private void Die()
    {
        GD.Print("Player Died");

        //QueueFree(); // or respawn logic later
        GetTree().ReloadCurrentScene();
    }

    private void SetAnimation(float direction)
    {
        if (direction != 0)
        {
            _animatedSprite.Play("Player_Run");
            _animatedSprite.FlipH = direction < 0;
        }
        else
        {
            _animatedSprite.Play("Player_Idle");
        }
    }

    //For layer 3 tiles
    private void _on_hit_box_body_entered(Node body)
    {
        if (body is PhysicsBody2D physicsBody)
        {
            // Layer 3 (spikes) = 1 << 2 = 4
            if ((physicsBody.CollisionLayer & (1 << 2)) != 0)
            {
                Die();
            }
            // Example: Layer 4 (enemies) = 1 << 3 = 8
            else if ((physicsBody.CollisionLayer & (1 << 3)) != 0)
            {
                TakeDamage(1, physicsBody.GlobalPosition);
            }
        }
    }


}
