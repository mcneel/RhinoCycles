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

namespace RhinoCyclesCore.Shaders
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
			m_shader?.Recreate();
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
