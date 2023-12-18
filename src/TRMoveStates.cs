using Godot;
using Hsm;

public partial class TRMoveController : RigidBody3D
{
    class Air : StateWithOwner<TRMoveController>
    {
        public override void Update(float step)
        {
            // Leapfrog integration of gravity force.
            Owner.ApplyHalfGravity(step);
            // TODO: Compute horizontal velocity
            Owner.MoveAndCollide(Owner.velocity * step);
            Owner.ApplyHalfGravity(step);
        }

        public override Transition GetTransition()
        {
            return Transition.None();
        }
    }

    class Ground : StateWithOwner<TRMoveController> { }

    class Swim : StateWithOwner<TRMoveController> { }
}
