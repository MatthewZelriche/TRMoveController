using Godot;

[GlobalClass]
public partial class PhysProp : RigidBody3D, Damagable
{
    [Export]
    float breakThreshold = 350.0f;
    Vector3 incomingForceThisFrame = Vector3.Zero;
    Vector3 oldVelocity = Vector3.Zero;
    Vector3 accel = Vector3.Zero;
    float unconvertedForce = 0;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() { }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        incomingForceThisFrame = Vector3.Zero;
        base._IntegrateForces(state);
        accel = (LinearVelocity - oldVelocity);
        Vector3 outgoingForce = accel * Mass;

        // Sum up incoming forces
        for (int i = 0; i < state.GetContactCount(); i++)
        {
            var contactBody = state.GetContactColliderObject(i);
            if (contactBody is AnimatableBody3D animBody)
            {
                incomingForceThisFrame += state.GetContactImpulse(i);
            }
            if (contactBody is RigidBody3D rigidBody)
            {
                incomingForceThisFrame += state.GetContactImpulse(i);
            }
        }

        // Record how much incoming force was not converted into outgoing force
        unconvertedForce = (
            (incomingForceThisFrame / state.Step) - (outgoingForce / state.Step)
        ).Length();

        oldVelocity = LinearVelocity;
    }

    public void Kill()
    {
        QueueFree();
    }

    public bool ShouldDamage(float delta, Vector3 moveDir)
    {
        return unconvertedForce > breakThreshold;
    }
}
