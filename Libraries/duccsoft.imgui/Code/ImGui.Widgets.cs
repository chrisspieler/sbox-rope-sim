﻿using Duccsoft.ImGui.Elements;
using Duccsoft.ImGui.Rendering;

namespace Duccsoft.ImGui;

public static partial class ImGui
{
	public static bool IsItemClicked( ImGuiMouseButton button = ImGuiMouseButton.Left )
	{
		return System.ClickedElementId == CurrentItemRecursive?.Id;
	}

	public static void Text( string formatString, params object[] args )
	{
		var text = string.Format( formatString, args );
		_ = new TextWidget( CurrentWindow, text );
	}

	public static bool Button( string label, Vector2 size = default )
	{
		var button = new ButtonWidget( CurrentWindow, label );
		return button.IsReleased;
	}

	public static bool Checkbox( string label, ref bool value )
	{
		var checkbox = new Checkbox( CurrentWindow, label, ref value );
		return checkbox.IsReleased;
	}

	public static bool DragInt( string label, ref int value, float speed = 1.0f, int min = 0, int max = 0, string format = null, ImGuiSliderFlags flags = 0 )
	{
		var drag = new DragInt( CurrentWindow, label, ref value, speed, min, max, format, flags );
		// TODO: Is returning true correct?
		return drag.IsActive;
	}

	public static bool SliderFloat( string label, ref float value, float min, float max, string format = "F3", ImGuiSliderFlags flags = 0 )
	{
		var components = new float[1] { value };
		var slider = new Slider<float>( CurrentWindow, label, ref components, min, max, format );
		var hasChanged = components[0] != value;
		value = components[0];
		return hasChanged;
	}

	public static bool SliderFloat2( string label, ref Vector2 value, float min, float max, string format = "F3", ImGuiSliderFlags flags = 0 )
	{
		var components = new float[2] { value.x, value.y };
		var slider = new Slider<float>( CurrentWindow, label, ref components, min, max, format );
		value.x = components[0];
		value.y = components[1];
		return slider.IsActive;
	}

	public static bool SliderFloat3( string label, ref Vector3 value, float min, float max, string format = "F3", ImGuiSliderFlags flags = 0 )
	{
		var components = new float[3] { value.x, value.y, value.z };
		var slider = new Slider<float>( CurrentWindow, label, ref components, min, max, format );
		var hasChanged = components[0] != value[0] || components[1] != value[1] || components[2] != value[2];
		value.x = components[0];
		value.y = components[1];
		value.z = components[2];
		return hasChanged;
	}

	public static bool SliderFloat4( string label, ref Vector4 value, float min, float max, string format = "F3", ImGuiSliderFlags flags = 0 )
	{
		var components = new float[4] { value.x, value.y, value.z, value.w };
		var slider = new Slider<float>( CurrentWindow, label, ref components, min, max, format );
		value.x = components[0];
		value.y = components[1];
		value.z = components[2];
		value.w = components[3];
		return slider.IsActive;
	}

	public static bool SliderInt( string label, ref int value, int min, int max, string format = null, ImGuiSliderFlags flags = 0 )
	{
		var components = new int[1] { value };
		var _ = new Slider<int>( CurrentWindow, label, ref components, min, max, format );
		var hasChanged = components[0] != value;
		value = components[0];
		return hasChanged;
	}

	public static void Image( Texture texture, Vector2 size, Vector2 uv0, 
		Vector2 uv1, Color tintColor, Color borderColor,
		ImDrawList.ImageTextureFiltering textureFiltering = ImDrawList.ImageTextureFiltering.Anisotropic, float angle = 0f )
	{
		_ = new ImageWidget( CurrentWindow, texture, size, uv0, uv1, tintColor, borderColor, textureFiltering, angle );
	}

	public static void Image( Texture texture, Vector2 size, Vector2 uv0, Vector2 uv1, Color tintColor, Color borderColor )
	{
		Image( texture, size, uv0, uv1, tintColor, borderColor, ImDrawList.ImageTextureFiltering.Anisotropic, 0f );
	}

	public static void Image( Texture texture, Vector2 size, Color tintColor, Color borderColor )
	{
		Image( texture, size, Vector2.Zero, Vector2.One, tintColor, borderColor );
	}
}
