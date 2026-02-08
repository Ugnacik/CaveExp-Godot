using Godot;

public partial class Enemy : CharacterBody2D
{
    [Export] public int Damage = 1;

    public void DealDamage(Player player)
    {
        player.TakeDamage(Damage, GlobalPosition);
    }
}

