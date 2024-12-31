namespace Duccsoft;

/// <summary>
/// Represents a type of collision that may be resolved using Verlet rope simulation.
/// </summary>
public enum RopeColliderType
{
	/// <summary>
	/// No collider was found.
	/// </summary>
	None,
	/// <summary>
	/// A mesh with arbitrary geometry. Collisions with this shape are resolved using a MeshDistanceField.
	/// <br/><br/>
	/// This serves as the emergency fallback for any shape not resolvable using simple math or signed 
	/// distance functions. The MeshDistanceField is built using the triangulation of the PhysicsShape.
	/// </summary>
	Mesh,
	/// <summary>
	/// An unscaled or uniformly scaled sphere.
	/// </summary>
	UniformSphere,
	/// <summary>
	/// A right rectangular prism where each side is parallel to its opposite.
	/// </summary>
	Box,
	/// <summary>
	/// A non-uniformly scaled sphere, treated by Box3D as a hull.
	/// </summary>
	HullSphere,
	/// <summary>
	/// A rectangular plane with arbitrary surface area and zero volume.
	/// This is treated by Box3D as a mesh, but the collision is resolvable with simple math.
	/// </summary>
	Plane,
	/// <summary>
	/// A capsule with rounded ends of the same radius.
	/// </summary>
	Capsule,
	// The following shapes are not yet implemented, but would likely be trivial to implement
	// using a signed distance function to calculate position and gradient/normal.
	/*
	Frustum,
	TriangularPrism,
	Cylinder,
	CappedCone,
	Pyramid
	*/
}

public static class RopeColliderPhysicsTraceResultExtensions
{
	public static RopeColliderType ClassifyColliderType( this PhysicsTraceResult tr )
	{
		// No collider was found.
		if ( !tr.Shape.IsValid() || tr.Shape.Collider is not Component )
			return RopeColliderType.None;

		// Uniformly scaled sphere.
		if ( tr.Shape.IsSphereShape )
			return RopeColliderType.UniformSphere;

		// Non-uniform sphere.
		if ( tr.Shape.IsHullShape && tr.Shape.Collider is SphereCollider )
			return RopeColliderType.HullSphere;

		if ( tr.Shape.IsCapsuleShape )
			return RopeColliderType.Capsule;

		if ( tr.Shape.IsHullShape && tr.Shape.Collider is BoxCollider )
			return RopeColliderType.Box;

		if ( tr.Shape.IsMeshShape && tr.Shape.Collider is PlaneCollider )
			return RopeColliderType.Plane;

		// Since we don't have any better way of dealing with this collider, classify it as a mesh.
		return RopeColliderType.Mesh;
	}
}
