using Duccsoft;
using Duccsoft.ImGui;
using System;

namespace Sandbox;

public class MeshDistanceFieldDemo : Component
{
	private MeshDistanceField _mdf;
	protected override void OnUpdate()
	{
		UpdateMdf();
		if ( _mdf is null )
		{
			_copiedTex?.Dispose();
			_copiedTex = null;
		}
		UpdateUI();
	}

	private void UpdateMdf()
	{
		var tr = Scene.Trace
			.Sphere( 1000f, Vector3.Zero, Vector3.Zero )
			.Run();
		if ( !tr.Hit || tr.Shape is null )
		{
			_mdf = null;
			return;
		}

		if ( !MeshDistanceSystem.Current.TryGetMdf( tr.Shape, out var mdf ) )
		{
			_mdf = null;
			return;
		}

		_mdf = mdf;
	}

	private int _textureSlice = -1;
	private ComputeShader _textureSliceCs = new( "texture_slice_copy_cs" );
	private Texture _copiedTex;

	private void UpdateUI()
	{
		if ( ImGui.Begin( "???" ) )
		{
			if ( _mdf is null )
			{
				ImGui.Text( "No mesh distance field generated." );
				return;
			}
			ImGui.Text( "Slice:" ); ImGui.SameLine();
			_textureSlice = _textureSlice.Clamp( 0, _mdf.VolumeTexture.Depth - 1);
			var newSlice = _textureSlice;
			ImGui.SliderInt( nameof( _textureSlice ), ref newSlice, 0, _mdf.VolumeTexture.Depth - 1 );
			if ( _copiedTex is null || newSlice != _textureSlice )
			{
				_textureSlice = newSlice.Clamp( 0, _mdf.VolumeTexture.Depth - 1 );
				_copiedTex = CopyMdfTexture( _textureSlice );
			}
			ImGui.Image( _copiedTex, _copiedTex.Size * 4 * ImGuiStyle.UIScale, Color.Transparent, ImGui.GetColorU32( ImGuiCol.Border ) );
			ImGui.Text( $"Size: {_mdf.VolumeTexture.Size.x}x{_mdf.VolumeTexture.Size.y}x{_mdf.VolumeTexture.Depth}" );
			if ( ImGui.Button( "Regenerate" ) )
			{
				MeshDistanceSystem.Current.RemoveMdf( _mdf.Id );
			}
		}
		ImGui.End();
	}

	private Texture CopyMdfTexture( int slice )
	{
		var input = _mdf.VolumeTexture;
		var output = Texture.Create( input.Width, input.Height, ImageFormat.RGBA32323232F )
			.WithUAVBinding()
			.Finish();

		_textureSliceCs.Attributes.Set( "InputTexture", input );
		_textureSliceCs.Attributes.Set( "OutputTexture", output );
		_textureSliceCs.Attributes.Set( "Slice", slice );
		_textureSliceCs.Dispatch( output.Width, output.Height );
		return output;
	}
}
