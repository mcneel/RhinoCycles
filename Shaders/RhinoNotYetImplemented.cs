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
		/// Create a new shader, using intermediate.Name as name
		/// </summary>
		/// <param name="client"></param>
		/// <param name="intermediate"></param>
		public RhinoNotYetImplemented(Session client, CyclesShader intermediate) : this(client, intermediate, intermediate.Front.Name)
		{
		}

		/// <summary>
		/// Create a new shader, with name overriding intermediate.Name
		/// </summary>
		/// <param name="client"></param>
		/// <param name="intermediate"></param>
		/// <param name="name"></param>
		public RhinoNotYetImplemented(Session client, CyclesShader intermediate, string name) : base(client, intermediate, name, null, true)
		{
		}

		public override Shader GetShader()
		{
			DiffuseBsdfNode diffuse_bsdf = new DiffuseBsdfNode(m_shader, "nyi_diffuse");
			// voronoi scale
			m_shader.AddNode(diffuse_bsdf);
			// plug BSDF into material surface
			diffuse_bsdf.outs.BSDF.Connect(m_shader.Output.ins.Surface);

			// done
			m_shader.FinalizeGraph();

			return m_shader;
		}
	}
}
