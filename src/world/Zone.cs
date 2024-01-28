using Godot;
using Godot.Collections;

public enum ZoneType
{
    WaterZone
}

public partial class Zone : Area3D
{
    [Export]
    Dictionary properties;

    protected ZoneType type;
    private CsgBox3D visualizer;

    public ZoneType Type
    {
        get => type;
    }

    public Zone()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        GetChild<MeshInstance3D>(0).Transparency = 0.5f;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
    }

    protected void OnBodyEntered(Node3D body)
    {
        if (body is TRMoveController)
        {
            ((TRMoveController)body).AddTouchingZone(this);
        }
    }

    protected void OnBodyExited(Node3D body)
    {
        if (body is TRMoveController)
        {
            ((TRMoveController)body).RemoveTouchingZone(this);
        }
    }
}
