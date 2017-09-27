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
using Rhino.DocObjects.Custom;
using System.Runtime.InteropServices;
using RhinoCyclesCore.Core;
using RhinoCyclesCore;
using Rhino;

namespace RhinoCycles.Settings
{
	[Guid("CAE9D284-03B0-4D1C-AA46-B431BC9A7EA2")]
	public class ViewportSettings : UserDictionary, IViewportSettings
	{
		public ViewportSettings()
		{
			Dictionary.Version = 1;
			Dictionary.Name = "Cycles viewport-specific settings";
		}

		protected override void OnDuplicate(UserData source)
		{
			var src = source as ViewportSettings;
			CopyFrom(src);
		}

		public void CopyFrom(IViewportSettings src)
		{
			if (src != null)
			{
				SelectedDeviceStr = src.SelectedDeviceStr;
				Samples = src.Samples;
				ThrottleMs = src.ThrottleMs;
				Seed = src.Seed;
				TileX = src.TileX;
				TileY = src.TileY;
				DiffuseSamples = src.DiffuseSamples;
				GlossySamples = src.GlossySamples;
				TransmissionSamples = src.TransmissionSamples;
				MaxBounce = src.MaxBounce;
				MaxDiffuseBounce = src.MaxDiffuseBounce;
				MaxGlossyBounce = src.MaxGlossyBounce;
				MaxVolumeBounce = src.MaxVolumeBounce;
				MaxTransmissionBounce = src.MaxTransmissionBounce;
			}
		}
		public virtual uint IntegratorHash
		{
			get
			{
				uint rem = 0xdead_beef;
				rem = RhinoMath.CRC32(rem, Seed);
				rem = RhinoMath.CRC32(rem, DiffuseSamples);
				rem = RhinoMath.CRC32(rem, GlossySamples);
				rem = RhinoMath.CRC32(rem, TransmissionSamples);
				rem = RhinoMath.CRC32(rem, MaxBounce);
				rem = RhinoMath.CRC32(rem, MaxDiffuseBounce);
				rem = RhinoMath.CRC32(rem, MaxGlossyBounce);
				rem = RhinoMath.CRC32(rem, MaxVolumeBounce);
				rem = RhinoMath.CRC32(rem, MaxTransmissionBounce);

				return rem;
			}
		}

		public bool AllowSelectedDeviceOverride => RcCore.It.EngineSettings.AllowSelectedDeviceOverride;

		private bool UseThis => RcCore.It.EngineSettings.AllowViewportSettingsOverride;

		public string SelectedDeviceStr
		{
			get { return UseThis && AllowSelectedDeviceOverride ? Dictionary.GetString("SelectedDevice", RcCore.It.EngineSettings.SelectedDeviceStr) : RcCore.It.EngineSettings.SelectedDeviceStr; }
			set { Dictionary.Set("SelectedDevice", value); }
		}

		public string IntermediateSelectedDeviceStr
		{
			get { return Dictionary.GetString("IntermediateSelectedDevice", SelectedDeviceStr); }
			set { Dictionary.Set("IntermediateSelectedDevice", value); }
		}

		public bool UseStartResolution
		{
			get { return Dictionary.GetBool("UseStartResolution", RcCore.It.EngineSettings.UseStartResolution); }
			set { Dictionary.Set("UseStartResolution", value); }
		}

		public int StartResolution
		{
			get { return Dictionary.GetInteger("StartResolution", RcCore.It.EngineSettings.StartResolution); }
			set { Dictionary.Set("StartResolution", value); }
		}

		public ccl.Device RenderDevice
		{
			get
			{
				if(!ccl.Device.IsValidDeviceString(SelectedDeviceStr))
				{
					SelectedDeviceStr = $"{ccl.Device.FirstGpu.Id}";
				}
				return ccl.Device.DeviceFromString(SelectedDeviceStr);
			}
		}

		public int Samples
		{
			get { return UseThis ? Dictionary.GetInteger("Samples", RcCore.It.EngineSettings.Samples) : RcCore.It.EngineSettings.Samples; }
			set { Dictionary.Set("Samples", value); }
		}

