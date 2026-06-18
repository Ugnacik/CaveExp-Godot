using Godot;

public partial class Enemy : CharacterBody2D
{
    [Signal] public delegate void PlayerContactEventHandler(Player player, Vector2 sourcePosition);

    [Export] public int MaxHealth = 1;
    [Export] public int Damage = 1;
    [Export] public float LedgeCheckDistance = 8f;

    protected AnimatedSprite2D _animatedSprite;
    protected bool _isAttacking = false;
    protected int _currentHealth;
    protected int _direction = 1;

    // New: Tracks if the enemy has valid patrol space
    protected bool _canPatrol = true;

    public override void _Ready()
    {
        _currentHealth = MaxHealth;
        _animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");

        // Validate spawn position once at initialization
        _canPatrol = ValidateSpawnPosition();

        if (!_canPatrol)
        {
            Velocity = Vector2.Zero;
            GD.Print($"{Name} spawned in invalid patrol space. Entering idle state.");
        }
    }

    /// <summary>
    /// Virtual method to determine if an enemy has valid movement space at spawn.
    /// Override in ground enemies to check for walls/traps.
    /// Returns true by default (for flying/static enemies).
    /// </summary>
    protected virtual bool ValidateSpawnPosition() => true;

    public override void _PhysicsProcess(double delta)
    {
        MoveAndSlide();
        HandlePlayerCollision();
        HandleEnemyCollision();
    }

    protected void HandleEnemyCollision()
    {
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);

            if (collision.GetCollider() is Enemy otherEnemy && otherEnemy != this)
            {
                // Get the normal of the collision (the direction pointing away from the object we hit)
                Vector2 normal = collision.GetNormal();

                // If we are moving toward the object we hit (dot product > 0), turn around
                // We use a small threshold (0.1f) to avoid floating point errors
                if (Velocity.Normalized().Dot(normal) > 0.1f)
                {
                    TurnAround();
                    return;
                }
            }
        }
    }

    // =========================
    // DAMAGE SYSTEM
    // =========================
    public virtual void TakeDamage(int amount)
    {
        _currentHealth -= amount;
        GD.Print($"{Name} took {amount} damage. HP: {_currentHealth}");

        if (_currentHealth <= 0) Die();
    }

    protected virtual void Die()
    {
        GD.Print($"{Name} died.");
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
    // MOVEMENT HELPERS
    // =========================
    public float GetCollisionHeight()
    {
        var shape = GetNode<CollisionShape2D>("CollisionShape2D").Shape as RectangleShape2D;
        return shape?.Size.Y ?? 16f;
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
            _animatedSprite.FlipH = _direction > 0;
        }
    }
}