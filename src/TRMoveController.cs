using System.Diagnostics;
using Godot;
using Hsm;

// Possible improvements:
// 1. Eliminate Ducktapping bug
// 2. Smooth uncrouch
// 3. Snap on step down
// 4. Toggle crouch button

[GlobalClass]
public partial class TRMoveController : RigidBody3D
{
    [Export]
    private TRCamera playerCamera = null;

    [Export]
    private CollisionShape3D collider = null;

    [ExportCategory("Dimensions")]
    [Export(PropertyHint.Range, "1,128,1,or_greater")]
    // Values exposed to the editor are in Trenchbroom/Hammer units for ease of
    // tweaking. As such, a unit conversion needs to occur in order to map Trenchbroom
    // units to Godot units. If you are using Qodot, set this to the same scale factor
    // you use in Qodot.
    private float scaleFactor = 16.0f;

    [Export]
    private float standingHeight = 72.0f;

    [Export]
    private float crouchHeight = 36.0f;

    [Export]
    private float width = 32.0f;

    [Export]
    private float eyeHeight = 64.0f;

    [Export]
    private float crouchedEyeHeight = 28.0f;

    [ExportCategory("Collision")]
    [Export]
    // Corresponds to the cvar 'sv_bounce' in goldsrc
    private float bounce = 1.0f;

    [ExportCategory("Basic Movement")]
    [Export(PropertyHint.Range, "0,600,5,or_greater")]
    // Corresponds to the cvar 'cl_forwardspeed' in goldsrc
    // Note that this value (as well as sideSpeed and upSpeed) can be misleading,
    // as they are "client-side" maximum speeds. Regardless of what you set these speeds
    // to, they cannot cause you to exceed the maxSpeed (sv_maxspeed) value
    private int forwardSpeed = 400;

    [Export(PropertyHint.Range, "0,600,5,or_greater")]
    // Corresponds to the cvar 'cl_sideSpeed' in goldsrc
    private int sideSpeed = 400;

    [Export(PropertyHint.Range, "0,450,5,or_greater")]
    // Corresponds to the cvar 'sv_maxspeed' in goldsrc
    private float maxSpeed = 320.0f;

    [Export(PropertyHint.Range, "0.1,6.0,0.1")]
    // Corresponds to the cvar 'sv_friction' in goldsrc
    private float friction = 4.0f;

    [Export(PropertyHint.Range, "0.1,6.0,0.1")]
    // Corresponds to the cvar 'edgefriction' in goldsrc
    private float edgeFriction = 2.0f;

    [Export(PropertyHint.Range, "10.0,300.0,1")]
    // Corresponds to the cvar 'sv_stopspeed' in goldsrc
    private float stopSpeed = 100.0f;

    [Export(PropertyHint.Range, "0.0, 100.0, 2.0")]
    // Corresponds to the cvar 'sv_accelerate' in goldsrc
    private float accelerate = 10.0f;

    [Export(PropertyHint.Range, "0.0, 100.0, 2.0")]
    // Corresponds to the cvar 'sv_airaccelerate' in goldsrc
    private float airAccelerate = 10.0f;

    [ExportCategory("Climbing")]
    [Export]
    // Corresponds to the cvar 'sv_stepsize' in goldsrc
    private float maxStepHeight = 18.0f;

    [ExportCategory("Gravity & Falling")]
    [Export(PropertyHint.Range, "0,1200,20,or_greater")]
    // Corresponds to the cvar 'sv_gravity' in goldsrc
    private float gravity = 800.0f;

    private float jumpForce = 268.3f;

    public float StandingHeight
    {
        get { return standingHeight / scaleFactor; }
        set { standingHeight = value * scaleFactor; }
    }

    public float CrouchHeight
    {
        get { return crouchHeight / scaleFactor; }
        set { crouchHeight = value * scaleFactor; }
    }

    public float Width
    {
        get { return width / scaleFactor; }
        set { width = value * scaleFactor; }
    }

    public float EyeHeight
    {
        get { return eyeHeight / scaleFactor; }
        set { eyeHeight = value * scaleFactor; }
    }

