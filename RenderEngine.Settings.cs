/**
Copyright 2014-2024 Robert McNeel and Associates

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

using RhinoCyclesCore.Core;
using RhinoCyclesCore.Settings;
using System.Drawing;

namespace RhinoCyclesCore
{
	partial class RenderEngine
	{
		/// <summary>
		/// Get or set the resolution for rendering.
		/// </summary>
		public Size RenderDimension { get; set; }

		private int _pixelSize = 1;

		/// <summary>
		/// Get or set the pixel size.
		/// </summary>
		public int PixelSize
		{
			get { return _pixelSize; }
			set { _pixelSize = value >= 1 ? value : 1; }
		}

		public enum AdvancedSettings
		{
			Seed,
			Samples,
			MaxBounce,
			MaxDiffuseBounce,
			MaxGlossyBounce,
			MaxTransmissionBounce,
			MaxVolumeBounce,
			TransparentMaxBounce,
		}

		/// <summary>
		/// The amount of threads requested for raytracing (on Cpu).
		/// </summary>
		public int ThreadCount { get; set; } = 0;
		private uint _oldIntegratorHash = 0;
		/// <summary>
		/// The render device set to this render engine implementation.
		/// </summary>
		public ccl.Device RenderDevice { get; set; }
		/// <summary>
		/// True if the render device we wanted to use wasn't ready. Instead we're using the
		/// fallback device
		/// </summary>
		public bool IsFallbackRenderDevice { get; set; } = false;

		/// <summary>
		/// Sleep in ms between each pass
		/// </summary>
		protected int _throttle { get; set; } = 30;
		/// <summary>
		/// Maximum samples this session will render
		/// </summary>
		public int MaxSamples { get; set; } = 1;

		protected bool _needReset { get; set; }

		public int _textureBakeQuality { get; set; } = 0;

		public void HandleIntegrator(IAllSettings settings)
		{
			if (Session != null && Session.Scene != null)
			{
				var hash = settings.IntegratorHash;
				if (hash != _oldIntegratorHash)
				{
					var integrator = Session.Scene.Integrator;
					integrator.Seed = settings.Seed;
					integrator.MaxBounce = settings.MaxBounce;
					integrator.MaxDiffuseBounce = settings.MaxDiffuseBounce;
					integrator.MaxGlossyBounce = settings.MaxGlossyBounce;
					integrator.MaxTransmissionBounce = settings.MaxTransmissionBounce;
					integrator.MaxVolumeBounce = settings.MaxVolumeBounce;
					integrator.TagForUpdate();
					_needReset = true;
					_oldIntegratorHash = hash;
				}
				Session.SetSamples(settings.Samples);
			}
		}

		public void HandleDevice(IAllSettings settings)
		{
			
			if(Rhino.RhinoApp.IsSafeModeEnabled)
			{
				RenderDevice = ccl.Device.Default;
			} else
			{
				var device = RcCore.It.IsDeviceReady(settings.RenderDevice);
				RenderDevice = device.actualDevice;
				IsFallbackRenderDevice = !device.isDeviceReady;
			}
			_textureBakeQuality = settings.TextureBakeQuality;
			_throttle = settings.ThrottleMs;
			MaxSamples = settings.Samples;
			if (Session != null && Session.Scene != null)
			{
				var hash = settings.IntegratorHash;
				if (hash != _oldIntegratorHash)
				{
					var integrator = Session.Scene.Integrator;
					integrator.Seed = settings.Seed;
					integrator.MaxBounce = settings.MaxBounce;
					integrator.MaxDiffuseBounce = settings.MaxDiffuseBounce;
					integrator.MaxGlossyBounce = settings.MaxGlossyBounce;
					integrator.MaxTransmissionBounce = settings.MaxTransmissionBounce;
					integrator.MaxVolumeBounce = settings.MaxVolumeBounce;
					integrator.TagForUpdate();
					_needReset = true;
					_oldIntegratorHash = hash;
				}
				Session.SetSamples(settings.Samples);
			}
		}
	}
}
