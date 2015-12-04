/**
Copyright 2014-2015 Robert McNeel and Associates

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
using Rhino;
using Rhino.Render;

namespace RhinoCycles
{
	public class EngineSettings
	{
		public EngineSettings()
		{
			DefaultSettings();
		}

		public EngineSettings(EngineSettings es)
		{
			Verbose = es.Verbose;
			ShowTime = es.ShowTime;
			UseSimpleShaders = es.UseSimpleShaders;
			IntegratorMethod = es.IntegratorMethod;
			UseCustomQualitySettings = es.UseCustomQualitySettings;

			SpotlightFactor = es.SpotlightFactor;
			PointlightFactor = es.PointlightFactor;
			SunlightFactor = es.SunlightFactor;
			ArealightFactor = es.ArealightFactor;
			PolishFactor = es.PolishFactor;

			UseInteractiveRenderer = es.UseInteractiveRenderer;
			UseSkyLight = es.UseSkyLight;

			Threads = es.Threads;
			SelectedDevice = es.SelectedDevice;

			Samples = es.Samples;
			Seed = es.Seed;

			MinBounce = es.MinBounce;
			MaxBounce = es.MaxBounce;

			NoCaustics = es.NoCaustics;

			MaxDiffuseBounce = es.MaxDiffuseBounce;
			MaxGlossyBounce = es.MaxGlossyBounce;
			MaxTransmissionBounce = es.MaxTransmissionBounce;
			MaxVolumeBounce = es.MaxVolumeBounce;

			TransparentMinBounce = es.TransparentMinBounce;
			TransparentMaxBounce = es.TransparentMaxBounce;
			TransparentShadows = es.TransparentShadows;

			/* the following sample settings are used when branched path integrator is used. */
			AaSamples = es.AaSamples;
			DiffuseSamples = es.DiffuseSamples;
			GlossySamples = es.GlossySamples;
			TransmissionSamples = es.TransmissionSamples;
			AoSamples = es.AoSamples;
			MeshLightSamples = es.MeshLightSamples;
			SubsurfaceSamples = es.SubsurfaceSamples;
			VolumeSamples = es.VolumeSamples;

			SensorWidth = es.SensorWidth;
			SensorHeight = es.SensorHeight;

			SamplingPattern = es.SamplingPattern;
			FilterGlossy = es.FilterGlossy;
			SampleClampDirect = es.SampleClampDirect;
			SampleClampIndirect = es.SampleClampIndirect;
			SampleAllLights = es.SampleAllLights;
			SampleAllLightsIndirect = es.SampleAllLightsIndirect;
			
		}

		public void DefaultSettings()
		{
			Verbose = false;
			ShowTime = true;
			UseSimpleShaders = false;
			IntegratorMethod = IntegratorMethod.Path;
			UseCustomQualitySettings = false;

			SpotlightFactor = 40.0f;
			PointlightFactor = 40.0f;
			SunlightFactor = 3.2f;
			ArealightFactor = 12.7f;
			PolishFactor = 0.09f;

			UseInteractiveRenderer = true;
			UseSkyLight = false;

			Threads = 0;
			SelectedDevice = -1;

			Samples = 100;
			Seed = 128;

			MinBounce = 3;
			MaxBounce = 8;

			NoCaustics = false;

			MaxDiffuseBounce = 0;
			MaxGlossyBounce = 4;
			MaxTransmissionBounce = 8;
			MaxVolumeBounce = 2;

			TransparentMinBounce = 8;
			TransparentMaxBounce = 8;
			TransparentShadows = true;

			/* the following sample settings are used when branched path integrator is used. */
			AaSamples = 8;
			DiffuseSamples = 1;
			GlossySamples = 1;
			TransmissionSamples = 1;
			AoSamples = 1;
			MeshLightSamples = 1;
			SubsurfaceSamples = 1;
			VolumeSamples = 1;

			SensorWidth = 32.0f;
			SensorHeight = 18.0f;

			SamplingPattern = SamplingPattern.CMJ;
			FilterGlossy = 0.5f;
			SampleClampDirect = 0.0f;
			SampleClampIndirect = 5.0f;
			SampleAllLights = true;
			SampleAllLightsIndirect = true;
		}

		public void SetQuality(PreviewSceneQuality quality)
		{
			if (!UseCustomQualitySettings)
			{
				switch (quality)
				{
					case PreviewSceneQuality.RefineFirstPass:
						Samples = 5000;
						GlossySamples = 2;
						TransmissionSamples = 2;
						break;
					case PreviewSceneQuality.RefineSecondPass:
						Samples = 5000;
						GlossySamples = 2;
						TransmissionSamples = 2;
						break;
					case PreviewSceneQuality.RefineThirdPass:
						Samples = 5000;
						TransmissionSamples = 3;
						GlossySamples = 3;
						break;
				}
			}
		}

		public void SetQuality(AntialiasLevel quality)
		{
			if (!UseCustomQualitySettings)
			{
				switch (quality)
				{
					case AntialiasLevel.None:
						Samples = 50;
						break;
					case AntialiasLevel.Draft:
						Samples = 200;
						DiffuseSamples = 2;
						GlossySamples = 2;
						TransmissionSamples = 2;
						break;
					case AntialiasLevel.Good:
						Samples = 500;
						DiffuseSamples = 3;
						GlossySamples = 3;
						TransmissionSamples = 3;
						break;
					case AntialiasLevel.High:
						Samples = 2000;
						DiffuseSamples = 4;
						GlossySamples = 4;
						TransmissionSamples = 4;
						break;
				}
			}
		}

		/// <summary>
		/// Set to true if rhino shader conversion should be skipped.
		/// </summary>
		public bool UseSimpleShaders { get; set; }

		public bool Verbose { get; set; }
		public bool ShowTime { get; set; }
		public bool UseInteractiveRenderer { get; set; }
		public bool UseSkyLight { get; set; }
		public float SpotlightFactor { get; set; }
		public float PointlightFactor { get; set; }
		public float SunlightFactor { get; set; }
		public float ArealightFactor { get; set; }
		public float PolishFactor { get; set; }

		public int Threads { get; set; }
		public int SelectedDevice { get; set; }

		public IntegratorMethod IntegratorMethod { get; set; }
		public int MinBounce { get; set; }
		public int MaxBounce { get; set; }
		public bool NoCaustics { get; set; }
		public int MaxDiffuseBounce { get; set; }
		public int MaxGlossyBounce { get; set; }
		public int MaxTransmissionBounce { get; set; }
		public int MaxVolumeBounce { get; set; }
		public int AaSamples { get; set; }
		public int DiffuseSamples { get; set; }
		public int GlossySamples { get; set; }
		public int TransmissionSamples { get; set; }
		public int AoSamples { get; set; }
		public int MeshLightSamples { get; set; }
		public int SubsurfaceSamples { get; set; }
		public int VolumeSamples { get; set; }
		public int Samples { get; set; }
		public int Seed { get; set; }
		public SamplingPattern SamplingPattern { get; set; }
		public float FilterGlossy { get; set; }
		public float SampleClampDirect { get; set; }
		public float SampleClampIndirect { get; set; }
		public bool SampleAllLights { get; set; }
		public bool SampleAllLightsIndirect { get; set; }
		public float SensorWidth { get; set; }
		public float SensorHeight { get; set; }
		public int TransparentMinBounce { get; set; }
		public int TransparentMaxBounce { get; set; }
		public bool TransparentShadows { get; set; }
		public bool UseCustomQualitySettings { get; set; }

	}
}
