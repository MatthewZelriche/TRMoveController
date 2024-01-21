using Godot;
using Hsm;

public partial class TRMoveController : RigidBody3D
{
    class Air : StateWithOwner<TRMoveController>
    {
        bool enteredDuringDuck = false;

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
            // We went from ground to air while in duck state, we have to make sure to
            // preserve the duck state as a child
            // TODO: Toggle support
            enteredDuringDuck = Input.IsActionPressed("Duck");
        }

        public override Transition GetTransition()
        {
            if (
                Owner.GetTouchingZonesOfType(ZoneType.WaterZone) != null
                && Owner.ComputeWaterLevel() <= WaterLevel.Center
            )
            {
                return Transition.Sibling<Swim>();
            }

            if (Input.IsActionJustPressed("Duck") && !IsInInnerState<Crouched>())
            {
                // Skip transition, head straight into crouched state
                return Transition.Inner<Crouched>();
            }

            Transition defaultTransition = Transition.None();
            if (enteredDuringDuck)
            {
                enteredDuringDuck = false;
                defaultTransition = Transition.Inner<Crouched>();
            }

            return Owner.IsOnFloorAndSnap(-2.0f) ? Transition.Sibling<Ground>() : defaultTransition;
        }
    }

    class Ground : StateWithOwner<TRMoveController>
    {
        bool enteredDuringDuck = false;

        public override void Update(float step)
        {
            Owner.velocity = Owner.ComputeHorzVelocity(step);
            Owner.MoveAndSlide(step);
        }

        public override void OnEnter()
        {
            // We went from air to ground while in duck state, we have to make sure to
            // preserve the duck state as a child
            // TODO: Toggle support
            enteredDuringDuck = Input.IsActionPressed("Duck");

            // Ensure vertical velocity is zeroed when we enter the ground state
            Owner.velocity.Y = 0;
        }

        public override Transition GetTransition()
        {
            if (
                Owner.GetTouchingZonesOfType(ZoneType.WaterZone) != null
                && Owner.ComputeWaterLevel() <= WaterLevel.Center
            )
            {
                return Transition.Sibling<Swim>();
            }

            if (Input.IsActionJustPressed("TRJump") || !Owner.IsOnFloorAndSnap(-2.0f))
            {
                return Transition.Sibling<Air>();
            }
            if (Input.IsActionJustPressed("Duck") && !IsInInnerState<Crouched>())
            {
                return Transition.Inner<CrouchTransition>();
            }
            if (enteredDuringDuck)
            {
                enteredDuringDuck = false;
                return Transition.Inner<Crouched>();
            }

            return Transition.None();
        }
    }

    class Swim : StateWithOwner<TRMoveController>
    {
        public override void Update(float step)
        {
            // Pressing jump in the water overrides vertical velocity completely
            if (Input.IsActionPressed("TRJump"))
            {
                Owner.velocity.Y = 100.0f / Owner.scaleFactor;
            }
            if (Owner.CheckWaterJump())
            {
                // TODO: This is not accurate to jwchong, but I cannot get this working
                // with the correct value of 225.0f
                Owner.velocity.Y = 350.0f / Owner.scaleFactor;
            }
            Owner.velocity = Owner.ComputeWaterVelocity(step);
            Owner.MoveAndSlide(step);
        }

        public override Transition GetTransition()
        {
            if (
                Owner.GetTouchingZonesOfType(ZoneType.WaterZone) == null
                || Owner.ComputeWaterLevel() > WaterLevel.Center
            )
            {
                return !Owner.IsOnFloorAndSnap(-2.0f)
                    ? Transition.Sibling<Air>()
                    : Transition.Sibling<Ground>();
            }

            return Transition.None();
        }
    }

    // TODO: This is currently mostly just a placeholder to get uncrouching working
    class Walking : StateWithOwner<TRMoveController>
    {
        public override Transition GetTransition()
        {
            return Transition.None();
        }
    }

    class CrouchBase : StateWithOwner<TRMoveController>
    {
        public bool CanUnduck()
        {
            var result = Owner.TestMove(
                Owner.Transform,
                new Vector3(0, Owner.StandingHeight - Owner.CrouchHeight, 0)
            );
            return result == null;
        }
    }

    class CrouchTransition : CrouchBase
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

        public override void OnExit()
        {
            if (!Input.IsActionPressed("Duck"))
            {
                // Aborting duck transition early
                Owner.MoveAndCollide(new Vector3(0, Owner.CrouchHeight / 2, 0));
                Owner.SetPlayerHeight(Owner.StandingHeight);
            }

            // Adjust eyeheight back to standing
            Owner.playerCamera.Position = new Vector3(
                Owner.playerCamera.Position.X,
                Owner.GetFeetLocalPos() + Owner.EyeHeight,
                Owner.playerCamera.Position.Z
            );
        }

        public override Transition GetTransition()
        {
            if (!Input.IsActionPressed("Duck"))
            {
                // Cancelled duck early
                return Transition.Sibling<Walking>();
            }

            return finishedCrouch ? Transition.Sibling<Crouched>() : Transition.None();
        }
    }

    class Crouched : CrouchBase
    {
        public override void OnEnter()
        {
            Owner.SetPlayerHeight(Owner.CrouchHeight);
            // Do not attempt to snap to the ground if we crouch in the air
            if (Owner.movementStates.IsInState<Ground>())
            {
                float oldFeetY = Owner.GetFeetLocalPos() * Owner.scaleFactor;
                GD.Print(oldFeetY - ((Owner.standingHeight - Owner.crouchHeight) / 2));
                Owner.IsOnFloorAndSnap(
                    oldFeetY - ((Owner.standingHeight - Owner.crouchHeight) / 2)
                );
            }

            // After adjusting the AABB, we must update the eye height because
            // our feet local position will have changed.
            float newViewY = Owner.GetFeetLocalPos() + Owner.CrouchedEyeHeight;
            Vector3 newPos = Owner.playerCamera.Position;
            newPos.Y = newViewY;
            Owner.playerCamera.Position = newPos;
        }

        public override void OnExit()
        {
            if (!Input.IsActionPressed("Duck"))
            {
                // Exiting because player left duck, as opposed to an exit
                // due to transition from the main ground/air states
                Owner.MoveAndCollide(new Vector3(0, Owner.CrouchHeight / 2, 0));
                Owner.SetPlayerHeight(Owner.StandingHeight);
                Owner.playerCamera.Position = new Vector3(
                    Owner.playerCamera.Position.X,
                    Owner.GetFeetLocalPos() + Owner.EyeHeight,
                    Owner.playerCamera.Position.Z
                );
            }
        }

        public override Transition GetTransition()
        {
            return !Input.IsActionPressed("Duck") && CanUnduck()
                ? Transition.Sibling<Walking>()
                : Transition.None();
        }
    }
}
