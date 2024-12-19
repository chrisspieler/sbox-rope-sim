namespace Duccsoft;

public class RopeRenderer : Component
{
	[Property] public LineRenderer Line { get; set; }
	public VerletRope Rope { get; set; }

	protected override void OnUpdate()
	{
		if ( !Line.IsValid() || Rope is null )
			return;
		Line.UseVectorPoints = true;
		Line.VectorPoints = Rope.Points
			.Select( p => p.Position )
			.ToList();
	}
}
