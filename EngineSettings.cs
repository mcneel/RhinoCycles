/**
Copyright 2014-2016 Robert McNeel and Associates

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

using System;
using ccl;
using Rhino;
using Rhino.PlugIns;
using Rhino.Render;

namespace RhinoCyclesCore
{
	public class EngineSettings
	{
		public static readonly PlugIn RcPlugIn = PlugIn.Find(new Guid("9BC28E9E-7A6C-4B8F-A0C6-3D05E02D1B97"));

		public EngineSettings()
		{
			DefaultSettings();
		}

		public bool IgnoreQualityChanges { get; set; }

		public void DefaultSettings()
		{
			ShowTime = true;
			IntegratorMethod = IntegratorMethod.Path;

			SpotlightFactor = 40.0f;
			PointlightFactor = 40.0f;
			SunlightFactor = 3.2f;
			ArealightFactor = 12.7f;
			PolishFactor = 0.09f;

			UseInteractiveRenderer = true;
			UseSkyLight = false;

			BumpDistance = 0.1f;

			Seed = 128;

			MinBounce = 3;
			MaxBounce = 128;

			NoCaustics = false;

			MaxDiffuseBounce = 2;
			MaxGlossyBounce = 32;
			MaxTransmissionBounce = 32;
			MaxVolumeBounce = 32;

			TransparentMinBounce = 8;
			TransparentMaxBounce = 32;
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
			SampleClampDirect = 1.0f;
			SampleClampIndirect = 5.0f;
			LightSamplingThreshold = 0.05f;
			SampleAllLights = true;
			SampleAllLightsIndirect = true;
		}

		public void SetQuality(PreviewSceneQuality quality)
		{
			//Samples = 500;
			GlossySamples = 2;
			TransmissionSamples = 2;
		}

		public void SetQuality(AntialiasLevel quality)
		{
			if (IgnoreQualityChanges) return;

			if (UseCustomSettings) return;

			switch (quality)
			{
				case AntialiasLevel.None:
				case AntialiasLevel.Draft:
				case AntialiasLevel.Good:
				case AntialiasLevel.High:
					//Samples = 10000;
					DiffuseSamples = 128;
					GlossySamples = 128;
					TransmissionSamples = 128;
					TransparentMaxBounce = 128;
					TransparentMinBounce = 128;
					LightSamplingThreshold = 0.05f;
					break;
			}
		}

		public bool RenderDeviceIsCuda => RenderDevice.IsMultiCuda || RenderDevice.IsCuda;

		public bool RenderDeviceIsOpenCl => RenderDevice.IsMultiOpenCl || RenderDevice.IsOpenCl;

		public Device RenderDevice
		{
			get
			{
				var render_device = SelectedDevice == -1
					? Device.FirstGpu
					: Device.GetDevice(SelectedDevice);
				return render_device;
			}
		}

		public bool SaveDebugImagesDefault => false;

		public bool SaveDebugImages
		{
			get { return RcPlugIn.Settings.GetBool("rc_savedebugimages", SaveDebugImagesDefault); }
			set { RcPlugIn.Settings.SetBool("rc_savedebugimages", value); }
		}

		public bool VerboseDefault => false;
		public bool Verbose
		{
			get { return RcPlugIn.Settings.GetBool("rc_verbose", VerboseDefault); }
			set { RcPlugIn.Settings.SetBool("rc_verbose", value); }
		}
		public bool ShowTime { get; set; }
		public bool UseInteractiveRenderer { get; set; }
		public bool UseSkyLight { get; set; }
		public float SpotlightFactor { get; set; }
		public float PointlightFactor { get; set; }
		public float SunlightFactor { get; set; }
		public float ArealightFactor { get; set; }
		public float PolishFactor { get; set; }

		public int ThreadsDefault => Math.Max(1, Environment.ProcessorCount - 2);
		public int Threads
		{
			get { return RcPlugIn.Settings.GetInteger("rc_threads", ThreadsDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_threads", value); }
		}
		public float BumpDistance { get; set; }
		public int SelectedDeviceDefault => -1;
		public int SelectedDevice
		{
			get { return RcPlugIn.Settings.GetInteger("rc_selecteddevice", SelectedDeviceDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_selecteddevice", value); }
		}

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
		public int SamplesDefault => 10000;
		public int Samples
		{
			get { return RcPlugIn.Settings.GetInteger("rc_samples", SamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_samples", value); }
		}

		public int Seed { get; set; }
		public SamplingPattern SamplingPattern { get; set; }
		public float FilterGlossy { get; set; }
		public float SampleClampDirect { get; set; }
		public float SampleClampIndirect { get; set; }
		public float LightSamplingThreshold { get; set; }
		public bool SampleAllLights { get; set; }
		public bool SampleAllLightsIndirect { get; set; }
		public float SensorWidth { get; set; }
		public float SensorHeight { get; set; }
		public int TransparentMinBounce { get; set; }
		public int TransparentMaxBounce { get; set; }
		public bool TransparentShadows { get; set; }

		public bool UseCustomSettingsDefault => true;
		public bool UseCustomSettings
		{
			get
			{
					return RcPlugIn.Settings.GetBool("rc_usecustomsettings", UseCustomSettingsDefault);
			}
			set
			{
				RcPlugIn.Settings.SetBool("rc_usecustomsettings", value);
			}
		}

	}
}
