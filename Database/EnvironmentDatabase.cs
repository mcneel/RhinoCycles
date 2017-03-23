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

using System;
using System.Globalization;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using RhinoCyclesCore.Shaders;
using sd = System.Drawing;

namespace RhinoCyclesCore.Database
{
	public class EnvironmentDatabase : IDisposable
	{
		/// <summary>
		/// record background shader changes to push to cycles
		/// note that we have only one object that gets updated when necessary.
		/// </summary>
		private readonly CyclesBackground _cqBackground = new CyclesBackground();

		public void Dispose()
		{
			_cqBackground.Dispose();
		}

		/// <summary>
		/// OpenCL doesn't properly support HDRi textures in the environment,
		/// so read them as byte textures instead.
		/// @todo remove as obsolete
		/// </summary>
		public void SetFloatTextureAsByteTexture(bool floatAsByte)
		{
			_cqBackground.m_float_as_byte = false;
		}

		public CyclesBackground CyclesShader => _cqBackground;

		public RhinoShader CurrentBackgroundShader { get; set; }

		/// <summary>
		/// Set whether skylight is enabled or not. This will mark background as
		/// modified if it is different from old state.
		/// </summary>
		/// <param name="enable">Set to true if enabled.</param>
		public void SetSkylightEnabled(bool enable)
		{
			_cqBackground.modified |= _cqBackground.skylight_enabled != enable;
			_cqBackground.skylight_enabled = enable;
		}

		/// <summary>
		/// Set gamma value to use in background shader creation.
		/// </summary>
		/// <param name="gamma"></param>
		public void SetGamma(float gamma)
		{
			_cqBackground.Gamma = gamma;
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

			mod |= _cqBackground.background_fill != backgroundStyle;
			_cqBackground.background_fill = backgroundStyle;
			mod |= !_cqBackground.color1.Equals(color1);
			_cqBackground.color1 = color1;
			mod |= !_cqBackground.color2.Equals(color2);
			_cqBackground.color2 = color2;

			_cqBackground.modified |= mod;
		}

		public void BackgroundWallpaper(ViewInfo view, bool scaleToFit)
		{
			_cqBackground.HandleWallpaper(view, scaleToFit);
		}

		public void BackgroundWallpaper(ViewInfo view)
		{
			_cqBackground.HandleWallpaper(view);
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
					if (environment?.RenderHash == _cqBackground.background_environment?.RenderHash) return;
					_cqBackground.Xml = "";
					var xmlenv = (environment?.TopLevelParent as Materials.ICyclesMaterial);
					if(xmlenv?.MaterialType == RhinoCyclesCore.ShaderBody.CyclesMaterial.XmlEnvironment || xmlenv?.MaterialType == RhinoCyclesCore.ShaderBody.CyclesMaterial.SimpleNoiseEnvironment)
					{
						xmlenv.BakeParameters();
						_cqBackground.Xml = xmlenv.MaterialXml;
					}
					_cqBackground.background_environment = environment;
					if (environment != null)
					{
						var s = environment.GetParameter("background-projection") as IConvertible;
						var proj = Convert.ToString(s, CultureInfo.InvariantCulture);
						_cqBackground.PlanarProjection = proj.Equals("planar");
					} else
					{
						_cqBackground.PlanarProjection = false;
					}
					break;
				case RenderEnvironment.Usage.Skylighting:
					_cqBackground.skylight_environment = environment;
					break;
				case RenderEnvironment.Usage.ReflectionAndRefraction:
					_cqBackground.reflection_environment = environment;
					break;
			}
			_cqBackground.modified = true;
		}

		public void HandleEnvironments(RenderEnvironment.Usage usage)
		{
			_cqBackground.HandleEnvironments(usage);
		}

		/// <summary>
		/// True if background has changed.
		/// </summary>
		public bool BackgroundHasChanged => _cqBackground.modified;

		/// <summary>
		/// Reset background changes.
		/// </summary>
		public void ResetBackgroundChangeQueue()
		{
			_cqBackground.Reset();
		}
	}
}
