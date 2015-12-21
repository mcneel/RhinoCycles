/**
Copyright 2014-2015 Robert McNeel and Associates

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

namespace RhinoCycles.Shaders
{
	public class RhinoBackground : RhinoShader
	{
		readonly BackgroundNode m_background = new BackgroundNode("m_shader bg");

		readonly BackgroundNode m_sky_bg = new BackgroundNode("sky bg");
		readonly BackgroundNode m_refl_bg = new BackgroundNode("refl bg");

		readonly LightPathNode m_lightpath = new LightPathNode("lightpath");
		readonly MixClosureNode m_mix_skylightswitch = new MixClosureNode("mix_skylightswitch");
		readonly MixClosureNode m_mix_bg_and_refl = new MixClosureNode("mix_bg_and_refl");
		readonly MathNode m_max = new MathNode("max") { Operation = MathNode.Operations.Maximum };
		// normal gradient texture (linear) has gradient from left to right
		// We want that to go along the window
		readonly TextureCoordinateNode m_texture_coordinates = new TextureCoordinateNode("texture_coordinates");
		// the mapping node should rotate the texture coordinate input 90 around
		// texture z axis (we're looking onto it)
		readonly MappingNode m_mapping = new MappingNode("mapping")
		{
			Mapping = MappingNode.MappingType.Texture,
			Translation = RenderEngine.CreateFloat4(0.0, 0.0, 0.0),
			Scale = RenderEngine.CreateFloat4(1.0, 1.0, 1.0),
			Rotation = RenderEngine.CreateFloat4(0.0, 0.0, 1.570796),
		};

		// the actual gradient node, used as factor for color ramp node
		readonly GradientTextureNode m_gradient = new GradientTextureNode { Gradient = GradientTextureNode.GradientType.Linear };

		// Add color ramp. Color stop on 1.0 is bottom color,
		// color stop on 0.0 is top color
		readonly ColorRampNode m_colorramp = new ColorRampNode();

		readonly EnvironmentTextureNode m_bg_env_texture = new EnvironmentTextureNode();
		readonly EnvironmentTextureNode m_refl_env_texture = new EnvironmentTextureNode();
		readonly EnvironmentTextureNode m_sky_env_texture = new EnvironmentTextureNode();

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
			var black = new float4(0.0f);
			var tst = new float4(1.0f, 0.5f, 0.25f);

			var color1 = m_original_background.color1.IsEmpty ? tst : RenderEngine.CreateFloat4(m_original_background.color1);
			var color2 = m_original_background.color2.IsEmpty ? tst : RenderEngine.CreateFloat4(m_original_background.color2);
			var skycolor = m_original_background.sky_color.IsEmpty ? black : RenderEngine.CreateFloat4(m_original_background.sky_color);
			var reflcolor = m_original_background.refl_color.IsEmpty ? black : RenderEngine.CreateFloat4(m_original_background.refl_color);

			// our main background shader. With just this, and some color != black set we should get skylighting
			m_background.ins.Strength.Value = 1.0f;
			m_background.ins.Color.Value = color1;

			#region skylight disabler/enabler nodes

			// node to give highest value (used for light path checks)
			m_max.ins.Value1.Value = 0.0f;
			m_max.ins.Value2.Value = 0.0f;

			#endregion

			#region gradient bg nodes


			// just simple linear gradient
			m_colorramp.ColorBand.Interpolation = ColorBand.Interpolations.Linear;
			// bottom color on 0.0f
			m_colorramp.ColorBand.InsertColorStop(color2, 0.0f);
			// top color on 1.0f
			m_colorramp.ColorBand.InsertColorStop(color1, 1.0f);

			#endregion

			#region nodes for environment textures on bg/refl/skylight

			if (m_original_background.wallpaper.HasTextureImage)
			{
				RenderEngine.SetTextureImage(m_bg_env_texture, m_original_background.wallpaper);
			}
			else if (m_original_background.bg.HasTextureImage)
			{
				RenderEngine.SetTextureImage(m_bg_env_texture, m_original_background.bg);
			}

			if (m_original_background.refl.HasTextureImage)
			{
				RenderEngine.SetTextureImage(m_refl_env_texture, m_original_background.refl);
			}

			if (m_original_background.sky.HasTextureImage)
			{
				RenderEngine.SetTextureImage(m_sky_env_texture, m_original_background.sky);
			}

			m_sky_bg.ins.Color.Value = skycolor;
			m_sky_bg.ins.Strength.Value = 1.0f;
			
			m_refl_bg.ins.Color.Value = reflcolor;
			m_refl_bg.ins.Strength.Value = 1.0f;

			#endregion

			// add background nodes
			m_shader.AddNode(m_background);
			m_shader.AddNode(m_refl_bg);
			m_shader.AddNode(m_sky_bg);
			// add environment texture nodes
			m_shader.AddNode(m_bg_env_texture);
			m_shader.AddNode(m_refl_env_texture);
			m_shader.AddNode(m_sky_env_texture);

			// light paths
			m_shader.AddNode(m_lightpath);
			// a max for skylight stuff
			m_shader.AddNode(m_max);

			// two mixer nodes
			m_shader.AddNode(m_mix_bg_and_refl);
			m_shader.AddNode(m_mix_skylightswitch);


			// gradient bg nodes
			m_shader.AddNode(m_texture_coordinates);
			m_shader.AddNode(m_mapping);
			m_shader.AddNode(m_gradient);
			m_shader.AddNode(m_colorramp);

			// to control skylight influence, the trick is to only sample on camera ray and on
			// glossy rays. This way we can see the background in
			// reflections and when we're looking directly to it.
			// our max(v1,v2) will be 1.0 when either or both are set
			m_lightpath.outs.IsCameraRay.Connect(m_max.ins.Value1);
			m_lightpath.outs.IsGlossyRay.Connect(m_max.ins.Value2);
			// also connect glossy ray to mix_bg_refl, so we get refl_bg when glossy ray
			m_lightpath.outs.IsGlossyRay.Connect(m_mix_bg_and_refl.ins.Fac);

			m_max.outs.Value.Connect(m_mix_skylightswitch.ins.Fac);

			// if there is a bg texture, put that in bg color
			if (m_original_background.bg.HasTextureImage && m_original_background.background_fill == BackgroundStyle.Environment)
			{
				m_bg_env_texture.outs.Color.Connect(m_background.ins.Color);
			}
			// or if gradient fill is needed, so lets do that.
			else if (m_original_background.background_fill == BackgroundStyle.Gradient)
			{
				// gradient is 'screen-based', so use window tex coordinates
				m_texture_coordinates.outs.Window.Connect(m_mapping.ins.Vector);
				// rotate those coords into gradient
				m_mapping.outs.Vector.Connect(m_gradient.ins.Vector);

				// and finally into our color ramp
				m_gradient.outs.Fac.Connect(m_colorramp.ins.Fac);
				// now use that as background input
				m_colorramp.outs.Color.Connect(m_background.ins.Color);
			}
			else if (m_original_background.background_fill == BackgroundStyle.WallpaperImage)
			{
				m_bg_env_texture.outs.Color.Connect(m_background.ins.Color);
				m_bg_env_texture.Projection = TextureNode.EnvironmentProjection.Wallpaper;
			}

			// connect refl env texture if texture exists
			if (m_original_background.refl.HasTextureImage)
			{
				m_refl_env_texture.outs.Color.Connect(m_refl_bg.ins.Color);
			}
			// connect sky env texture if texture exists
			if (m_original_background.sky.HasTextureImage)
			{
				m_sky_env_texture.outs.Color.Connect(m_sky_bg.ins.Color);
			}

			if(m_original_background.HasSky && m_original_background.skylight_enabled) {
				m_sky_bg.outs.Background.Connect(m_mix_skylightswitch.ins.Closure1);
			}
			else
			{
				if (m_original_background.skylight_enabled)
				{
					m_mix_bg_and_refl.outs.Closure.Connect(m_mix_skylightswitch.ins.Closure1);
				}
			}

			// background always goes into closure 1 for bg+refl mix
			m_background.outs.Background.Connect(m_mix_bg_and_refl.ins.Closure1);

			// if we have a reflection color or texture, use that in
			// background and reflection mixer.
			if (m_original_background.HasRefl)
			{
				m_refl_bg.outs.Background.Connect(m_mix_bg_and_refl.ins.Closure2);
			}
			else // no color or texture for reflections, use regular background
			{
				m_background.outs.Background.Connect(m_mix_bg_and_refl.ins.Closure2);
			}

			// the bakground and reflection should always be connected to skylight
			// switch in second closure slot. This is so that direct background ray
			// hit from camera shows background. Also glossy ray still should be evaluated
			// to this one.
			m_mix_bg_and_refl.outs.Closure.Connect(m_mix_skylightswitch.ins.Closure2);

			m_mix_skylightswitch.outs.Closure.Connect(m_shader.Output.ins.Surface);

			// phew, done.
			m_shader.FinalizeGraph();
			m_shader.Tag();

			return m_shader;
		}

	}
}
