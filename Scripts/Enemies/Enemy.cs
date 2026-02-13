using Godot;

public partial class Enemy : CharacterBody2D
{
    [Export] public int MaxHealth = 1;
    [Export] public int Damage = 1;
    [Export] public float LedgeCheckDistance = 8f;
    protected bool _isAttacking = false;
    protected int _currentHealth;
    protected int _direction = 1;

    public override void _Ready()
    {
        _currentHealth = MaxHealth;
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
    public virtual void DealDamage(Player player)
    {
        player.TakeDamage(Damage, GlobalPosition);
    }
    private void _on_hit_box_body_entered(Node body)
    {
        if (body is Player player)
        {
            DealDamage(player);
            _isAttacking |= true;
        }
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
        var sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (sprite != null)
            sprite.FlipH = _direction < 0;
    }
}

