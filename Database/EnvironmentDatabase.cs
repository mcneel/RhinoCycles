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

using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using RhinoCyclesCore.Shaders;
using sd = System.Drawing;

namespace RhinoCyclesCore.Database
{
	public class EnvironmentDatabase
	{
		/// <summary>
		/// record background shader changes to push to cycles
		/// note that we have only one object that gets updated when necessary.
		/// </summary>
		private readonly CyclesBackground m_cq_background = new CyclesBackground();

		public CyclesBackground CyclesShader
		{
			get
			{
				return m_cq_background;
			}
		}

		private RhinoShader m_current_background_shader;

		public RhinoShader CurrentBackgroundShader
		{
			get
			{
				return m_current_background_shader;
			}
			set
			{
				m_current_background_shader = value;
			}
		}

		/// <summary>
		/// Set whether skylight is enabled or not. This will mark background as
		/// modified if it is different from old state.
		/// </summary>
		/// <param name="enable">Set to true if enabled.</param>
		public void SetSkylightEnabled(bool enable)
		{
			m_cq_background.modified |= m_cq_background.skylight_enabled != enable;
			m_cq_background.skylight_enabled = enable;
		}

		/// <summary>
		/// Set gamma value to use in background shader creation.
		/// </summary>
		/// <param name="gamma"></param>
		public void SetGamma(float gamma)
		{
			m_cq_background.gamma = gamma;
		}

		/// <summary>
		/// Set background style and gradient colors.
		///
		/// Mark background as modified if any given parameter differs from original.
		/// </summary>
		/// <param name="backgroundStyle"></param>
		/// <param name="color1"></param>
		/// <param name="color2"></param>
		public void SetBackgroundData(BackgroundStyle backgroundStyle, sd.Color color1, sd.Color color2)
		{

			var mod = false;

			mod |= m_cq_background.background_fill != backgroundStyle;
			m_cq_background.background_fill = backgroundStyle;
			mod |= !m_cq_background.color1.Equals(color1);
			m_cq_background.color1 = color1;
			mod |= !m_cq_background.color2.Equals(color2);
			m_cq_background.color2 = color2;

			m_cq_background.modified |= mod;
		}

		public void BackgroundWallpaper(ViewInfo view, bool scaleToFit)
		{
			m_cq_background.HandleWallpaper(view, scaleToFit);
		}

		public void BackgroundWallpaper(ViewInfo view)
		{
			m_cq_background.HandleWallpaper(view);
		}

		/// <summary>
		/// Set the RenderEnvironment for usage
		/// </summary>
		/// <param name="environment"></param>
		/// <param name="usage"></param>
		public void SetBackground(RenderEnvironment environment, RenderEnvironment.Usage usage)
		{
			switch (usage)
			{
				case RenderEnvironment.Usage.Background:
					m_cq_background.background_environment = environment;
					break;
				case RenderEnvironment.Usage.Skylighting:
					m_cq_background.skylight_environment = environment;
					break;
				case RenderEnvironment.Usage.ReflectionAndRefraction:
					m_cq_background.reflection_environment = environment;
					break;
			}

			m_cq_background.HandleEnvironments();

			m_cq_background.modified = true;
		}

		/// <summary>
		/// True if background has changed.
		/// </summary>
		public bool BackgroundHasChanged
		{
			get { return m_cq_background.modified; }
		}

		/// <summary>
		/// Reset background changes.
		/// </summary>
		public void ResetBackgroundChangeQueue()
		{
			m_cq_background.Reset();
		}
	}
}