		public int ThrottleMs
		{
			get { return UseThis ? Dictionary.GetInteger("ThrottleMs", RcCore.It.EngineSettings.ThrottleMs) : RcCore.It.EngineSettings.ThrottleMs; }
			set { Dictionary.Set("ThrottleMs", value); }
		}

		public int Seed
		{
			get { return UseThis ? Dictionary.GetInteger("Seed", RcCore.It.EngineSettings.Seed) : RcCore.It.EngineSettings.Seed; }
			set { Dictionary.Set("Seed", value); }
		}

		public int TileX
		{
			get { return UseThis ? Dictionary.GetInteger("TileX", RcCore.It.EngineSettings.TileX) : RcCore.It.EngineSettings.TileX; }
			set { Dictionary.Set("TileX", value); }
		}

		public int TileY
		{
			get { return UseThis ? Dictionary.GetInteger("TileY", RcCore.It.EngineSettings.TileY) : RcCore.It.EngineSettings.TileY; }
			set { Dictionary.Set("TileY", value); }
		}

		public int DiffuseSamples
		{
			get { return UseThis ? Dictionary.GetInteger("DiffuseSamples", RcCore.It.EngineSettings.DiffuseSamples) : RcCore.It.EngineSettings.DiffuseSamples; }
			set { Dictionary.Set("DiffuseSamples", value); }
		}

		public int GlossySamples
		{
			get { return UseThis ? Dictionary.GetInteger("GlossySamples", RcCore.It.EngineSettings.GlossySamples) : RcCore.It.EngineSettings.GlossySamples; }
			set { Dictionary.Set("GlossySamples", value); }
		}

		public int TransmissionSamples
		{
			get { return UseThis ? Dictionary.GetInteger("TransmissionSamples", RcCore.It.EngineSettings.TransmissionSamples) : RcCore.It.EngineSettings.TransmissionSamples; }
			set { Dictionary.Set("TransmissionSamples", value); }
		}

		public int MaxBounce
		{
			get { return UseThis ? Dictionary.GetInteger("MaxBounce", RcCore.It.EngineSettings.MaxBounce) : RcCore.It.EngineSettings.MaxBounce; }
			set { Dictionary.Set("MaxBounce", value); }
		}

		public int MaxDiffuseBounce
		{
			get { return UseThis ? Dictionary.GetInteger("MaxDiffusebounce", RcCore.It.EngineSettings.MaxDiffuseBounce) : RcCore.It.EngineSettings.MaxDiffuseBounce; }
			set { Dictionary.Set("MaxDiffusebounce", value); }
		}

		public int MaxGlossyBounce
		{
			get { return UseThis ? Dictionary.GetInteger("MaxGlossybounce", RcCore.It.EngineSettings.MaxGlossyBounce) : RcCore.It.EngineSettings.MaxGlossyBounce; }
			set { Dictionary.Set("MaxGlossybounce", value); }
		}

		public int MaxVolumeBounce
		{
			get { return UseThis ? Dictionary.GetInteger("MaxVolumebounce", RcCore.It.EngineSettings.MaxVolumeBounce) : RcCore.It.EngineSettings.MaxVolumeBounce; }
			set { Dictionary.Set("MaxVolumebounce", value); }
		}

		public int MaxTransmissionBounce
		{
			get { return UseThis ? Dictionary.GetInteger("MaxTransmissionbounce", RcCore.It.EngineSettings.MaxTransmissionBounce) : RcCore.It.EngineSettings.MaxTransmissionBounce; }
			set { Dictionary.Set("MaxTransmissionbounce", value); }
		}
		public int TransparentMaxBounce
		{
			get { return UseThis ? Dictionary.GetInteger("TransparentMaxbounce", RcCore.It.EngineSettings.TransparentMaxBounce) : RcCore.It.EngineSettings.TransparentMaxBounce; }
			set { Dictionary.Set("TransparentMaxbounce", value); }
		}

		public int PixelSize
		{
			get { return UseThis ? Dictionary.GetInteger("PixelSize", RcCore.It.EngineSettings.PixelSize) : RcCore.It.EngineSettings.PixelSize; }
			set { Dictionary.Set("PixelSize", value); }
		}
	}
}
