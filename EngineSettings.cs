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

using System;
using ccl;
using Rhino;
using Rhino.PlugIns;
using Rhino.Render;

namespace RhinoCyclesCore
{
	public class EngineSettings : IViewportSettings
	{
		public static readonly PlugIn RcPlugIn = PlugIn.Find(new Guid("9BC28E9E-7A6C-4B8F-A0C6-3D05E02D1B97"));

		public EngineSettings()
		{
			DefaultSettings();
		}

		public bool IgnoreQualityChanges { get; set; }

		public void DefaultSettings()
		{
			IntegratorMethod = IntegratorMethod.Path;
			SamplingPattern = SamplingPattern.CMJ;

			// persisted settings
			SaveDebugImages = SaveDebugImages;
			Verbose = Verbose;

			ShowViewportPropertiesPanel = ShowViewportPropertiesPanel;

			SpotlightFactor = SpotlightFactor;
			PointlightFactor = PointlightFactor;
			SunlightFactor = SunlightFactor;
			ArealightFactor = ArealightFactor;
			PolishFactor = PolishFactor;

			ThrottleMs = ThrottleMs;
			Threads = Threads;
			BumpDistance = BumpDistance;

			SelectedDeviceStr = SelectedDeviceStr;

			MinBounce = MinBounce;
			MaxBounce = MaxBounce;

			NoCaustics = NoCaustics;

			MaxDiffuseBounce = MaxDiffuseBounce;
			MaxGlossyBounce = MaxGlossyBounce;
			MaxTransmissionBounce = MaxTransmissionBounce;

			MaxVolumeBounce = MaxVolumeBounce;

			AaSamples = AaSamples;

			DiffuseSamples = DiffuseSamples;
			GlossySamples = GlossySamples;
			TransmissionSamples = TransmissionSamples;
			
			AoSamples = AoSamples;
			
			MeshLightSamples = MeshLightSamples;
			SubsurfaceSamples = SubsurfaceSamples;
			VolumeSamples = VolumeSamples;

			Samples = Samples;
			Seed = Seed;

			FilterGlossy = FilterGlossy;

			SampleClampDirect = SampleClampDirect;
			SampleClampIndirect = SampleClampIndirect;
			LightSamplingThreshold = LightSamplingThreshold;

			SampleAllLights = SampleAllLights;
			SampleAllLightsIndirect = SampleAllLightsIndirect;

			SensorWidth = SensorWidth;
			SensorHeight = SensorHeight;

			TransparentMinBounce = TransparentMinBounce;
			TransparentMaxBounce = TransparentMaxBounce;
			TransparentShadows = TransparentShadows;
		}

		public bool RenderDeviceIsCuda => RenderDevice.IsMultiCuda || RenderDevice.IsCuda;

		public bool RenderDeviceIsOpenCl => RenderDevice.IsMultiOpenCl || RenderDevice.IsOpenCl;

		public Device RenderDevice
		{
			get
			{
				return Device.DeviceFromString(SelectedDeviceStr);
			}
		}

		public bool SaveDebugImagesDefault => false;

		public virtual bool SaveDebugImages
		{
			get { return RcPlugIn.Settings.GetBool("rc_savedebugimages", SaveDebugImagesDefault); }
			set { RcPlugIn.Settings.SetBool("rc_savedebugimages", value); }
		}

		public bool ShowViewportPropertiesPanelDefault => false;
		public virtual bool ShowViewportPropertiesPanel
		{
			get { return RcPlugIn.Settings.GetBool("rc_showviewproportiesdialog", ShowViewportPropertiesPanelDefault); }
			set { RcPlugIn.Settings.SetBool("rc_showviewproportiesdialog", value); }
		}

		public bool VerboseDefault => false;
		public virtual bool Verbose
		{
			get { return RcPlugIn.Settings.GetBool("rc_verbose", VerboseDefault); }
			set { RcPlugIn.Settings.SetBool("rc_verbose", value); }
		}