    public float CrouchedEyeHeight
    {
        get { return crouchedEyeHeight / scaleFactor; }
        set { crouchedEyeHeight = value * scaleFactor; }
    }

    public int ForwardSpeed
    {
        get { return forwardSpeed / (int)scaleFactor; }
        set { forwardSpeed = value * (int)scaleFactor; }
    }

    public int SideSpeed
    {
        get { return sideSpeed / (int)scaleFactor; }
        set { sideSpeed = value * (int)scaleFactor; }
    }

    public float MaxSpeed
    {
        get { return maxSpeed / scaleFactor; }
        set { maxSpeed = value * scaleFactor; }
    }

    public float StopSpeed
    {
        get { return stopSpeed / scaleFactor; }
        set { stopSpeed = value * scaleFactor; }
    }

    public float Gravity
    {
        get { return gravity / scaleFactor; }
        set { gravity = value * scaleFactor; }
    }

    public float JumpForce
    {
        get { return jumpForce / scaleFactor; }
        set { jumpForce = value * scaleFactor; }
    }

    public float MaxStepHeight
    {
        get { return maxStepHeight / scaleFactor; }
        set { maxStepHeight = value * scaleFactor; }
    }

    private StateMachine movementStates = new StateMachine();
    private Vector3 velocity;
    private float entityFriction = 1.0f;
    float maxFloorAngleValue = 0.7f;
    private ShapeCast3D stepCollider = new ShapeCast3D();

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Debug.Assert(
            playerCamera != null,
            "You forgot to assign a Camera3D to the TRMoveController!"
        );
        Debug.Assert(
            collider != null,
            "You forgot to assign a CollisionShape3D to the TRMoveController!"
        );

        // Set dimensions
        stepCollider.Shape = new BoxShape3D();
        AddChild(stepCollider);
        SetPlayerHeight(StandingHeight);
        playerCamera.Position = new Vector3(
            playerCamera.Position.X,
            GetFeetLocalPos() + EyeHeight,
            playerCamera.Position.Y
        );

