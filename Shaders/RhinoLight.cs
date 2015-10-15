using ccl;
using ccl.ShaderNodes;

namespace RhinoCycles.Shaders
{
	public class RhinoLight : RhinoShader
	{

		public RhinoLight(Client client, CyclesLight intermediate, Shader existing) : this(client, intermediate, existing, "light")
		{
		}

		public RhinoLight(Client client, CyclesLight intermediate, Shader existing, string name) : base(client, intermediate)
		{
			if (existing != null)
			{
				m_shader = existing;
				m_shader.Recreate();
			}
			else
			{
				m_shader = new Shader(m_client, Shader.ShaderType.Material)
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
			var use_falloff = m_original_light.Type == LightType.Spot || m_original_light.Type == LightType.Point || m_original_light.Type == LightType.Area;

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

				falloffnode.outs.Constant.Connect(emnode.ins.Strength);
			}

			emnode.outs.Emission.Connect(m_shader.Output.ins.Surface);
			m_shader.FinalizeGraph();
			m_shader.Tag();

			return m_shader;
		}

	}
}
