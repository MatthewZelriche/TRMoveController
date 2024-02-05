using Godot;

public partial class MovingPlatform : AnimatableBody3D
{
    protected Vector3 velocity;

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        var res = MoveAndCollide(velocity * (float)delta, true, maxCollisions: 8);
        if (res != null)
        {
            for (int i = 0; i < res.GetCollisionCount(); i++)
            {
                var body = res.GetCollider(i);
                if (body is Damagable damagable)
                {
                    if (damagable.ShouldDamage((float)delta, velocity))
                    {
                        damagable.Kill();
                    }
                }

                // Stop when hitting static body
                // TODO: Consider not checking for this and just require maps to
                // specify an endpoint, ignoring collisions with staticbodies
                if (body is StaticBody3D)
                {
                    velocity = Vector3.Zero;
                }
            }
        }

        GlobalPosition += velocity * (float)delta;
    }
}
