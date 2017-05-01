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
				MinBounce = src.MinBounce;
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
				rem = RhinoMath.CRC32(rem, MinBounce);
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
			get { return UseThis && AllowSelectedDeviceOverride ? Dictionary.GetString("selecteddevice", RcCore.It.EngineSettings.SelectedDeviceStr) : RcCore.It.EngineSettings.SelectedDeviceStr; }
			set { Dictionary.Set("selecteddevice", value); }
		}

		public string IntermediateSelectedDeviceStr
		{
			get { return Dictionary.GetString("intermediateselecteddevice", SelectedDeviceStr); }
			set { Dictionary.Set("intermediateselecteddevice", value); }
		}

		public bool UseStartResolution
		{
			get { return Dictionary.GetBool("usestartresolution", RcCore.It.EngineSettings.UseStartResolution); }
			set { Dictionary.Set("usestartresolution", value); }
		}

		public int StartResolution
		{
			get { return Dictionary.GetInteger("startresolution", RcCore.It.EngineSettings.StartResolution); }
			set { Dictionary.Set("startresolution", value); }
		}

		public ccl.Device RenderDevice
		{
			get
			{
				return ccl.Device.DeviceFromString(SelectedDeviceStr);
			}
		}

		public int Samples
		{
			get { return UseThis ? Dictionary.GetInteger("samples", RcCore.It.EngineSettings.Samples) : RcCore.It.EngineSettings.Samples; }
			set { Dictionary.Set("samples", value); }
		}

		public int ThrottleMs
		{
			get { return UseThis ? Dictionary.GetInteger("throttlems", RcCore.It.EngineSettings.ThrottleMs) : RcCore.It.EngineSettings.ThrottleMs; }
			set { Dictionary.Set("throttlems", value); }
		}

		public int Seed
		{
			get { return UseThis ? Dictionary.GetInteger("seed", RcCore.It.EngineSettings.Seed) : RcCore.It.EngineSettings.Seed; }
			set { Dictionary.Set("seed", value); }
		}

		public int TileX
		{
			get { return UseThis ? Dictionary.GetInteger("tilex", RcCore.It.EngineSettings.TileX) : RcCore.It.EngineSettings.TileX; }
			set { Dictionary.Set("tilex", value); }
		}

		public int TileY
		{
			get { return UseThis ? Dictionary.GetInteger("tilex", RcCore.It.EngineSettings.TileY) : RcCore.It.EngineSettings.TileY; }
			set { Dictionary.Set("tilex", value); }
		}

		public int DiffuseSamples
		{
			get { return UseThis ? Dictionary.GetInteger("diffusesamples", RcCore.It.EngineSettings.DiffuseSamples) : RcCore.It.EngineSettings.DiffuseSamples; }
			set { Dictionary.Set("diffusesamples", value); }
		}

		public int GlossySamples
		{
			get { return UseThis ? Dictionary.GetInteger("glossysamples", RcCore.It.EngineSettings.GlossySamples) : RcCore.It.EngineSettings.GlossySamples; }
			set { Dictionary.Set("glossysamples", value); }
		}

		public int TransmissionSamples
		{
			get { return UseThis ? Dictionary.GetInteger("transmissionsamples", RcCore.It.EngineSettings.TransmissionSamples) : RcCore.It.EngineSettings.TransmissionSamples; }
			set { Dictionary.Set("transmissionsamples", value); }
		}

		public int MinBounce
		{
			get { return UseThis ? Dictionary.GetInteger("minbounce", RcCore.It.EngineSettings.MinBounce) : RcCore.It.EngineSettings.MinBounce; }
			set { Dictionary.Set("minbounce", value); }
		}

		public int MaxBounce
		{
			get { return UseThis ? Dictionary.GetInteger("maxbounce", RcCore.It.EngineSettings.MaxBounce) : RcCore.It.EngineSettings.MaxBounce; }
			set { Dictionary.Set("maxbounce", value); }
		}

		public int MaxDiffuseBounce
		{
			get { return UseThis ? Dictionary.GetInteger("maxdiffusebounce", RcCore.It.EngineSettings.MaxDiffuseBounce) : RcCore.It.EngineSettings.MaxDiffuseBounce; }
			set { Dictionary.Set("maxdiffusebounce", value); }
		}

		public int MaxGlossyBounce
		{
			get { return UseThis ? Dictionary.GetInteger("maxglossybounce", RcCore.It.EngineSettings.MaxGlossyBounce) : RcCore.It.EngineSettings.MaxGlossyBounce; }
			set { Dictionary.Set("maxglossybounce", value); }
		}

		public int MaxVolumeBounce
		{
			get { return UseThis ? Dictionary.GetInteger("maxvolumebounce", RcCore.It.EngineSettings.MaxVolumeBounce) : RcCore.It.EngineSettings.MaxVolumeBounce; }
			set { Dictionary.Set("maxvolumebounce", value); }
		}

		public int MaxTransmissionBounce
		{
			get { return UseThis ? Dictionary.GetInteger("maxtransmissionbounce", RcCore.It.EngineSettings.MaxTransmissionBounce) : RcCore.It.EngineSettings.MaxTransmissionBounce; }
			set { Dictionary.Set("maxtransmissionbounce", value); }
		}
	}
}
