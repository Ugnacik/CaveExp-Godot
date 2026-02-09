using Godot;

public partial class Enemy : CharacterBody2D
{
    [Export] public int Damage = 1;
    protected bool _isAttacking = false;

    // =========================
    // DAMAGE
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
}