		public bool ShowMaxPassesDefault = false;
		/// <summary>
		/// Set to true to show the maximum passes count in the HUD, i.e. 48/100. For
		/// false it would show just 48.
		/// </summary>
		public virtual bool ShowMaxPasses
		{
			get { return RcPlugIn.Settings.GetBool("rc_maxpasses", ShowMaxPassesDefault); }
			set { RcPlugIn.Settings.SetBool("rc_maxpasses", value); }
		}

		public float SpotlightFactorDefault => 40.0f;
		public virtual float SpotlightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble("rc_spotlightfactor", SpotlightFactorDefault); }
			set { RcPlugIn.Settings.SetDouble("rc_spotlightfactor", value); }
		}

		public float PointlightFactorDefault => 40.0f;
		public virtual float PointlightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble("rc_pointlightfactor", PointlightFactorDefault); }
			set { RcPlugIn.Settings.SetDouble("rc_pointlightfactor", value); }
		}

		public float SunlightFactorDefault => 3.2f;
		public virtual float SunlightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble("rc_sunlightfactor", SunlightFactorDefault); }
			set { RcPlugIn.Settings.SetDouble("rc_sunlightfactor", value); }
		}

		public float ArealightFactorDefault => 17.2f;
		public virtual float ArealightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble("rc_arealightfactor", ArealightFactorDefault); }
			set { RcPlugIn.Settings.SetDouble("rc_arealightfactor", value); }
		}

		public float PolishFactorDefault => 0.09f;
		public virtual float PolishFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble("rc_polishfactor", PolishFactorDefault); }
			set { RcPlugIn.Settings.SetDouble("rc_polishfactor", value); }
		}

		public int ThrottleMsDefault => 10;
		/// <summary>
		/// On systems where the (only) GPU is the primary device it can happen that
		/// the system becomes very sluggish while using Raytraced. Set this to a number
		/// greater than zero to introduce a throttle. Note that the number is a sleep
		/// duration in milliseconds.
		/// </summary>
		public virtual int ThrottleMs
		{
			get { return RcPlugIn.Settings.GetInteger("rc_throttlems", ThrottleMsDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_throttlems", value); }
		}

		public int ThreadsDefault => Math.Max(1, Environment.ProcessorCount - 2);
		/// <summary>
		/// Set the amount of rendering threads to create. Especially useful for CPU rendering where
		/// one doesn't want to use 100% CPU to retain responsiveness. By default set to
		/// (logical) processor count - 2, at minimum 1.
		/// </summary>
		public virtual int Threads
		{
			get { return RcPlugIn.Settings.GetInteger("rc_threads", ThreadsDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_threads", value); }
		}

		public int TileXDefault => 128;
		/// <summary>
		/// Set the width of the render tile.
		/// </summary>
		public virtual int TileX
		{
			get { return RcPlugIn.Settings.GetInteger("rc_tilex", TileXDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_tilex", value); }
		}

		public int TileYDefault => 128;
		/// <summary>
		/// Set the height of the render tile.
		/// </summary>
		public virtual int TileY
		{
			get { return RcPlugIn.Settings.GetInteger("rc_tiley", TileYDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_tiley", value); }
		}

		public float BumpDistanceDefault => 0.01f;
		public virtual float BumpDistance
		{
			get { return (float)RcPlugIn.Settings.GetDouble("rc_bumpdistance", BumpDistanceDefault); }
			set { RcPlugIn.Settings.SetDouble("rc_bumpdistance", value); }
		}
		public int SelectedDeviceDefault => -1;
		[Obsolete("Device selection setting has changed to be a string to cope with multi-devices.")]
		public virtual int SelectedDevice
		{
			get { return RcPlugIn.Settings.GetInteger("rc_selecteddevice", SelectedDeviceDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_selecteddevice", value); }
		}

		public string SelectedDeviceStrDefault => "-1";
		public virtual string SelectedDeviceStr
		{
			get { return RcPlugIn.Settings.GetString("rc_selecteddevicestr", SelectedDeviceStrDefault); }
			set { RcPlugIn.Settings.SetString("rc_selecteddevicestr", value); }
		}

		public virtual IntegratorMethod IntegratorMethod { get; set; }
		public int MinBounceDefault => 3;
		public virtual int MinBounce
		{
			get { return RcPlugIn.Settings.GetInteger("rc_minbounce", MinBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_minbounce", value); }
		}
		public int MaxBounceDefault => 128;
		public virtual int MaxBounce
		{
			get { return RcPlugIn.Settings.GetInteger("rc_maxbounce", MaxBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_maxbounce", value); }
		}
		public bool NoCausticsDefault => false;
		public virtual bool NoCaustics
		{
			get { return RcPlugIn.Settings.GetBool("rc_nocaustics", NoCausticsDefault); }
			set { RcPlugIn.Settings.SetBool("rc_nocaustics", value); }
		}
		public int MaxDiffuseBounceDefault => 2;
		public virtual int MaxDiffuseBounce
		{
			get { return RcPlugIn.Settings.GetInteger("rc_maxdiffusebounce", MaxDiffuseBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_maxdiffusebounce", value); }
		}
		public int MaxGlossyBounceDefault => 32;
		public virtual int MaxGlossyBounce
		{
			get { return RcPlugIn.Settings.GetInteger("rc_maxglossybounce", MaxGlossyBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_maxglossybounce", value); }
		}
		public int MaxTransmissionBounceDefault => 32;
		public virtual int MaxTransmissionBounce
		{
			get { return RcPlugIn.Settings.GetInteger("rc_maxtransmission*gbounce", MaxTransmissionBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_maxtransmission*gbounce", value); }
		}
		public int MaxVolumeBounceDefault => 32;
		public virtual int MaxVolumeBounce
		{
			get { return RcPlugIn.Settings.GetInteger("rc_maxglossybounce", MaxVolumeBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_maxglossybounce", value); }
		}
		public int AaSamplesDefault => 8;
		public virtual int AaSamples
		{
			get { return RcPlugIn.Settings.GetInteger("rc_aasamples", AaSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_aasamples", value); }
		}
		public int DiffuseSamplesDefault => 128;
		public virtual int DiffuseSamples
		{
			get { return RcPlugIn.Settings.GetInteger("rc_diffusesamples", DiffuseSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_diffusesamples", value); }
		}
		public int GlossySamplesDefault => 128;
		public virtual int GlossySamples
		{
			get { return RcPlugIn.Settings.GetInteger("rc_glossysamples", GlossySamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_glossysamples", value); }
		}
		public int TransmissionSamplesDefault => 128;
		public virtual int TransmissionSamples
		{
			get { return RcPlugIn.Settings.GetInteger("rc_transmissionsamples", TransmissionSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_transmissionsamples", value); }
		}
		public int AoSamplesDefault => 2;
		public virtual int AoSamples
		{
			get { return RcPlugIn.Settings.GetInteger("rc_aosamples", AoSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_aosamples", value); }
		}
		public int MeshLightSamplesDefault => 2;
		public virtual int MeshLightSamples
		{
			get { return RcPlugIn.Settings.GetInteger("rc_meshlightsamples", MeshLightSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_meshlightsamples", value); }
		}
		public int SubsurfaceSamplesDefault => 2;
		public virtual int SubsurfaceSamples
		{
			get { return RcPlugIn.Settings.GetInteger("rc_subsurfacesamples", SubsurfaceSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_subsurfacesamples", value); }
		}
		public int VolumeSamplesDefault => 2;
		public virtual int VolumeSamples
		{
			get { return RcPlugIn.Settings.GetInteger("rc_volumesamples", VolumeSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_volumesamples", value); }
		}
		public int SamplesDefault => 10000;
		public virtual int Samples
		{
			get { return RcPlugIn.Settings.GetInteger("rc_samples", SamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_samples", value); }
		}

		public int SeedDefault => 128;
		public virtual int Seed
		{
			get { return RcPlugIn.Settings.GetInteger("rc_seed", SeedDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_seed", value); }
		}
		public virtual SamplingPattern SamplingPattern { get; set; }
		public float FilterGlossyDefault => 0.5f;
		public virtual float FilterGlossy
		{
			get { return (float)RcPlugIn.Settings.GetDouble("rc_filterglossy", FilterGlossyDefault); }
			set { RcPlugIn.Settings.SetDouble("rc_filterglossy", value); }
		}

		public float SampleClampDirectDefault => 1.0f;
		public virtual float SampleClampDirect
		{
			get { return (float)RcPlugIn.Settings.GetDouble("rc_sampleclampdirect", SampleClampDirectDefault); }
			set { RcPlugIn.Settings.SetDouble("rc_sampleclampdirect", value); }
		}
		public float SampleClampIndirectDefault => 5.0f;
		public virtual float SampleClampIndirect
		{
			get { return (float)RcPlugIn.Settings.GetDouble("rc_sampleclampindirect", SampleClampIndirectDefault); }
			set { RcPlugIn.Settings.SetDouble("rc_sampleclampindirect", value); }
		}

		public float LightSamplingThresholdDefault => 0.05f;
		public virtual float LightSamplingThreshold
		{
			get { return (float)RcPlugIn.Settings.GetDouble("rc_lightsamplingthreshold", LightSamplingThresholdDefault); }
			set { RcPlugIn.Settings.SetDouble("rc_lightsamplingthreshold", value); }
		}

		public bool SampleAllLightsDefault => true;
		public virtual bool SampleAllLights
		{
			get { return RcPlugIn.Settings.GetBool("rc_samplealllights", SampleAllLightsDefault); }
			set { RcPlugIn.Settings.SetBool("rc_samplealllights", value); }
		}

		public bool SampleAllLightsIndirectDefault => true;
		public virtual bool SampleAllLightsIndirect
		{
			get { return RcPlugIn.Settings.GetBool("rc_samplealllightsindirect", SampleAllLightsIndirectDefault); }
			set { RcPlugIn.Settings.SetBool("rc_samplealllightsindirect", value); }
		}

		public float SensorWidthDefault => 32.0f;
		public virtual float SensorWidth
		{
			get { return (float)RcPlugIn.Settings.GetDouble("rc_sensorwidth", SensorWidthDefault); }
			set { RcPlugIn.Settings.SetDouble("rc_sensorwidth", value); }
		}
		public float SensorHeightDefault => 18.0f;
		public virtual float SensorHeight
		{
			get { return (float)RcPlugIn.Settings.GetDouble("rc_sensorheight", SensorHeightDefault); }
			set { RcPlugIn.Settings.SetDouble("rc_sensorheight", value); }
		}

		public int TransparentMinBounceDefault => 128;
		public virtual int TransparentMinBounce
		{
			get { return RcPlugIn.Settings.GetInteger("rc_transparentminbounce", TransparentMinBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_transparentminbounce", value); }
		}
		public int TransparentMaxBounceDefault => 128;
		public virtual int TransparentMaxBounce
		{
			get { return RcPlugIn.Settings.GetInteger("rc_transparentmaxbounce", TransparentMaxBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("rc_transparentmaxbounce", value); }
		}
		public bool TransparentShadowsDefault => true;
		public virtual bool TransparentShadows
		{
			get { return RcPlugIn.Settings.GetBool("rc_transparentshadows", TransparentShadowsDefault); }
			set { RcPlugIn.Settings.SetBool("rc_transparentshadows", value); }
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
	}
	public interface IViewportSettings
	{
		int Samples { get; set; }
		int ThrottleMs { get; set; }
		int Seed { get; set; }
		int TileX { get; set; }
		int TileY { get; set; }
		int DiffuseSamples { get; set; }
		int GlossySamples { get; set; }
		int TransmissionSamples { get; set; }
		int MinBounce { get; set; }
		int MaxBounce { get; set; }
		int MaxDiffuseBounce { get; set; }
		int MaxGlossyBounce { get; set; }
		int MaxVolumeBounce { get; set; }
		int MaxTransmissionBounce { get; set; }

		string SelectedDeviceStr { get; set; }

		uint IntegratorHash { get; }
	}

	public class ViewportSettingsChangedArgs : EventArgs
	{
		public IViewportSettings Settings { get; private set; }
		public ViewportSettingsChangedArgs(IViewportSettings vps) { Settings = vps; }
	}
}
