using Godot;

public partial class Enemy : CharacterBody2D
{
    [Signal]
    public delegate void PlayerContactEventHandler(Player player, Vector2 sourcePosition);

    [Export] public int MaxHealth = 1;
    [Export] public int Damage = 1;
    [Export] public float LedgeCheckDistance = 8f;
    protected AnimatedSprite2D _animatedSprite;
    protected bool _isAttacking = false;
    protected int _currentHealth;
    protected int _direction = 1;

    public override void _Ready()
    {
        _currentHealth = MaxHealth;
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
    }
    public override void _PhysicsProcess(double delta)
    {
        MoveAndSlide();
        HandlePlayerCollision();
    }

    // =========================
    // DAMAGE SYSTEM
    // =========================
    public virtual void TakeDamage(int amount)
    {
        _currentHealth -= amount;

        GD.Print($"{Name} took {amount} damage. HP: {_currentHealth}");

        if (_currentHealth <= 0)
        {
            Die();
        }
    }
    protected virtual void Die()
    {
        GD.Print($"{Name} died.");

        // Later you can:
        // - Play animation
        // - Spawn particles
        // - Drop loot
        // - Disable collision first

        QueueFree();
    }
    // =========================
    // PLAYER CONTACT DAMAGE
    // =========================
    protected void HandlePlayerCollision()
    {
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);

            if (collision.GetCollider() is Player player)
            {
                player.TakeDamage(Damage, GlobalPosition);
                return;
            }
        }
    }
    public virtual void DealDamage(Player player)
    {
        player.TakeDamage(Damage, GlobalPosition);
    }

    // =========================
    // COLLISION HEIGHT HELPER
    // =========================
    public float GetCollisionHeight()
    {
        var shape = GetNode<CollisionShape2D>("CollisionShape2D").Shape as RectangleShape2D;
        return shape.Size.Y;
    }
    protected bool IsAtLedge(float forwardDistance = 8f)
    {
        Vector2 forward = new Vector2(Mathf.Sign(Velocity.X), 0);
        Vector2 rayStart = GlobalPosition + forward * forwardDistance;

        var space = GetWorld2D().DirectSpaceState;
        var query = PhysicsRayQueryParameters2D.Create(
            rayStart,
            rayStart + Vector2.Down * 16f
        );

        var result = space.IntersectRay(query);

        return result.Count == 0 && IsOnFloor();
    }


    protected virtual void TurnAround()
    {
        _direction *= -1;
        UpdateFacing();
    }

    protected virtual void UpdateFacing()
    {
        if (_animatedSprite != null)
        {
            GD.Print($"{Name} flipped.");
            _animatedSprite.FlipH = _direction > 0;
        }
    }
}

