using Rhino.DocObjects.Custom;
using System.Runtime.InteropServices;
using RhinoCyclesCore.Core;

namespace RhinoCycles.Settings
{
	[Guid("CAE9D284-03B0-4D1C-AA46-B431BC9A7EA2")]
	public class ViewportSettings : UserDictionary
	{
		public ViewportSettings()
		{
			Dictionary.Version = 1;
			Dictionary.Name = "Cycles viewport-specific settings";

			Samples = RcCore.It.EngineSettings.Samples;
			Seed = RcCore.It.EngineSettings.Seed;
			DiffuseSamples = RcCore.It.EngineSettings.DiffuseSamples;
			GlossySamples = RcCore.It.EngineSettings.GlossySamples;
			TransmissionSamples = RcCore.It.EngineSettings.TransmissionSamples;

			MinBounce = RcCore.It.EngineSettings.MinBounce;
			MaxBounce = RcCore.It.EngineSettings.MaxBounce;

			MaxDiffuseBounce = RcCore.It.EngineSettings.MaxDiffuseBounce;
			MaxGlossyBounce = RcCore.It.EngineSettings.MaxGlossyBounce;
			MaxTransmissionBounce = RcCore.It.EngineSettings.MaxTransmissionBounce;

			TileX = RcCore.It.EngineSettings.TileX;
			TileY = RcCore.It.EngineSettings.TileY;
		}

		public int Samples
		{
			get { return Dictionary.GetInteger("samples", RcCore.It.EngineSettings.SamplesDefault); }
			set { Dictionary.Set("samples", value); }
		}

		public int Seed
		{
			get { return Dictionary.GetInteger("seed", RcCore.It.EngineSettings.SeedDefault); }
			set { Dictionary.Set("seed", value); }
		}

		public int TileX
		{
			get { return Dictionary.GetInteger("tilex", RcCore.It.EngineSettings.TileXDefault); }
			set { Dictionary.Set("tilex", value); }
		}

		public int TileY
		{
			get { return Dictionary.GetInteger("tilex", RcCore.It.EngineSettings.TileYDefault); }
			set { Dictionary.Set("tilex", value); }
		}

		public int DiffuseSamples
		{
			get { return Dictionary.GetInteger("diffusesamples", RcCore.It.EngineSettings.DiffuseSamplesDefault); }
			set { Dictionary.Set("diffusesamples", value); }
		}

		public int GlossySamples
		{
			get { return Dictionary.GetInteger("glossysamples", RcCore.It.EngineSettings.GlossySamplesDefault); }
			set { Dictionary.Set("glossysamples", value); }
		}

		public int TransmissionSamples
		{
			get { return Dictionary.GetInteger("transmissionsamples", RcCore.It.EngineSettings.TransmissionSamplesDefault); }
			set { Dictionary.Set("transmissionsamples", value); }
		}

		public int MinBounce
		{
			get { return Dictionary.GetInteger("minbounce", RcCore.It.EngineSettings.MinBounceDefault); }
			set { Dictionary.Set("minbounce", value); }
		}

		public int MaxBounce
		{
			get { return Dictionary.GetInteger("maxbounce", RcCore.It.EngineSettings.MaxBounceDefault); }
			set { Dictionary.Set("maxbounce", value); }
		}

		public int MaxDiffuseBounce
		{
			get { return Dictionary.GetInteger("maxdiffusebounce", RcCore.It.EngineSettings.MaxDiffuseBounceDefault); }
			set { Dictionary.Set("maxdiffusebounce", value); }
		}

		public int MaxGlossyBounce
		{
			get { return Dictionary.GetInteger("maxglossybounce", RcCore.It.EngineSettings.MaxGlossyBounceDefault); }
			set { Dictionary.Set("maxglossybounce", value); }
		}

		public int MaxVolumeBounce
		{
			get { return Dictionary.GetInteger("maxvolumebounce", RcCore.It.EngineSettings.MaxVolumeBounceDefault); }
			set { Dictionary.Set("maxvolumebounce", value); }
		}

		public int MaxTransmissionBounce
		{
			get { return Dictionary.GetInteger("maxtransmissionbounce", RcCore.It.EngineSettings.MaxTransmissionBounceDefault); }
			set { Dictionary.Set("maxtransmissionbounce", value); }
		}
	}
}
