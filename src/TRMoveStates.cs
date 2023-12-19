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
            // Treat the velocity as strictly horizontal
            float savedVertVelocity = Owner.velocity.Y;
            Owner.velocity.Y = 0;
            Owner.velocity = Owner.ComputeHorzVelocity(step);
            Owner.velocity.Y = savedVertVelocity;
            Owner.MoveAndSlide(step);
            Owner.ApplyHalfGravity(step);
        }

        public override void OnExit()
        {
            // Don't forget to zero out our vertical velocity once we are no longer
            // in the air
            Owner.velocity.Y = 0;
        }

        public override Transition GetTransition()
        {
            return Owner.IsOnFloor() ? Transition.Sibling<Ground>() : Transition.None();
        }
    }

    class Ground : StateWithOwner<TRMoveController>
    {
        public override void Update(float step)
        {
            Owner.velocity = Owner.ComputeHorzVelocity(step);
            Owner.MoveAndSlide(step);
        }

        public override Transition GetTransition()
        {
            // TODO
            return Transition.None();
        }
    }

    class Swim : StateWithOwner<TRMoveController> { }
}
