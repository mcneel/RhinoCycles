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
using RhinoCyclesCore.Core;

namespace RhinoCyclesCore.Shaders
{
	public abstract class RhinoShader
	{
		protected Shader m_shader;
		protected CyclesShader m_original;
		protected CyclesBackground m_original_background;
		protected CyclesLight m_original_light;

		protected Session m_session;

		private void InitShader(string name, Shader existing, bool recreate)
		{
			if (existing != null)
			{
				m_shader = existing;
				if(recreate) m_shader.Recreate();
			}
			else
			{
				m_shader = new Shader(m_session.Scene)
				{
					UseMis = true,
					UseTransparentShadow = true,
					HeterogeneousVolume = false,
					Name = name,
					Verbose = false
				};
			}

		}
		protected RhinoShader(Session session, CyclesShader intermediate, string name, Shader existing, bool recreate)
		{
			m_session = session;
			m_original = intermediate;
			if (m_original.Front != null) m_original.Front.Gamma = m_original.Gamma;
			if (m_original.Back != null) m_original.Back.Gamma = m_original.Gamma;
			InitShader(name, existing, recreate);

		}

		protected RhinoShader(Session session, CyclesBackground intermediateBackground, string name, Shader existing, bool recreate)
		{
			m_session = session;
			m_original_background = intermediateBackground;
			InitShader(name, existing, recreate);
		}

		protected RhinoShader(Session session, CyclesLight intermediateLight, string name, Shader existing, bool recreate)
		{
			m_session = session;
			m_original_light = intermediateLight;
			InitShader(name, existing, recreate);
		}

		public void Reset()
		{
			m_shader?.Recreate();
		}

		public static RhinoShader CreateRhinoMaterialShader(Session session, CyclesShader intermediate)
		{
			RhinoShader theShader = new RhinoFullNxt(session, intermediate);

			return theShader;
		}

		public static RhinoShader RecreateRhinoMaterialShader(Session session, CyclesShader intermediate, Shader existing)
		{
			RhinoShader theShader = new RhinoFullNxt(session, intermediate, existing);

			return theShader;
		}

		public static RhinoShader CreateRhinoBackgroundShader(Session session, CyclesBackground intermediateBackground, Shader existingShader)
		{
			RhinoShader theShader = new RhinoBackground(session, intermediateBackground, existingShader);
			return theShader;
		}

		public static RhinoShader CreateRhinoLightShader(Session session, CyclesLight intermediateLight, Shader existingShader)
		{
			RhinoShader shader = new RhinoLight(session, intermediateLight, existingShader);
			return shader;
		}

		/// <summary>
		/// Get the ccl.Shader representing this
		/// </summary>
		/// <returns></returns>
		public abstract Shader GetShader();
	}
}
