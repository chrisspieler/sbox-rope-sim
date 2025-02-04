﻿namespace Duccsoft;

public class SceneRopeObject : SceneCustomObject
{
	public enum CapStyle
	{
		None,
		Triangle,
		Arrow,
		Rounded
	}

	public enum FaceMode
	{
		Camera,
		Normal
	}

	private static readonly Material LineShader = Material.FromShader( "shaders/line.shader" );

	public int VertexCount
	{
		get
		{
			if ( !Vertices.IsValid() )
				return 0;

			return Vertices.ElementCount;
		}
		set => GpuBufferUtils.EnsureCount( ref _vertices, value, GpuBuffer.UsageFlags.Structured | GpuBuffer.UsageFlags.Vertex );
	}
	public GpuBuffer<RopeVertex> Vertices 
	{
		get => _vertices;
		set => _vertices = value;
	}
	private GpuBuffer<RopeVertex> _vertices;

	public Texture LineTexture
	{
		get => Attributes.GetTexture( "BaseTexture", null );
		set => Attributes.Set( "BaseTexture", value );
	}

	public CapStyle StartCap
	{
		get => (CapStyle)Attributes.GetInt( "StartCap" );
		set => Attributes.Set( "StartCap", (int)value );
	}

	public CapStyle EndCap
	{
		get => (CapStyle)Attributes.GetInt( "EndCap" );
		set => Attributes.Set( "EndCap", (int)value );
	}

	public FaceMode Face
	{
		get => (FaceMode)Attributes.GetInt( "FaceMode" );
		set => Attributes.Set( "FaceMode", (int)value );
	}

	public bool Wireframe
	{
		get => Attributes.GetComboBool( "D_WIREFRAME" );
		set => Attributes.SetCombo( "D_WIREFRAME", value );
	}

	public int Smoothness
	{
		get => Attributes.GetInt( "Smoothness" );
		set => Attributes.Set( "Smoothness", value );
	}

	public bool Opaque
	{
		get => Attributes.GetComboBool( "D_OPAQUE" );
		set => Attributes.SetCombo( "D_OPAQUE", value );
	}

	public bool EnableLighting
	{
		get => Attributes.GetComboBool( "D_ENABLE_LIGHTING" );
		set => Attributes.SetCombo( "D_ENABLE_LIGHTING", value );
	}

	public SceneRopeObject( SceneWorld sceneWorld )
		: base( sceneWorld )
	{
		LineTexture = Texture.White;
		RenderOverride = Render;
	}

	private void Render( SceneObject so )
	{
		if ( !Vertices.IsValid() || Vertices.ElementCount < 2 )
			return;

		Graphics.Draw( Vertices, LineShader, startVertex: 0, vertexCount: 0, attributes: Attributes, primitiveType: Graphics.PrimitiveType.LineStripWithAdjacency );
	}
}
