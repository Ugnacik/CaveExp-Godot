using Godot;

public partial class Player : CharacterBody2D
{
    [Signal]
    public delegate void HitEventHandler();

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
}
