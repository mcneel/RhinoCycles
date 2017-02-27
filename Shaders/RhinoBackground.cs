/**
Copyright 2014-2016 Robert McNeel and Associates

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
**/

using ccl;
using ccl.ShaderNodes;
using Rhino.Display;

namespace RhinoCyclesCore.Shaders
{
	public class RhinoBackground : RhinoShader
	{
		readonly BackgroundNode _backgroundNode = new BackgroundNode("m_shader bg");

		readonly BackgroundNode _skyBg = new BackgroundNode("sky bg");
		readonly BackgroundNode _reflBg = new BackgroundNode("refl bg");

		readonly LightPathNode _lightpath = new LightPathNode("lightpath");
		readonly MixClosureNode _mixSkylightSwitch = new MixClosureNode("mix_skylightswitch");
		readonly MixClosureNode _mixBgAndRefl = new MixClosureNode("mix_bg_and_refl");
		readonly MathNode _max = new MathNode("max") { Operation = MathNode.Operations.Maximum };
		// normal gradient texture (linear) has gradient from left to right
		// We want that to go along the window
		readonly TextureCoordinateNode _textureCoordinates = new TextureCoordinateNode("texture_coordinates");
		// the mapping node should rotate the texture coordinate input 90 around
		// texture z axis (we're looking onto it)
		readonly MappingNode _mapping = new MappingNode("mapping")
		{
			Mapping = MappingNode.MappingType.Texture,
			Translation = RenderEngine.CreateFloat4(0.0, 0.0, 0.0),
			Scale = RenderEngine.CreateFloat4(1.0, 1.0, 1.0),
			Rotation = RenderEngine.CreateFloat4(0.0, 0.0, 1.570796),
		};

		// the actual gradient node, used as factor for color ramp node
		readonly GradientTextureNode _gradient = new GradientTextureNode { Gradient = GradientTextureNode.GradientType.Linear };

		// Add color ramp. Color stop on 1.0 is bottom color,
		// color stop on 0.0 is top color
		readonly ColorRampNode _colorramp = new ColorRampNode();

		readonly EnvironmentTextureNode _bgEnvTexture = new EnvironmentTextureNode { ColorSpace = TextureNode.TextureColorSpace.None, IsLinear = true, Interpolation = InterpolationType.Cubic};
		readonly EnvironmentTextureNode _reflEnvTexture = new EnvironmentTextureNode { ColorSpace = TextureNode.TextureColorSpace.None, IsLinear = false };
		readonly EnvironmentTextureNode _skyEnvTexture = new EnvironmentTextureNode { ColorSpace = TextureNode.TextureColorSpace.None, IsLinear = false };

		public RhinoBackground(Client client, CyclesBackground intermediate, Shader existing) : this(client, intermediate, existing, "background")
		{
		}

		public RhinoBackground(Client client, CyclesBackground intermediate, Shader existing, string name) : base(client, intermediate)
		{
			if (existing != null)
			{
				m_shader = existing;
				m_shader.Recreate();
			}
			else
			{
				m_shader = new Shader(m_client, Shader.ShaderType.World)
				{
					UseMis = true,
					UseTransparentShadow = true,
					HeterogeneousVolume = false,
					Name = name
				};
			}
		}

