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

using System;
using ccl;
using ccl.ShaderNodes;
using Rhino.Display;

namespace RhinoCycles.Shaders
{
	public class RhinoBackground : RhinoShader
	{

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
			var background = new BackgroundNode("m_shader bg");
			background.ins.Strength.Value = 1.0f;
			background.ins.Color.Value = color1;

			#region skylight disabler/enabler nodes

			var lightpath = new LightPathNode("lightpath");
			var mix_skylightswitch = new MixClosureNode("mix_skylightswitch");
			var mix_bg_and_refl = new MixClosureNode("mix_bg_and_refl");
			// node to give highest value (used for light path checks)
			var max = new MathNode("max")
			{
				Operation = MathNode.Operations.Maximum
			};
			max.ins.Value1.Value = 0.0f;
			max.ins.Value2.Value = 0.0f;

			#endregion

			#region gradient bg nodes

			// normal gradient texture (linear) has gradient from left to right
			// We want that to go along the window
			var texture_coordinates = new TextureCoordinateNode("texture_coordinates");
			// the mapping node should rotate the texture coordinate input 90 around
			// texture z axis (we're looking onto it)
			var mapping = new MappingNode("mapping")
			{
				Mapping = MappingNode.MappingType.Texture,
				Translation = black,
				Scale = RenderEngine.CreateFloat4(1.0f, 1.0f, 1.0f),
				Rotation = RenderEngine.CreateFloat4(0.0f, 0.0f, 1.570796f),
			};

			// the actual gradient node, used as factor for color ramp node
			var gradient = new GradientTextureNode
			{
				Gradient = GradientTextureNode.GradientType.Linear
			};

			// Add color ramp. Color stop on 1.0 is bottom color,
			// color stop on 0.0 is top color
			var colorramp = new ColorRampNode();
			// just simple linear gradient
			colorramp.ColorBand.Interpolation = ColorBand.Interpolations.Linear;
			// bottom color on 1.0f
			colorramp.ColorBand.InsertColorStop(color2, 1.0f);
			// top color on 0.0f
			colorramp.ColorBand.InsertColorStop(color1, 0.0f);

			#endregion

			#region nodes for environment textures on bg/refl/skylight

			var bg_env_texture = new EnvironmentTextureNode();
			if (m_original_background.bg.HasTextureImage)
			{
				RenderEngine.SetTextureImage(bg_env_texture, m_original_background.bg);
			}

			var refl_env_texture = new EnvironmentTextureNode();
			if (m_original_background.refl.HasTextureImage)
			{
				RenderEngine.SetTextureImage(refl_env_texture, m_original_background.refl);
			}

			var sky_env_texture = new EnvironmentTextureNode();
			if (m_original_background.sky.HasTextureImage)
			{
				RenderEngine.SetTextureImage(sky_env_texture, m_original_background.sky);
			}

			var sky_bg = new BackgroundNode("sky bg");
			sky_bg.ins.Color.Value = skycolor;
			sky_bg.ins.Strength.Value = 1.0f;
			
			var refl_bg = new BackgroundNode("refl bg");
			refl_bg.ins.Color.Value = reflcolor;
			refl_bg.ins.Strength.Value = 1.0f;

			#endregion

			// add background nodes
			m_shader.AddNode(background);
			m_shader.AddNode(refl_bg);
			m_shader.AddNode(sky_bg);
			// add environment texture nodes
			m_shader.AddNode(bg_env_texture);
			m_shader.AddNode(refl_env_texture);
			m_shader.AddNode(sky_env_texture);

			// light paths
			m_shader.AddNode(lightpath);
			// a max for skylight stuff
			m_shader.AddNode(max);

			// two mixer nodes
			m_shader.AddNode(mix_bg_and_refl);
			m_shader.AddNode(mix_skylightswitch);


			// gradient bg nodes
			m_shader.AddNode(texture_coordinates);
			m_shader.AddNode(mapping);
			m_shader.AddNode(gradient);
			m_shader.AddNode(colorramp);

			// to control skylight influence, the trick is to only sample on camera ray and on
			// glossy rays. This way we can see the background in
			// reflections and when we're looking directly to it.
			// our max(v1,v2) will be 1.0 when either or both are set
			lightpath.outs.IsCameraRay.Connect(max.ins.Value2);
			lightpath.outs.IsGlossyRay.Connect(max.ins.Value1);
			// also connect glossy ray to mix_bg_refl, so we get refl_bg when glossy ray
			lightpath.outs.IsGlossyRay.Connect(mix_bg_and_refl.ins.Fac);

			max.outs.Value.Connect(mix_skylightswitch.ins.Fac);

			// if there is a bg texture, put that in bg color
			if (m_original_background.bg.HasTextureImage && m_original_background.background_fill == BackgroundStyle.Environment)
			{
				bg_env_texture.outs.Color.Connect(background.ins.Color);
			}
			// or if gradient fill is needed, so lets do that.
			else if (m_original_background.background_fill == BackgroundStyle.Gradient)
			{
				// gradient is 'screen-based', so use window tex coordinates
				texture_coordinates.outs.Window.Connect(mapping.ins.Vector);
				// rotate those coords into gradient
				mapping.outs.Vector.Connect(gradient.ins.Vector);

				// and finally into our color ramp
				gradient.outs.Fac.Connect(colorramp.ins.Fac);
				// now use that as background input
				colorramp.outs.Color.Connect(background.ins.Color);
			}

			// connect refl env texture if texture exists
			if (m_original_background.refl.HasTextureImage)
			{
				refl_env_texture.outs.Color.Connect(refl_bg.ins.Color);
			}
			// connect sky env texture if texture exists
			if (m_original_background.sky.HasTextureImage)
			{
				sky_env_texture.outs.Color.Connect(sky_bg.ins.Color);
			}

			if(m_original_background.HasSky && m_original_background.skylight_enabled) {
				sky_bg.outs.Background.Connect(mix_skylightswitch.ins.Closure1);
			}
			else
			{
				if (m_original_background.skylight_enabled)
				{
					mix_bg_and_refl.outs.Closure.Connect(mix_skylightswitch.ins.Closure1);
				}
			}

			// background always goes into closure 1 for bg+refl mix
			background.outs.Background.Connect(mix_bg_and_refl.ins.Closure1);

			// if we have a reflection color or texture, use that in
			// background and reflection mixer.
			if (m_original_background.HasRefl)
			{
				refl_bg.outs.Background.Connect(mix_bg_and_refl.ins.Closure2);
			}
			else // no color or texture for reflections, use regular background
			{
				background.outs.Background.Connect(mix_bg_and_refl.ins.Closure2);
			}

			// the bakground and reflection should always be connected to skylight
			// switch in second closure slot. This is so that direct background ray
			// hit from camera shows background. Also glossy ray still should be evaluated
			// to this one.
			mix_bg_and_refl.outs.Closure.Connect(mix_skylightswitch.ins.Closure2);

			mix_skylightswitch.outs.Closure.Connect(m_shader.Output.ins.Surface);

			// phew, done.
			m_shader.FinalizeGraph();
			m_shader.Tag();

			return m_shader;
		}

	}
}
