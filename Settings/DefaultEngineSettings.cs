using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RhinoCyclesCore.Settings
{
	public static class DefaultEngineSettings
	{
		static public bool Verbose => false;

		static public float SpotLightFactor => 40.0f;
		static public float PointLightFactor => 40.0f;
		static public float SunLightFactor => 3.2f;
		static public float LinearLightFactor => 10.0f;
		static public float AreaLightFactor => 17.2f;
		static public float PolishFactor => 0.09f;

		static public int ThrottleMs => 5;
		static public int Threads => Math.Max(1, Environment.ProcessorCount - 2);
		static public float BumpDistance => 1.0f;
		static public float NormalStrengthFactor => 1.0f;
		static public float BumpStrengthFactor => 1.0f;

		static public string SelectedDeviceStr => "-1";
		static public bool AllowSelectedDeviceOverride => false;

		static public bool UseStartResolution => RenderEngine.OnHighDpi ? true : false;
		static public int StartResolution => 64;

		static public float DpiScale => 1.0f;

		static public int TileX => 128;
		static public int TileY => 128;

		static public int MaxBounce => 32;

		static public bool NoCaustics => false;

		static public int MaxDiffuseBounce => 32;
		static public int MaxGlossyBounce => 32;
		static public int MaxTransmissionBounce => 32;

		static public int MaxVolumeBounce => 32;

		static public int AaSamples => 32;

		static public int DiffuseSamples => 32;
		static public int GlossySamples => 32;
		static public int TransmissionSamples => 32;

		static public int AoSamples => 32;

		static public int MeshLightSamples => 32;
		static public int SubSurfaceSamples => 32;
		static public int VolumeSamples => 32;

		static public int Samples => 1000;
		static public bool UseDocumentSamples => false;
		/// <summary>
		/// Texture bake quality 0-3
		///
		/// 0 = low
		/// 1 = standard
		/// 2 = high
		/// 3 = ultra
		/// </summary>
		static public int TextureBakeQuality => 0;
		static public int Seed => 128;

		static public float FilterGlossy => 0.5f;

		static public float SampleClampDirect => 3.0f;
		static public float SampleClampIndirect => 3.0f;
		static public float LightSamplingThreshold => 0.05f;

		static public bool SampleAllLights => true;
		static public bool SampleAllLightsIndirect => true;

		static public int Blades => 0;
		static public float BladesRotation => 0.0f;
		static public float ApertureRatio => 1.0f;
		static public float ApertureFactor => 0.1f;

		static public float SensorWidth => 32.0f;
		static public float SensorHeight => 18.0f;

		static public int TransparentMaxBounce => 32;

		static public int SssMethod => 44;

		static public bool ShowMaxPasses => true;
		static public int OpenClDeviceType => 4;
		static public int OpenClKernelType => 0;
		static public bool CPUSplitKernel => true;
		static public bool OpenClSingleProgram => true;
		static public bool NoShadows => false;
		static public bool SaveDebugImages => false;
		static public bool DebugSimpleShaders => false;
		static public bool DebugNoOverrideTileSize => false;
		static public bool FlushAtEndOfCreateWorld => false;
		static public int PreviewSamples => 150;
	}
}
