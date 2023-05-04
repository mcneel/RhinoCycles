/**
Copyright 2014-2020 Robert McNeel and Associates

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

using ccl;

namespace RhinoCyclesCore.Settings
{
	public interface IDocumentSettings
	{
		IntegratorMethod IntegratorMethod { get; set; }
		uint IntegratorHash { get;  }
		int Samples { get; set; }
		bool UseDocumentSamples { get; set; }
		int TextureBakeQuality { get; set; }
		int Seed { get; set; }
		int DiffuseSamples { get; set; }
		int GlossySamples { get; set; }
		int TransmissionSamples { get; set; }
		int MaxBounce { get; set; }
		int MaxDiffuseBounce { get; set; }
		int MaxGlossyBounce { get; set; }
		int MaxVolumeBounce { get; set; }
		int MaxTransmissionBounce { get; set; }
		int TransparentMaxBounce { get; set; }

		int TileX { get; set; }
		int TileY { get; set; }

		float SpotLightFactor { get; set; }
		float PointLightFactor { get; set; }
		float SunLightFactor { get; set; }
		float LinearLightFactor { get; set; }
		float AreaLightFactor { get; set; }
		float PolishFactor { get; set; }

		float BumpDistance { get; set; }
		float NormalStrengthFactor { get; set; }
		float BumpStrengthFactor { get; set; }

		bool NoCaustics { get; set; }

		int AaSamples { get; set; }

		int AoSamples { get; set; }

		int MeshLightSamples { get; set; }
		int SubsurfaceSamples { get; set; }
		int VolumeSamples { get; set; }

		float FilterGlossy { get; set; }

		float SampleClampDirect { get; set; }
		float SampleClampIndirect { get; set; }
		float LightSamplingThreshold { get; set; }

		bool SampleAllLights { get; set; }
		bool SampleAllLightsIndirect { get; set; }

		int Blades { get; set; }
		float BladesRotation { get; set; }
		float ApertureRatio { get; set; }
		float ApertureFactor { get; set; }

		float SensorWidth { get; set; }
		float SensorHeight { get; set; }

		int SssMethod { get; set; }
		bool AllowSelectedDeviceOverride { get; }
		Device RenderDevice { get; }
		bool ShowMaxPasses { get; set; }
	}
}