        movementStates.Init<Air>(this);
    }

    public float GetFeetLocalPos()
    {
        float height = movementStates.IsInState<Crouched>() ? CrouchHeight : StandingHeight;

        return -(height / 2);
    }

    public void SetPlayerHeight(float height)
    {
        ((BoxShape3D)collider.Shape).Size = new Vector3(Width, height, Width);
        ((BoxShape3D)stepCollider.Shape).Size = new Vector3(Width, height, Width);
    }

    public override void _PhysicsProcess(double _step)
    {
        float step = (float)_step;
        // Processing states second ensures FSU values are modified on the next frame,
        // as intended
        movementStates.UpdateStates(step);
        movementStates.ProcessStateTransitions();
    }

    private Vector3 ComputeHorzVelocity(float step)
    {
        bool airborne = movementStates.IsInState<Air>();

        Vector3 FSU = ComputeFSU();
        Vector3 accelVector = (
            (
                FSU.X * playerCamera.UnitRightVector()
                + (FSU.Z * playerCamera.UnitForwardHorzVector())
            )
        );

        Vector3 velAfterGroundFriction = airborne
            ? velocity
            : ComputeGroundFriction(velocity, step);

        float clampedSpeed = Mathf.Min(MaxSpeed, accelVector.Length());
        float accelValue = airborne ? airAccelerate : accelerate;
        float gamma1 = entityFriction * step * clampedSpeed * accelValue;
        float airSpeedCap = airborne ? Mathf.Min(30 / scaleFactor, clampedSpeed) : clampedSpeed;
        float gamma2 = airSpeedCap - velAfterGroundFriction.Dot(accelVector.Normalized());
        float muCoefficient = gamma2 > 0 ? Mathf.Min(gamma1, gamma2) : 0;

        return velAfterGroundFriction + (muCoefficient * accelVector.Normalized());
    }

    private Vector3 ComputeGroundFriction(Vector3 horzVel, float step)
    {
        // TODO: Check EdgeFriction
        float frictionCoefficient = friction * entityFriction * 1.0f;
        float smallSpeed = Mathf.Max(0.1f / scaleFactor, step * StopSpeed * frictionCoefficient);

        if (horzVel.Length() >= StopSpeed)
        {
            return (1 - (frictionCoefficient * step)) * horzVel;
        }
        else if (horzVel.Length() >= smallSpeed)
        {
            return horzVel - (step * StopSpeed * frictionCoefficient * horzVel.Normalized());
        }
        else
        {
            return Vector3.Zero;
        }
    }

    private Vector3 ComputeFSU()
    {
        Vector3 FSU = new Vector3();
        if (Input.IsActionPressed("TRForward"))
        {
            FSU.Z += ForwardSpeed;
        }
        if (Input.IsActionPressed("TRBack"))
        {
            FSU.Z -= ForwardSpeed;
        }
        if (Input.IsActionPressed("TRLeft"))
        {
            FSU.X -= SideSpeed;
        }
        if (Input.IsActionPressed("TRRight"))
        {
            FSU.X += SideSpeed;
        }

        // TODO: Upmove

        // Truncation to integer via Export Hints
        // Must also clamp to -2047, 2047
        Mathf.Clamp(FSU.X, -2047 / scaleFactor, 2047 / scaleFactor);
        Mathf.Clamp(FSU.Y, -2047 / scaleFactor, 2047 / scaleFactor);
        Mathf.Clamp(FSU.Z, -2047 / scaleFactor, 2047 / scaleFactor);

        // If all are zero, return early
        if (FSU == Vector3.Zero)
        {
            return FSU;
        }

        // Now perform what was traditionally a "server-side" computation
        // This clamps the final allowable input speed to maxSpeed and performs some
        // normalization of the input vector to avoid faster movement while moving
        // diagonally
        float FSULength = FSU.Length();
        Vector3 FSUFinal = new Vector3();
        FSUFinal.X = (FSU.X * MaxSpeed) / FSULength;
        FSUFinal.Y = (FSU.Y * MaxSpeed) / FSULength;
        FSUFinal.Z = (FSU.Z * MaxSpeed) / FSULength;

        if (movementStates.IsInState<Crouched>())
        {
            FSUFinal *= 0.333f;
        }

        return FSUFinal;
    }

    // https://www.jwchong.com/hl/basicphy.html
    public bool IsOnFloorAndSnap(float distanceToCheck)
    {
        Debug.Assert(distanceToCheck < 0.0f, "Cannot pass positive value to IsOnFloorAndSnap");

        // TODO: Max floor angle

        // This is Goldsrc's method for getting us "unstuck" from the ground when we jump
        // TODO: IMPROVEMENT: Ignore this hardcoded value and just use hsm to handle this
        if (velocity.Y > 180.0f / scaleFactor)
        {
            return false;
        }

        Vector3 downMotion = new Vector3(0.0f, distanceToCheck / scaleFactor, 0.0f);
        KinematicCollision3D collision;
        if ((collision = TestMove(GlobalTransform, downMotion)) != null)
        {
            // This is Goldsrc's method for determining whether a slope is walkable or not
            // TODO: IMPROVEMENT: We can base this on an actual angle and make it
            // configurable
            if (collision.GetNormal().Y >= maxFloorAngleValue)
            {
                // Snap to the ground
                MoveAndCollide(downMotion);
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }

    private KinematicCollision3D TestMove(Transform3D from, Vector3 motion)
    {
        KinematicCollision3D collision = new KinematicCollision3D();
        bool res = TestMove(from, motion, collision, recoveryAsCollision: true, maxCollisions: 6);

        return res ? collision : null;
    }

    // Gravity is normally applied via "leapfrog integration" instead of a simpler
    // euler integration technique. I'm not 100% sure this is actually necessary
    // due to the fact that we use a fixed physics timestep, but there isn't much of a
    // reason not to do it to be safe.
    // https://www.jwchong.com/hl/movement.html#gravity
    private void ApplyHalfGravity(float timeStep)
    {
        velocity.Y -= 0.5f * Gravity * timeStep;
    }

    private void MoveAndSlide(float timeStep)
    {
        Vector3 deltaRemaining = velocity * timeStep;

        for (int i = 0; i < 4; i++)
        {
            KinematicCollision3D collision = MoveAndCollide(deltaRemaining);

            // We didn't collide with anythin, so we are done
            if (collision == null)
            {
                break;
            }
            // Collision was detected, so we haven't moved the entire distance yet...
            // Get the amount of movement still left to perform
            deltaRemaining = collision.GetRemainder();

            // Before we process collision, can we step up to avoid it?
            Vector3 stepUpPos;
            if (
                movementStates.IsInState<Ground>()
                && (stepUpPos = TryStepUp(ref deltaRemaining)) != Vector3.Zero
            )
            {
                // Teleport directly to the step up location
                GlobalPosition = stepUpPos;
                continue;
            }

            // Process Collision
            float bounceCoefficient = ComputeBounceCoefficient(collision.GetNormal().Y);
            deltaRemaining = ComputeCollisionResponse(
                deltaRemaining,
                collision.GetNormal(),
                bounceCoefficient
            );
            velocity = ComputeCollisionResponse(velocity, collision.GetNormal(), bounceCoefficient);

            // TODO: IMPROVEMENT: The player can "collide up" a steep slope when on the ground.
            // It would be nice to remove this....somehow, if it doesn't conflict with any
            // other movement functionality
        }
    }

    private Vector3 ComputeCollisionResponse(
        Vector3 vel,
        Vector3 collisionNormal,
        float bounceCoefficient
    )
    {
        vel -= bounceCoefficient * vel.Dot(collisionNormal) * collisionNormal;

        return vel;
    }

    private float ComputeBounceCoefficient(float normalY)
    {
        if (
            (movementStates.IsInState<Air>() || !Mathf.IsEqualApprox(entityFriction, 1.0f))
            && normalY <= maxFloorAngleValue
        )
        {
            return 1 + (bounce * (1 - entityFriction));
        }
        else
        {
            return 1.0f;
        }
    }

    // TODO: Research how correct this solution is compared to goldsrc
    // jwchong doesn't have a lot to say about it...
    // TODO: Minor bug causes player to not step up at very slow speeds
    private Vector3 TryStepUp(ref Vector3 deltaRemaining)
    {
        float minMoveAmt = 0.125f / scaleFactor;
        Vector3 up = new Vector3(0, MaxStepHeight, 0);

        // Trace up to see how much headroom we have
        stepCollider.Position = Vector3.Zero;
        stepCollider.TargetPosition = up;
        stepCollider.ForceShapecastUpdate();
        Vector3 upMoved = up * stepCollider.GetClosestCollisionSafeFraction();
        stepCollider.Position = upMoved;

        // Trace forward with our remaining velocity to see how far forward we can potentially move
        // above the obstacle
        stepCollider.TargetPosition = deltaRemaining;
        stepCollider.ForceShapecastUpdate();
        Vector3 amtMoved = deltaRemaining * stepCollider.GetClosestCollisionSafeFraction();
        stepCollider.Position += amtMoved;
        // Failed to move any appreciable difference, there is no step up...
        if (amtMoved.Length() <= minMoveAmt)
        {
            return Vector3.Zero;
        }

        // Save how far we've moved, in case this step up succeeds
        Vector3 potentialNewRemaining =
            deltaRemaining * (1 - stepCollider.GetClosestCollisionSafeFraction());

        // If we've made it this far, we should trace downwards to find where the top
        // of the obstacle is
        stepCollider.TargetPosition = -upMoved;
        stepCollider.ForceShapecastUpdate();
        Vector3 stepPosition =
            stepCollider.Position + (-upMoved * stepCollider.GetClosestCollisionSafeFraction());
        stepCollider.Position = Vector3.Zero;

        // Check if we've moved up a meaningful amount
        // TODO: Is 0.1 trenchbroom units a good margin of error?
        if (Mathf.Abs(ToGlobal(stepPosition).Y - GlobalPosition.Y) > minMoveAmt)
        {
            // We successfully stepped up. We need to subtract the velocity we are about
            // to perform with a teleport
            deltaRemaining = potentialNewRemaining;
            return ToGlobal(stepPosition);
        }
        else
        {
            return Vector3.Zero;
        }
    }
}
