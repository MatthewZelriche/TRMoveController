using System.Diagnostics;
using Godot;

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

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Debug.Assert(
            playerCamera != null,
            "You forgot to assign a Camera3D to the TRMoveController!"
        );
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) { }
}
