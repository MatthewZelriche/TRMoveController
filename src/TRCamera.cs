using Godot;

public partial class TRCamera : Camera3D
{
    [Export]
    // Corresponds to the cvar 'm_yaw' in goldSrc
    private float yawSpeed = 0.022f;

    [Export]
    // Corresponds to the cvar 'm_pitch' in goldSrc
    private float pitchSpeed = 0.022f;

    [Export]
    // Corresponds to the cvar 'sensitivity' in goldSrc
    private float sensitivity = 2.0f;

    private float pitch;
    private float yaw;

    public override void _Ready()
    {
        // Camera may start rotated - so we need to get the "starting" pitch and yaw
        pitch = Mathf.RadToDeg(Rotation.X);
        yaw = Mathf.RadToDeg(Rotation.Y);
    }

    public Vector3 UnitForwardHorzVector()
    {
        // Project the Forward Vector onto the XZ horizontal plane, so that we
        // can get the player's horizontal movement direction irrespective of their
        // camera pitch
        return new Plane(new Vector3(0, 1, 0)).Project(UnitForwardVector()).Normalized();
    }

    public Vector3 UnitForwardVector()
    {
        return -GlobalTransform.Basis.Z.Normalized();
    }

    public Vector3 UnitRightVector()
    {
        return GlobalTransform.Basis.X.Normalized();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motionEvent)
        {
            pitch -= motionEvent.Relative.Y * pitchSpeed * sensitivity;
            yaw -= motionEvent.Relative.X * yawSpeed * sensitivity;

            // Clamp camera to avoid flipping
            if (pitch >= 89.0f)
            {
                pitch = 89.0f;
            }
            if (pitch <= -89.0f)
            {
                pitch = -89.0f;
            }

            Rotation = new Vector3(Mathf.DegToRad(pitch), Mathf.DegToRad(yaw), Rotation.Z);
        }
    }
}
