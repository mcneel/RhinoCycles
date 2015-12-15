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
using System.Drawing;
using Rhino.Display;
using Rhino.Render;

namespace RhinoCycles
{
	/// <summary>
	/// Helper class to hold background/world shader related data and state
	/// </summary>
	public class CyclesBackground
	{
		/// <summary>
		/// True if ChangeQueue modified the background
		/// </summary>
		public bool modified;

		/// <summary>
		/// True if skylight is enabled
		/// </summary>
		public bool skylight_enabled;

		/// <summary>
		/// Get background style.
		/// </summary>
		public BackgroundStyle background_fill = BackgroundStyle.SolidColor;
		/// <summary>
		/// Solid color or top color for gradient
		/// </summary>
		public Color color1;
		/// <summary>
		/// Bottom color for gradient
		/// </summary>
		public Color color2;
		/// <summary>
		/// Environment used for background (360deg environment)
		/// </summary>
		public RenderEnvironment background_environment;
		/// <summary>
		/// Custom environment used for skylight. If not set, background_environment will be used.
		/// </summary>
		public RenderEnvironment skylight_environment;
		/// <summary>
		/// Custom environment to use for reflections. If not set, use background_environment,
		/// solid color or gradient, whichever is used as background
		/// </summary>
		public RenderEnvironment reflection_environment;

		/// <summary>
		/// Hold texture data for background (360deg)
		/// </summary>
		public CyclesTextureImage bg = new CyclesTextureImage();
		/// <summary>
		/// Background color, used if bg is no image
		/// </summary>
		public Color bg_color = Color.Empty;
		
		/// <summary>
		/// Hold texture data for skylight
		/// </summary>
		public CyclesTextureImage sky = new CyclesTextureImage();
		/// <summary>
		/// Sky color, used if sky has no image
		/// </summary>
		public Color sky_color = Color.Empty;

		public float gamma = 1.0f;

		/// <summary>
		/// True if skylight is used.
		/// </summary>
		public bool HasSky
		{
			get { return sky.HasTextureImage || sky_color != Color.Empty; }
		}

		/// <summary>
		/// Hold texture data for reflection
		/// </summary>
		public CyclesTextureImage refl = new CyclesTextureImage();
		/// <summary>
		/// Reflection color, used if refl has no image
		/// </summary>
		public Color refl_color = Color.Empty;

		/// <summary>
		/// True if custom reflection env is used
		/// </summary>
		public bool HasRefl
		{
			get { return refl.HasTextureImage || refl_color != Color.Empty; }
		}

		/// <summary>
		/// Read texture data and bg color from environments
		/// </summary>
		public void HandleEnvironments()
		{
			SimulatedEnvironment simenv;

			if (background_environment != null)
			{
				simenv = background_environment.SimulateEnvironment(true);
				if (simenv != null)
				{
					bg_color = simenv.BackgroundColor;
				}
			}
			else
			{
				bg_color = Color.Empty;
				bg.Clear();
			}
			BitmapConverter.EnvironmentBitmapFromEvaluator(background_environment, bg, gamma);

			bool resampled = false;
			if (skylight_environment != null)
			{
				simenv = skylight_environment.SimulateEnvironment(true);
				if (simenv != null)
				{
					sky_color = simenv.BackgroundColor;
				}

				var skylight_copy = skylight_environment.MakeCopy() as RenderEnvironment;
				if (skylight_copy != null)
				{
					var original_render_texture = skylight_copy.FindChild("texture");
					if (original_render_texture != null)
					{
						var resampler = RenderContentType.NewContentFromTypeId(new System.Guid("71D5FEEF-4144-4133-8C38-1EEF2BC851F1"));
						var render_texture = original_render_texture.MakeCopy() as RenderTexture;
						resampler.SetParameter("u-division-count", 64, RenderContent.ChangeContexts.Program);
						resampler.SetParameter("v-division-count", 64, RenderContent.ChangeContexts.Program);
						resampler.SetParameter("blur-on", true, RenderContent.ChangeContexts.Program);
						resampler.SetParameter("blur-radius", 0.2, RenderContent.ChangeContexts.Program);
						resampler.SetChild(render_texture, "texture", RenderContent.ChangeContexts.Program);
						skylight_copy.SetChild(resampler, "texture", RenderContent.ChangeContexts.Program);


						BitmapConverter.EnvironmentBitmapFromEvaluator(skylight_copy, sky, gamma);
						resampled = true;

						render_texture.Dispose();
						resampler.Dispose();
					}
					skylight_copy.Dispose();
				}
			}
			else
			{
				sky_color = Color.Empty;
				sky.Clear();
			}
			if(!resampled)
				BitmapConverter.EnvironmentBitmapFromEvaluator(skylight_environment, sky, gamma);

			if (reflection_environment != null)
			{
				simenv = reflection_environment.SimulateEnvironment(true);
				if (simenv != null)
				{
					refl_color = simenv.BackgroundColor;
				}
			}
			else
			{
				refl_color = Color.Empty;
				refl.Clear();
			}
			BitmapConverter.EnvironmentBitmapFromEvaluator(reflection_environment, refl, gamma);
		}

		/// <summary>
		/// Reset modified flag.
		/// </summary>
		public void Reset()
		{
			modified = false;
		}
	}
}
