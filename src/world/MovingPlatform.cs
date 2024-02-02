using Godot;

public partial class MovingPlatform : AnimatableBody3D
{
    // Called when the node enters the scene tree for the first time.
    public override void _Ready() { }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) { }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        var collision = MoveAndCollide(new Vector3(0, 2, 0) * (float)delta, true);
        if (collision != null)
        {
            // Blocked...
            var body = (PhysicsBody3D)collision.GetCollider();
            if (body is Damagable)
            {
                var nextCollision = body.MoveAndCollide(new Vector3(0, 2, 0) * (float)delta, true);
                if (nextCollision != null)
                {
                    ((Damagable)body).Kill();
                }
            }
            else
            {
                return;
            }
        }

        GlobalPosition += new Vector3(0, 2, 0) * (float)delta;
    }
}
