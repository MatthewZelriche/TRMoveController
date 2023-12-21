using System.Buffers;
using System.Diagnostics;
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

        public override void OnEnter()
        {
            if (Input.IsActionJustPressed("TRJump"))
            {
                Owner.velocity.Y = Owner.JumpForce;
            }
        }

        public override void OnExit()
        {
            // Don't forget to zero out our vertical velocity once we are no longer
            // in the air
            Owner.velocity.Y = 0;
        }

        public override Transition GetTransition()
        {
            return Owner.IsOnFloorAndSnap(-2.0f) ? Transition.Sibling<Ground>() : Transition.None();
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
            if (Input.IsActionJustPressed("TRJump") || !Owner.IsOnFloorAndSnap(-2.0f))
            {
                return Transition.Sibling<Air>();
            }
            if (Input.IsActionJustPressed("Duck"))
            {
                return Transition.Inner<CrouchTransition>();
            }

            return Transition.None();
        }
    }

    class Swim : StateWithOwner<TRMoveController> { }

    class CrouchTransition : StateWithOwner<TRMoveController>
    {
        private float startTime = 0.4f;
        private float startLocalY;
        private float endLocalY;
        private bool finishedCrouch = false;

        private SceneTreeTimer countDown;

        public override void OnEnter()
        {
            startLocalY = Owner.GetFeetLocalPos() + Owner.EyeHeight;
            endLocalY = Owner.GetFeetLocalPos() + Owner.CrouchedEyeHeight;
            countDown = Owner.GetTree().CreateTimer(startTime);
            countDown.Timeout += () =>
            {
                finishedCrouch = true;
            };
        }

        public override void Update(float step)
        {
            double amtComplete = countDown.TimeLeft / startTime;

            // Smooth lerp player eye height
            float newViewY = (float)Mathf.Lerp(endLocalY, startLocalY, amtComplete);

            Vector3 newPos = Owner.playerCamera.Position;
            newPos.Y = newViewY;
            Owner.playerCamera.Position = newPos;
        }

        public override Transition GetTransition()
        {
            return finishedCrouch ? Transition.Sibling<Crouched>() : Transition.None();
        }
    }

    class Crouched : StateWithOwner<TRMoveController>
    {
        public override void OnEnter()
        {
            float oldFeetY = Owner.GetFeetLocalPos() * Owner.scaleFactor;
            Owner.SetPlayerHeight(Owner.CrouchHeight);
            GD.Print(oldFeetY - ((Owner.standingHeight - Owner.crouchHeight) / 2));
            Debug.Assert(
                Owner.IsOnFloorAndSnap(
                    oldFeetY - ((Owner.standingHeight - Owner.crouchHeight) / 2)
                ),
                "Should be impossible?"
            );

            // After adjusting the AABB, we must update the eye height because
            // our feet local position will have changed.
            float newViewY = Owner.GetFeetLocalPos() + Owner.CrouchedEyeHeight;
            Vector3 newPos = Owner.playerCamera.Position;
            newPos.Y = newViewY;
            Owner.playerCamera.Position = newPos;
        }

        public override Transition GetTransition()
        {
            return Transition.None();
        }
    }
}
