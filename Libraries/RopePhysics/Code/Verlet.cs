namespace Duccsoft;

public class Verlet
{
	public class Point
	{
		public Point( Vector3 position, bool isLocked )
		{
			Position = LastPosition = position;
			IsLocked = isLocked;
		}

		public Vector3 Position, LastPosition;
		public bool IsLocked;
	}

	public class Segment
	{
		public Segment( Point pointA, Point pointB, float length )
		{
			PointA = pointA;
			PointB = pointB;
			Length = length;
		}

		public Point PointA, PointB;
		public float Length;
	}

	public Vector3 Gravity { get; set; } = Vector3.Down * 800f;
	public int Iterations { get; set; } = 3;

	private readonly List<Point> Points = new();
	private readonly List<Segment> Segments = new();

	public IEnumerable<Point> GetPoints() => Points;
	public int PointCount => Points.Count;
	public IEnumerable<Segment> GetSegments() => Segments;
	public int SegmentCount => Segments.Count;

	public void Clear()
	{
		Points.Clear();
		Segments.Clear();
	}

	public Point AddPoint( Vector3 position, bool isLocked )
	{
		var point = new Point( position, isLocked );
		Points.Add( point );
		return point;
	}

	public Segment AddSegment( Point pointA, Point pointB, float length )
	{
		var segment = new Segment( pointA, pointB, length );
		Segments.Add( segment );
		return segment;
	}

	public void Tick()
	{
		UpdatePoints();
		for ( int i = 0; i < Iterations; i++ )
		{
			UpdateSegments();
		}
	}

	private void UpdatePoints()
	{
		foreach ( var point in Points )
		{
			if ( !point.IsLocked )
			{
				var positionBeforeUpdate = point.Position;
				point.Position += point.Position - point.LastPosition;
				point.Position += Gravity * Time.Delta * Time.Delta;
				point.LastPosition = positionBeforeUpdate;
			}
		}
	}

	private void UpdateSegments()
	{
		foreach ( var segment in Segments )
		{
			Vector3 center = (segment.PointA.Position + segment.PointB.Position) * 0.5f;
			Vector3 direction = (segment.PointA.Position - segment.PointB.Position).Normal;
			if ( !segment.PointA.IsLocked )
				segment.PointA.Position = center + direction * segment.Length * 0.5f;
			if ( !segment.PointB.IsLocked )
				segment.PointB.Position = center - direction * segment.Length * 0.5f;
		}
	}
}
