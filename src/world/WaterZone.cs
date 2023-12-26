using Godot;

[Tool]
public partial class WaterZone : Zone
{
	Color color = Colors.Blue;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		base._Ready();
		type = ZoneType.WaterZone;
		SetColor(color);
	}

	// Called when the node enters the scene tree for the first time.
	public override void _EnterTree()
	{
		base._EnterTree();
		SetColor(color);
	}
}
