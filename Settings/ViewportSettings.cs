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
			if (src != null)
			{
				SelectedDevice = src.SelectedDevice;
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
				uint rem = 0xdeadbeef;
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

		public string SelectedDevice
		{
			get { return Dictionary.GetString("selecteddevice", RcCore.It.EngineSettings.SelectedDeviceStr); }
			set { Dictionary.Set("selecteddevice", value); }
		}

		public int Samples
		{
			get { return Dictionary.GetInteger("samples", RcCore.It.EngineSettings.Samples); }
			set { Dictionary.Set("samples", value); }
		}

		public int ThrottleMs
		{
			get { return Dictionary.GetInteger("throttlems", RcCore.It.EngineSettings.ThrottleMs); }
			set { Dictionary.Set("throttlems", value); }
		}

		public int Seed
		{
			get { return Dictionary.GetInteger("seed", RcCore.It.EngineSettings.Seed); }
			set { Dictionary.Set("seed", value); }
		}

		public int TileX
		{
			get { return Dictionary.GetInteger("tilex", RcCore.It.EngineSettings.TileX); }
			set { Dictionary.Set("tilex", value); }
		}

		public int TileY
		{
			get { return Dictionary.GetInteger("tilex", RcCore.It.EngineSettings.TileY); }
			set { Dictionary.Set("tilex", value); }
		}

		public int DiffuseSamples
		{
			get { return Dictionary.GetInteger("diffusesamples", RcCore.It.EngineSettings.DiffuseSamples); }
			set { Dictionary.Set("diffusesamples", value); }
		}

		public int GlossySamples
		{
			get { return Dictionary.GetInteger("glossysamples", RcCore.It.EngineSettings.GlossySamples); }
			set { Dictionary.Set("glossysamples", value); }
		}

		public int TransmissionSamples
		{
			get { return Dictionary.GetInteger("transmissionsamples", RcCore.It.EngineSettings.TransmissionSamples); }
			set { Dictionary.Set("transmissionsamples", value); }
		}

		public int MinBounce
		{
			get { return Dictionary.GetInteger("minbounce", RcCore.It.EngineSettings.MinBounce); }
			set { Dictionary.Set("minbounce", value); }
		}

		public int MaxBounce
		{
			get { return Dictionary.GetInteger("maxbounce", RcCore.It.EngineSettings.MaxBounce); }
			set { Dictionary.Set("maxbounce", value); }
		}

		public int MaxDiffuseBounce
		{
			get { return Dictionary.GetInteger("maxdiffusebounce", RcCore.It.EngineSettings.MaxDiffuseBounce); }
			set { Dictionary.Set("maxdiffusebounce", value); }
		}

		public int MaxGlossyBounce
		{
			get { return Dictionary.GetInteger("maxglossybounce", RcCore.It.EngineSettings.MaxGlossyBounce); }
			set { Dictionary.Set("maxglossybounce", value); }
		}

		public int MaxVolumeBounce
		{
			get { return Dictionary.GetInteger("maxvolumebounce", RcCore.It.EngineSettings.MaxVolumeBounce); }
			set { Dictionary.Set("maxvolumebounce", value); }
		}

		public int MaxTransmissionBounce
		{
			get { return Dictionary.GetInteger("maxtransmissionbounce", RcCore.It.EngineSettings.MaxTransmissionBounce); }
			set { Dictionary.Set("maxtransmissionbounce", value); }
		}
	}
}
