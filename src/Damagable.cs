using Godot;

public interface Damagable
{
    public void Kill();
    public bool ShouldDamage(float delta, Vector3 moveDir);
}
