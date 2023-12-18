using System.Diagnostics;
using Godot;
using Hsm;

[GlobalClass]
public partial class TRMoveController : RigidBody3D
{
    [Export]
    private Camera3D playerCamera = null;
    private float scaleFactor = 16.0f;

    [ExportCategory("Gravity & Falling")]
    [Export]
    // Corresponds to the cvar 'sv_gravity' in goldsrc
    private float gravity = 800.0f;

    public float Gravity
    {
        get { return gravity / scaleFactor; }
        set { gravity = value * scaleFactor; }
    }

    private StateMachine movementStates = new StateMachine();
    private Vector3 velocity;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Debug.Assert(
            playerCamera != null,
            "You forgot to assign a Camera3D to the TRMoveController!"
        );

        movementStates.Init<Air>(this);
    }

    public override void _PhysicsProcess(double _step)
    {
        float step = (float)_step;
        movementStates.UpdateStates(step);
        movementStates.ProcessStateTransitions();
    }

    private void MoveAndSlide(float timeStep)
    {
        // TODO: Proper movement update
        MoveAndCollide(velocity * timeStep);
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
}
