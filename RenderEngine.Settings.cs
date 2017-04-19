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
		public enum IntegratorSetting
		{
			Seed,
			DiffuseSamples,
			GlossySamples,
			TransmissionSamples,
			MinBounce,
			MaxBounce,
			MaxDiffuseBounce,
			MaxGlossyBounce,
			MaxTransmissionBounce,
			MaxVolumeBounce,
		}

		public event EventHandler CurrentViewportSettingsRequested;
		protected void TriggerCurrentViewportSettingsRequested()
		{
			CurrentViewportSettingsRequested?.Invoke(this, EventArgs.Empty);
		}

		private uint _oldIntegratorHash = 0;
		protected ccl.Device RenderDevice { get; set; }
		public void ViewportSettingsChangedHandler(object sender, ViewportSettingsChangedArgs e)
		{
			if(e.Settings.AllowSelectedDeviceOverride)
			{
				RenderDevice = ccl.Device.DeviceFromString(e.Settings.SelectedDeviceStr);
			}
			else
			{
				RenderDevice = ccl.Device.DeviceFromString(RcCore.It.EngineSettings.SelectedDeviceStr);
			}
			if (Session != null && Session.Scene != null)
			{
				var hash = e.Settings.IntegratorHash;
				if (hash != _oldIntegratorHash)
				{
					var integrator = Session.Scene.Integrator;
					integrator.Seed = e.Settings.Seed;
					integrator.DiffuseSamples = e.Settings.DiffuseSamples;
					integrator.GlossySamples = e.Settings.GlossySamples;
					integrator.MinBounce = e.Settings.MinBounce;
					integrator.MaxBounce = e.Settings.MaxBounce;
					integrator.MaxDiffuseBounce = e.Settings.MaxDiffuseBounce;
					integrator.MaxGlossyBounce = e.Settings.MaxGlossyBounce;
					integrator.MaxTransmissionBounce = e.Settings.MaxTransmissionBounce;
					integrator.MaxVolumeBounce = e.Settings.MaxVolumeBounce;
					integrator.TagForUpdate();
					_needReset = true;
					_oldIntegratorHash = hash;
				}
				Session.SetSamples(e.Settings.Samples);
				_throttle = e.Settings.ThrottleMs;
				_samples = e.Settings.Samples;
			}
		}

		protected int _throttle = 10;
		protected int _samples = 1;

		protected bool _needReset;
	}
}