		public override Shader GetShader()
		{
			if (!string.IsNullOrEmpty(m_original_background.Xml))
			{
				var xml = m_original_background.Xml;
				Shader.ShaderFromXml(ref m_shader, xml);
			}
			else
			{
				var black = new float4(0.0f);
				var tst = new float4(1.0f, 0.5f, 0.25f);

				var color1 = (m_original_background.color1.IsEmpty ? tst : RenderEngine.CreateFloat4(m_original_background.color1)) ^ m_original_background.Gamma;
				var color2 = (m_original_background.color2.IsEmpty ? tst : RenderEngine.CreateFloat4(m_original_background.color2)) ^ m_original_background.Gamma;
				var bgcolor = (m_original_background.bg_color.IsEmpty ? black : RenderEngine.CreateFloat4(m_original_background.bg_color)) ^ m_original_background.Gamma;
				var skycolor = (m_original_background.sky_color.IsEmpty ? black : RenderEngine.CreateFloat4(m_original_background.sky_color)) ^ m_original_background.Gamma;
				var reflcolor = (m_original_background.refl_color.IsEmpty ? black : RenderEngine.CreateFloat4(m_original_background.refl_color)) ^ m_original_background.Gamma;

				// our main background shader. With just this, and some color != black set we should get skylighting
				// use the bgcolor from the background (360deg) environment if it is specified, instead.
				_backgroundNode.ins.Strength.Value = 1.0f;
				_backgroundNode.ins.Color.Value = (m_original_background.background_environment != null ? bgcolor : color1);

				#region skylight disabler/enabler nodes

				// node to give highest value (used for light path checks)
				_max.ins.Value1.Value = 0.0f;
				_max.ins.Value2.Value = 0.0f;

				#endregion

				#region gradient bg nodes


				// just simple linear gradient
				_colorramp.ColorBand.Interpolation = ColorBand.Interpolations.Linear;
				// bottom color on 0.0f
				_colorramp.ColorBand.InsertColorStop(color2, 0.0f);
				// top color on 1.0f
				_colorramp.ColorBand.InsertColorStop(color1, 1.0f);

				#endregion

				#region nodes for environment textures on bg/refl/skylight

				if (m_original_background.wallpaper.HasTextureImage && m_original_background.background_fill == BackgroundStyle.WallpaperImage)
				{
					RenderEngine.SetTextureImage(_bgEnvTexture, m_original_background.wallpaper);
				}
				else if (m_original_background.bg.HasTextureImage)
				{
					RenderEngine.SetTextureImage(_bgEnvTexture, m_original_background.bg);
				}

				if (m_original_background.refl.HasTextureImage)
				{
					RenderEngine.SetTextureImage(_reflEnvTexture, m_original_background.refl);
				}

				if (m_original_background.sky.HasTextureImage)
				{
					RenderEngine.SetTextureImage(_skyEnvTexture, m_original_background.sky);
				}

				_skyBg.ins.Color.Value = skycolor;
				_skyBg.ins.Strength.Value = 1.0f;

				_reflBg.ins.Color.Value = reflcolor;
				_reflBg.ins.Strength.Value = 1.0f;

				#endregion

				// add background nodes
				m_shader.AddNode(_backgroundNode);
				m_shader.AddNode(_reflBg);
				m_shader.AddNode(_skyBg);
				// add environment texture nodes
				m_shader.AddNode(_bgEnvTexture);
				m_shader.AddNode(_reflEnvTexture);
				m_shader.AddNode(_skyEnvTexture);

				// light paths
				m_shader.AddNode(_lightpath);
				// a max for skylight stuff
				m_shader.AddNode(_max);

				// two mixer nodes
				m_shader.AddNode(_mixBgAndRefl);
				m_shader.AddNode(_mixSkylightSwitch);


				// gradient bg nodes
				m_shader.AddNode(_textureCoordinates);
				m_shader.AddNode(_mapping);
				m_shader.AddNode(_gradient);
				m_shader.AddNode(_colorramp);

				// to control skylight influence, the trick is to only sample on camera ray and on
				// glossy rays. This way we can see the background in
				// reflections and when we're looking directly to it.
				// our max(v1,v2) will be 1.0 when either or both are set
				_lightpath.outs.IsCameraRay.Connect(_max.ins.Value1);
				_lightpath.outs.IsGlossyRay.Connect(_max.ins.Value2);
				// also connect glossy ray to mix_bg_refl, so we get refl_bg when glossy ray
				_lightpath.outs.IsGlossyRay.Connect(_mixBgAndRefl.ins.Fac);

				_max.outs.Value.Connect(_mixSkylightSwitch.ins.Fac);

				// if there is a bg texture, put that in bg color
				if (m_original_background.bg.HasTextureImage && m_original_background.background_fill == BackgroundStyle.Environment && !m_original_background.PlanarProjection)
				{
					_bgEnvTexture.outs.Color.Connect(_backgroundNode.ins.Color);
				}
				// or if gradient fill is needed, so lets do that.
				else if (m_original_background.background_fill == BackgroundStyle.Gradient)
				{
					// gradient is 'screen-based', so use window tex coordinates
					_textureCoordinates.outs.Window.Connect(_mapping.ins.Vector);
					// rotate those coords into gradient
					_mapping.outs.Vector.Connect(_gradient.ins.Vector);

					// and finally into our color ramp
					_gradient.outs.Fac.Connect(_colorramp.ins.Fac);
					// now use that as background input
					_colorramp.outs.Color.Connect(_backgroundNode.ins.Color);
				}
				else if(m_original_background.PlanarProjection)
				{
					_mapping.outs.Vector.Connect(_bgEnvTexture.ins.Vector);
					_bgEnvTexture.outs.Color.Connect(_backgroundNode.ins.Color);
					_bgEnvTexture.Projection = TextureNode.EnvironmentProjection.Wallpaper;
				}
				else if (m_original_background.background_fill == BackgroundStyle.WallpaperImage && m_original_background.wallpaper.HasTextureImage)
				{
					_bgEnvTexture.outs.Color.Connect(_backgroundNode.ins.Color);
					_bgEnvTexture.Projection = TextureNode.EnvironmentProjection.Wallpaper;
				}

				// connect refl env texture if texture exists
				if (m_original_background.refl.HasTextureImage)
				{
					_reflEnvTexture.outs.Color.Connect(_reflBg.ins.Color);
				}
				// connect sky env texture if texture exists
				if (m_original_background.sky.HasTextureImage)
				{
					_skyEnvTexture.outs.Color.Connect(_skyBg.ins.Color);
				}

				if (m_original_background.HasSky && m_original_background.skylight_enabled)
				{
					_skyBg.outs.Background.Connect(_mixSkylightSwitch.ins.Closure1);
				}
				else
				{
					if (m_original_background.skylight_enabled)
					{
						_mixBgAndRefl.outs.Closure.Connect(_mixSkylightSwitch.ins.Closure1);
					}
				}

				// background always goes into closure 1 for bg+refl mix
				_backgroundNode.outs.Background.Connect(_mixBgAndRefl.ins.Closure1);

				// if we have a reflection color or texture, use that in
				// background and reflection mixer.
				if (m_original_background.HasRefl)
				{
					_reflBg.outs.Background.Connect(_mixBgAndRefl.ins.Closure2);
				}
				else // no color or texture for reflections, use regular background
				{
					_backgroundNode.outs.Background.Connect(_mixBgAndRefl.ins.Closure2);
				}

				// the bakground and reflection should always be connected to skylight
				// switch in second closure slot. This is so that direct background ray
				// hit from camera shows background. Also glossy ray still should be evaluated
				// to this one.
				_mixBgAndRefl.outs.Closure.Connect(_mixSkylightSwitch.ins.Closure2);

				_mixSkylightSwitch.outs.Closure.Connect(m_shader.Output.ins.Surface);
			}

			// phew, done.
			m_shader.FinalizeGraph();
			m_shader.Tag();

			return m_shader;
		}

	}
}
