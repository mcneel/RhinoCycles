/**
Copyright 2014-2021 Robert McNeel and Associates

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

namespace RhinoCyclesCore.Shaders
{
	public class RhinoNotYetImplemented : RhinoShader
	{

		/// <summary>
		/// Color mixer for wave and voronoi color. Fac is driven by whichever fac output
		/// of wav and voronoi textures is largest.
		/// </summary>
		private readonly MixNode m_mix_wave_and_voronoi = new MixNode("mix_wave_and_voronoi");
		/// <summary>
		/// Diffuse BSDF for outputting max(Wave,Voronoi);
		/// </summary>
		private readonly DiffuseBsdfNode diffuse_bsdf = new DiffuseBsdfNode("diffuse_bsdf");
		/// <summary>
		/// Voronoi texture with Cell coloring
		/// </summary>
		private readonly VoronoiTexture vor = new VoronoiTexture {Dimension = VoronoiTexture.Dimensions.D3};

		/// <summary>
		/// Wave output with type Rings
		/// </summary>
		private readonly WaveTexture wav = new WaveTexture {WaveType = WaveTexture.WaveTypes.Rings };
		/// <summary>
		/// Mathnode doing max(Wave factor, Voronoi factor)
		/// </summary>
		private readonly MathNode max = new MathNode("max") {Operation = MathNode.Operations.Maximum};

		/// <summary>
		/// Texture coordinate input node for driving UV
		/// </summary>
		private readonly TextureCoordinateNode texture_uv = new TextureCoordinateNode("texture_uv");

		/// <summary>
		/// Create a new shader, using intermediate.Name as name
		/// </summary>
		/// <param name="client"></param>
		/// <param name="intermediate"></param>
		public RhinoNotYetImplemented(Client client, CyclesShader intermediate) : this(client, intermediate, intermediate.Front.Name)
		{
		}

		/// <summary>
		/// Create a new shader, with name overriding intermediate.Name
		/// </summary>
		/// <param name="client"></param>
		/// <param name="intermediate"></param>
		/// <param name="name"></param>
		public RhinoNotYetImplemented(Client client, CyclesShader intermediate, string name) : base(client, intermediate, name, null, true)
		{
		}

		public override Shader GetShader()
		{
			// voronoi scale
			vor.ins.Scale.Value = 5.0f;
			// wave scale
			wav.ins.Scale.Value = 5.0f;

			// add the nodes
			m_shader.AddNode(vor);
			m_shader.AddNode(wav);
			m_shader.AddNode(diffuse_bsdf);
			m_shader.AddNode(m_mix_wave_and_voronoi);
			m_shader.AddNode(texture_uv);
			m_shader.AddNode(max);

			// use UV texture coordinates
			texture_uv.outs.UV.Connect(vor.ins.Vector);
			texture_uv.outs.UV.Connect(wav.ins.Vector);

			// inputs to max(wav.fac, vor.fac)
			wav.outs.Fac.Connect(max.ins.Value1);
			vor.outs.Distance.Connect(max.ins.Value2);

			// drive wave + voronoi color mixing with max(wav.fac, vor.fac)
			max.outs.Value.Connect(m_mix_wave_and_voronoi.ins.Fac);
			wav.outs.Color.Connect(m_mix_wave_and_voronoi.ins.Color1);
			vor.outs.Color.Connect(m_mix_wave_and_voronoi.ins.Color2);

			// use color output for diffuse BSDF
			m_mix_wave_and_voronoi.outs.Color.Connect(diffuse_bsdf.ins.Color);

			// plug BSDF into material surface
			diffuse_bsdf.outs.BSDF.Connect(m_shader.Output.ins.Surface);

			// done
			m_shader.FinalizeGraph();

			return m_shader;
		}
	}
}
