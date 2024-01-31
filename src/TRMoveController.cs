using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using Hsm;

// Possible improvements:
// 1. Eliminate Ducktapping bug
// 2. Smooth uncrouch
// 3. Snap on step down
// 4. Toggle crouch button


// Note: This is ordered a particular way for performance
enum WaterLevel
{
    Eyes,
    Center,
    Feet,
    None
}

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

    [Export(PropertyHint.Range, "0,600,5,or_greater")]
    // Corresponds to the cvar 'cl_upspeed' in goldsrc
    private int upSpeed = 320;

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

    public float ForwardSpeed
    {
        get { return forwardSpeed / scaleFactor; }
        set { forwardSpeed = (int)(value * scaleFactor); }
    }

    public int SideSpeed
    {
        get { return sideSpeed / (int)scaleFactor; }
        set { sideSpeed = value * (int)scaleFactor; }
    }

    public int UpSpeed
    {
        get { return upSpeed / (int)scaleFactor; }
        set { upSpeed = value * (int)scaleFactor; }
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

    private Dictionary<ZoneType, List<Zone>> touchingZones = new();

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
        SetPlayerHeight(StandingHeight);
        playerCamera.Position = new Vector3(
            playerCamera.Position.X,
            GetFeetLocalPos() + EyeHeight,
            playerCamera.Position.Z
        );

        movementStates.Init<Air>(this);
    }

    public float GetFeetLocalPos()
    {
        float height = GetPlayerHeight();

        return -(height / 2);
    }

    public void SetPlayerHeight(float height)
    {
        ((BoxShape3D)collider.Shape).Size = new Vector3(Width, height, Width);
    }

    public float GetPlayerHeight()
    {
        return ((BoxShape3D)collider.Shape).Size.Y;
    }

    public override void _PhysicsProcess(double _step)
    {
        float step = (float)_step;
        // Processing states second ensures FSU values are modified on the next frame,
        // as intended
        movementStates.UpdateStates(step);
        movementStates.ProcessStateTransitions();
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        // HACK: Rigidbody sometimes causes some "residual" velocity after contact
        // Easiest way to fix is to just zero out the velocity at every frame since we
        // track it seperately
        state.LinearVelocity = Vector3.Zero;
    }

    private WaterLevel ComputeWaterLevel()
    {
        PhysicsPointQueryParameters3D param = new PhysicsPointQueryParameters3D();
        param.CollideWithAreas = true;
        param.CollideWithBodies = false;
        var space = GetWorld3D().DirectSpaceState;

        foreach (WaterLevel level in Enum.GetValues(typeof(WaterLevel)))
        {
            if (level == WaterLevel.None)
            {
                continue;
            }

            switch (level)
            {
                case WaterLevel.Feet:
                    param.Position = ToGlobal(
                        new Vector3(0, GetFeetLocalPos() + (1.0f / scaleFactor), 0)
                    );
                    break;
                case WaterLevel.Center:
                    param.Position = GlobalPosition;
                    break;
                case WaterLevel.Eyes:
                    param.Position = playerCamera.GlobalPosition;
                    break;
            }

            var results = space.IntersectPoint(param);
            foreach (var entry in results)
            {
                if (entry["collider"].AsGodotObject() is Zone)
                {
                    Zone zone = (Zone)entry["collider"].AsGodotObject();
                    if (zone.Type == ZoneType.WaterZone)
                    {
                        return level;
                    }
                }
            }
        }

        return WaterLevel.None;
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

    private Vector3 ComputeWaterVelocity(float step)
    {
        Vector3 FSU = ComputeFSU();
        Vector3 accelVector;
        if (FSU == Vector3.Zero)
        {
            accelVector = new Vector3(0, -(60 / scaleFactor), 0);
        }
        else
        {
            accelVector = (
                (
                    FSU.X * playerCamera.UnitRightVector()
                    + new Vector3(0, FSU.Y, 0)
                    + (FSU.Z * playerCamera.UnitForwardVector())
                )
            );
        }
        float clampedSpeed = 0.8f * Mathf.Min(MaxSpeed, accelVector.Length());
        float gamma1 = entityFriction * step * clampedSpeed * accelerate;
        float frictionCoefficient = friction * entityFriction * edgeFriction;
        float gamma2 =
            clampedSpeed - (1 - (entityFriction * frictionCoefficient * step)) * velocity.Length();
        float muCoefficient;
        if (gamma2 > 0 && clampedSpeed >= 0.1f / scaleFactor)
        {
            muCoefficient = Mathf.Min(gamma1, gamma2);
        }
        else
        {
            muCoefficient = 0;
        }

        return (1 - (entityFriction * frictionCoefficient * step)) * velocity
            + (muCoefficient * accelVector.Normalized());
    }

    private bool CheckWaterJump()
    {
        if (ComputeWaterLevel() != WaterLevel.Center)
        {
            return false;
        }

        Vector3 FSU = ComputeFSU();
        Vector3 moveDir =
            (
                FSU.X * playerCamera.UnitRightVector()
                + FSU.Z * playerCamera.UnitForwardHorzVector()
            ).Normalized() * (24.0f / scaleFactor);

        Vector3 from = GlobalPosition + new Vector3(0, 8 / scaleFactor, 0);
        Transform3D startTransform = GlobalTransform;
        startTransform.Origin = from;
        Vector3 to = moveDir;
        KinematicCollision3D result;
        if ((result = TestMove(startTransform, to)) != null && result.GetNormal().Y < 0.1f)
        {
            PhysicsRayQueryParameters3D param = new PhysicsRayQueryParameters3D();
            var exclude = new Godot.Collections.Array<Rid>();
            exclude.Add(GetRid());
            param.Exclude = exclude;
            var space = GetWorld3D().DirectSpaceState;
            Vector3 globalPos = GlobalPosition;
            globalPos.Y += GetPlayerHeight() / 2;
            param.From = globalPos;
            param.To = param.From + moveDir;
            var res = space.IntersectRay(param);
            if (res.Count == 0)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 ComputeGroundFriction(Vector3 horzVel, float step)
    {
        float edgeFriction = ComputeEdgeFriction(horzVel);
        float frictionCoefficient = friction * entityFriction * edgeFriction;
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

    private float ComputeEdgeFriction(Vector3 horzVel)
    {
        Transform3D transform = Transform;
        transform.Origin +=
            (Width / 2) * horzVel.Normalized() - new Vector3(0, GetPlayerHeight() / 2, 0);
        var result = TestMove(transform, new Vector3(0, -(34 / scaleFactor), 0));

        return result == null ? edgeFriction : 1.0f;
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
        if (Input.IsActionPressed("TRUp"))
        {
            FSU.Y += UpSpeed;
        }
        if (Input.IsActionPressed("TRDown"))
        {
            FSU.Y -= UpSpeed;
        }

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
        float FSULength = Mathf.Min(FSU.Length(), MaxSpeed);
        FSU = FSU.Normalized() * FSULength;

        if (movementStates.IsInState<Crouched>())
        {
            FSU *= 0.333f;
        }

        return FSU;
    }

    // https://www.jwchong.com/hl/basicphy.html
    public bool IsOnFloorAndSnap(float distanceToCheck)
    {
        Debug.Assert(distanceToCheck < 0.0f, "Cannot pass positive value to IsOnFloorAndSnap");

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
        bool res = TestMove(from, motion, collision, maxCollisions: 6);

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

            // Apply an impulse onto a rigidbody instead of sliding
            if (collision.GetCollider().IsClass("RigidBody3D"))
            {
                RigidBody3D aa = (RigidBody3D)collision.GetCollider();
                aa.ApplyCentralImpulse(Mass * deltaRemaining);
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

        Transform3D a = GlobalTransform;
        KinematicCollision3D result = TestMove(a, up);
        Vector3 upMove = result != null ? result.GetTravel() : up;
        a.Origin += upMove;

        result = TestMove(a, deltaRemaining);
        Vector3 forwardMove = result != null ? result.GetTravel() : deltaRemaining;
        a.Origin += forwardMove;
        Vector3 potentialRemainder = result != null ? result.GetRemainder() : Vector3.Zero;

        result = TestMove(a, -upMove);
        Vector3 downMove = result != null ? result.GetTravel() : -upMove;
        a.Origin += downMove;

        if (Mathf.Abs(GlobalPosition.Y - a.Origin.Y) > minMoveAmt)
        {
            deltaRemaining = potentialRemainder;
            return a.Origin;
        }
        else
        {
            return Vector3.Zero;
        }
    }

    public void AddTouchingZone(Zone zone)
    {
        if (!touchingZones.ContainsKey(zone.Type))
        {
            touchingZones.Add(zone.Type, new List<Zone>());
        }

        touchingZones[zone.Type].Add(zone);
    }

    public void RemoveTouchingZone(Zone zone)
    {
        touchingZones[zone.Type].Remove(zone);
    }

    public List<Zone> GetTouchingZonesOfType(ZoneType type)
    {
        if (!touchingZones.ContainsKey(type))
        {
            return null;
        }

        return touchingZones[type];
    }
}
