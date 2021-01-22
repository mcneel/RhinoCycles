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

using RhinoCyclesCore.Core;
using RhinoCyclesCore.Settings;
using System;
using System.Drawing;

namespace RhinoCyclesCore
{
	partial class RenderEngine
	{
		/// <summary>
		/// Get or set the resolution for rendering.
		/// </summary>
		public Size RenderDimension { get; set; }
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

		public int ThreadCount { get; set; } = 0;
		private uint _oldIntegratorHash = 0;
		public ccl.Device RenderDevice { get; set; }

		protected int _throttle { get; set; } = 10;
		protected int _samples { get; set; } = 1;

		protected bool _needReset;

		public int _textureBakeQuality { get; set; } = 0;

		public void HandleDeviceAndIntegrator(IAllSettings settings)
		{
			RenderDevice = settings.RenderDevice;
			_textureBakeQuality = settings.TextureBakeQuality;
			_throttle = settings.ThrottleMs;
			_samples = settings.Samples;
			if (Session != null && Session.Scene != null)
			{
				var hash = settings.IntegratorHash;
				if (hash != _oldIntegratorHash)
				{
					var integrator = Session.Scene.Integrator;
					integrator.Seed = settings.Seed;
					integrator.DiffuseSamples = settings.DiffuseSamples;
					integrator.GlossySamples = settings.GlossySamples;
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
