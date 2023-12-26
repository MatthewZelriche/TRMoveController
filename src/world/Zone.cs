using Godot;

public enum ZoneType
{
	WaterZone
}

[Tool]
public partial class Zone : Area3D
{
	[Export]
	protected Vector3 size = new Vector3(64.0f, 64.0f, 64.0f);

	protected ZoneType type;

	private CollisionShape3D collider;
	private BoxShape3D colliderShape = new BoxShape3D();
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

	private Vector3 Size
	{
		get { return size / 32.0f; }
		set { size = value * 32.0f; }
	}

	// Called when the node enters the scene tree for the first time.
	public override void _EnterTree()
	{
		collider = GetNode<CollisionShape3D>("collider");
		collider.Shape = colliderShape;
		visualizer = GetNode<CsgBox3D>("visualizer");
		AdjustZoneShape();
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		AdjustZoneShape();
	}

	protected void SetColor(Color color)
	{
		if (visualizer.Material is null)
		{
			visualizer.Material = new StandardMaterial3D();
		}
		((StandardMaterial3D)visualizer.Material).AlbedoColor = color;
	}

	private void AdjustZoneShape()
	{
		colliderShape.Size = Size;
		visualizer.Size = Size;
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
