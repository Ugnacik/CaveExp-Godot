using Godot;

public partial class Player : CharacterBody2D
{
    [Signal]
    public delegate void HitEventHandler();

    [Export] public int Speed { get; set; } = 400;
    [Export] public float Gravity = 900f;
    [Export] public float JumpForce = -400f;
    [Export] public float JumpVelocity = -450f;

    private AnimatedSprite2D _animatedSprite;
    public Vector2 ScreenSize;

    public override void _Ready()
    {
        ScreenSize = GetViewportRect().Size;
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 velocity = Velocity;

        // 1️⃣ Apply gravity
        if (!IsOnFloor())
            velocity.Y += Gravity * (float)delta;
        else if (velocity.Y > 0)
            velocity.Y = 0;

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
        setAnimation(direction);
    }

    private void setAnimation(float direction)
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
