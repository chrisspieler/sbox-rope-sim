using Duccsoft;
using Duccsoft.ImGui;

public class MdfTextureViewer : Component
{
	public int MdfIndex
	{
		get => _mdfIndex;
		set
		{
			_mdfIndex = value.UnsignedMod( MeshDistanceSystem.Current.MdfCount );
		}
	}
	private int _mdfIndex;
	public int TextureSlice { get; set; }
	public MeshDistanceField Mdf { get; set; }

	readonly ComputeShader _textureSliceCs = new( "mesh_sdf_preview_cs" );
	int _maxSlice;
	Texture _copiedTex;

	protected override void OnUpdate()
	{
		ImGui.SetNextWindowPos( new Vector2( 50, 50 ) * ImGuiStyle.UIScale );
		if ( ImGui.Begin( "Volume Texture Viewer" ) )
		{
			DrawWindow();
		}
		ImGui.End();
	}

	private void DrawWindow()
	{
		var system = MeshDistanceSystem.Current;

		if ( Mdf?.VoxelSdf is null )
		{
			ImGui.Text( $"No mesh distance field exists yet." );
			return;
		}
		ImGui.Text( $"MDF Count: {system.MdfCount}" ); ImGui.SameLine();
		ImGui.Text( $"Total Data Size: {system.MdfTotalDataSize.FormatBytes()}" );
		if ( ImGui.Button( "Next MDF" ) )
		{
			MdfIndex++;
		} 
		ImGui.SameLine();
		if ( ImGui.Button( "Previous MDF" ) )
		{
			MdfIndex--;
		}
		ImGui.Text( $"MDF Index: {MdfIndex}" ); ImGui.SameLine();
		ImGui.Text( $"MDF Id: {Mdf.Id}" );
		var size = Mdf.VoxelSdf.VoxelGridDims;
		ImGui.Text( $"Dimensions: {size}x{size}x{size}" ); ImGui.SameLine();
		ImGui.Text( $"Data Size: { Mdf.DataSize.FormatBytes()}" );
		if ( ImGui.Button( "Remove MDF" ) )
		{
			system.RemoveMdf( Mdf.Id );
			return;
		}
		ImGui.Text( "Slice:" ); ImGui.SameLine();
		_maxSlice = size - 1;
		TextureSlice = TextureSlice.Clamp( 0, _maxSlice );
		var newSlice = TextureSlice;
		ImGui.SliderInt( nameof( TextureSlice ), ref newSlice, 0, _maxSlice );
		if ( _copiedTex is null || newSlice != TextureSlice )
		{
			TextureSlice = newSlice.Clamp( 0, _maxSlice );
			_copiedTex = CopyMdfTexture( Mdf, TextureSlice );
		}
		ImGui.Image( _copiedTex, new Vector2( 400 ) * ImGuiStyle.UIScale, new Vector2( 0, 0 ), new Vector2( 1, 1 ), Color.Transparent, ImGui.GetColorU32( ImGuiCol.Border ) );
	}

	private Texture CopyMdfTexture( MeshDistanceField mdf, int z )
	{
		var size = mdf.VoxelSdf.VoxelGridDims;

		var outputTex = Texture.Create( size, size )
			.WithUAVBinding()
			.Finish();

		var voxelSdfGpu = new GpuBuffer<int>( size * size * size / 4, GpuBuffer.UsageFlags.Structured );
		voxelSdfGpu.SetData( mdf.VoxelSdf.VoxelSdf );
		_textureSliceCs.Attributes.Set( "VoxelMinsOs", mdf.Bounds.Mins );
		_textureSliceCs.Attributes.Set( "VoxelMaxsOs", mdf.Bounds.Maxs );
		_textureSliceCs.Attributes.Set( "VoxelVolumeDims", new Vector3( size ) );
		_textureSliceCs.Attributes.Set( "VoxelSdf", voxelSdfGpu );
		_textureSliceCs.Attributes.Set( "ZLayer", z );
		_textureSliceCs.Attributes.Set( "OutputTexture", outputTex );
		_textureSliceCs.Dispatch( size, size, size );
		return outputTex;
	}
}
