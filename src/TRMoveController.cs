using System.Diagnostics;
using Godot;
using Hsm;

[GlobalClass]
public partial class TRMoveController : RigidBody3D
{
    [Export]
    private Camera3D playerCamera = null;

    [Export]
    private CollisionShape3D collider = null;

    [Export(PropertyHint.Range, "1,128,1,or_greater")]
    // Values exposed to the editor are in Trenchbroom/Hammer units for ease of
    // tweaking. As such, a unit conversion needs to occur in order to map Trenchbroom
    // units to Godot units. If you are using Qodot, set this to the same scale factor
    // you use in Qodot.
    private float scaleFactor = 16.0f;

    [ExportCategory("Gravity & Falling")]
    [Export(PropertyHint.Range, "0,1200,20,or_greater")]
    // Corresponds to the cvar 'sv_gravity' in goldsrc
    private float gravity = 800.0f;

    public float Gravity
    {
        get { return gravity / scaleFactor; }
        set { gravity = value * scaleFactor; }
    }

    private StateMachine movementStates = new StateMachine();
    private Vector3 velocity;
    private SeparationRayShape3D floorChecker = new SeparationRayShape3D();

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

        movementStates.Init<Air>(this);
    }

    public override void _PhysicsProcess(double _step)
    {
        float step = (float)_step;
        movementStates.UpdateStates(step);
        movementStates.ProcessStateTransitions();
    }

    public bool IsOnFloor(bool snap = true)
    {
        // TODO: Max floor angle

        Vector3 downMotion = new Vector3(0.0f, -1.0f / scaleFactor, 0.0f);
        if (TestMove(GlobalTransform, downMotion) != null)
        {
            if (snap)
            {
                MoveAndCollide(downMotion);
            }
            return true;
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
        // TODO: Proper movement update
        MoveAndCollide(velocity * timeStep);
    }
}
