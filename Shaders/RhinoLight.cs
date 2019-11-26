/**
Copyright 2014-2017 Robert McNeel and Associates

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
	public class RhinoLight : RhinoShader
	{

		public RhinoLight(Client client, CyclesLight intermediate, Shader existing) : this(client, intermediate, existing, "light", true)
		{
		}

		public RhinoLight(Client client, CyclesLight intermediate, Shader existing, string name, bool recreate) : base(client, intermediate, name, existing, recreate)
		{
		}

		public override Shader GetShader()
		{
			var use_falloff =
				m_original_light.Type == LightType.Spot ||
				m_original_light.Type == LightType.Point ||
				m_original_light.Type == LightType.Area;

			var emnode = new EmissionNode();
			emnode.ins.Color.Value = m_original_light.DiffuseColor ^ m_original_light.Gamma;
			emnode.ins.Strength.Value = m_original_light.Strength;
			m_shader.AddNode(emnode);

			if (use_falloff)
			{
				var falloffnode = new LightFalloffNode();
				falloffnode.ins.Strength.Value = m_original_light.Strength;
				falloffnode.ins.Smooth.Value = 0.5f;

				m_shader.AddNode(falloffnode);

				switch(m_original_light.Falloff) {
					case CyclesLightFalloff.Constant:
						falloffnode.outs.Constant.Connect(emnode.ins.Strength);
						break;
					case CyclesLightFalloff.Linear:
						falloffnode.outs.Linear.Connect(emnode.ins.Strength);
						break;
					case CyclesLightFalloff.Quadratic:
						falloffnode.outs.Quadratic.Connect(emnode.ins.Strength);
						break;
				}

			}

			emnode.outs.Emission.Connect(m_shader.Output.ins.Surface);
			m_shader.FinalizeGraph();
			m_shader.Tag();

			return m_shader;
		}

	}
}
