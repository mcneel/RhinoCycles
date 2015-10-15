using ccl;

namespace RhinoCycles.Shaders
{
	public abstract class RhinoShader
	{
		protected Shader m_shader;
		protected CyclesShader m_original;
		protected CyclesBackground m_original_background;
		protected CyclesLight m_original_light;

		protected Client m_client;
		protected RhinoShader(Client client, CyclesShader intermediate)
		{
			m_client = client;
			m_original = intermediate;
		}

		protected RhinoShader(Client client, CyclesBackground intermediateBackground)
		{
			m_client = client;
			m_original_background = intermediateBackground;
		}

		protected RhinoShader(Client client, CyclesLight intermediateLight)
		{
			m_client = client;
			m_original_light = intermediateLight;
		}

		public void Reset()
		{
			if (m_shader != null)
			{
				m_shader.Recreate();
			}
		}

		public static RhinoShader CreateRhinoMaterialShader(Client client, CyclesShader intermediate)
		{
			RhinoShader theShader = new RhinoFullNxt(client, intermediate);

			return theShader;
		}

		public static RhinoShader RecreateRhinoMaterialShader(Client client, CyclesShader intermediate, Shader existing)
		{
			RhinoShader theShader = new RhinoFullNxt(client, intermediate, existing);

			return theShader;
		}

		public static RhinoShader CreateRhinoBackgroundShader(Client client, CyclesBackground intermediateBackground, Shader existingShader)
		{
			RhinoShader theShader = new RhinoBackground(client, intermediateBackground, existingShader);
			return theShader;
		}

		public static RhinoShader CreateRhinoLightShader(Client client, CyclesLight intermediateLight, Shader existingShader)
		{
			RhinoShader shader = new RhinoLight(client, intermediateLight, existingShader);
			return shader;
		}

		/// <summary>
		/// Get the ccl.Shader representing this
		/// </summary>
		/// <returns></returns>
		public abstract Shader GetShader();
	}
}
