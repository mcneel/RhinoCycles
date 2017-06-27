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
using RhinoCyclesCore.Core;

namespace RhinoCyclesCore
{
	public class EngineSettings : IApplicationSettings, IViewportSettings
	{
		public static readonly PlugIn RcPlugIn = PlugIn.Find(new Guid("9BC28E9E-7A6C-4B8F-A0C6-3D05E02D1B97"));

		public EngineSettings()
		{
			IntegratorMethod = IntegratorMethod.Path;
			SamplingPattern = SamplingPattern.CMJ;

			// persisted settings
			Verbose = Verbose;

			ShowViewportPropertiesPanel = ShowViewportPropertiesPanel;

			SpotlightFactor = SpotlightFactor;
			PointlightFactor = PointlightFactor;
			SunlightFactor = SunlightFactor;
			ArealightFactor = ArealightFactor;
			PolishFactor = PolishFactor;

			ThrottleMs = ThrottleMs;
			FadeInMs = FadeInMs;
			Threads = Threads;
			BumpDistance = BumpDistance;

			SelectedDeviceStr = SelectedDeviceStr;
			IntermediateSelectedDeviceStr = IntermediateSelectedDeviceStr;
			AllowSelectedDeviceOverride = AllowSelectedDeviceOverride;

			UseStartResolution = UseStartResolution;
			StartResolution = StartResolution;

			TileX = TileX;
			TileY = TileY;

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

			// application settings
			AllowViewportSettingsOverride = AllowViewportSettingsOverride;
			UseDrawOpenGl = UseDrawOpenGl;
			OpenClDeviceType = OpenClDeviceType;
			OpenClKernelType = OpenClKernelType;
			CPUSplitKernel = CPUSplitKernel;
			OpenClSingleProgram = OpenClSingleProgram;
			FullHdrSkylight = FullHdrSkylight;
			NoShadows = NoShadows;
			SaveDebugImages = SaveDebugImages;
			FlushAtEndOfCreateWorld = FlushAtEndOfCreateWorld;
		}

		public bool IgnoreQualityChanges { get; set; }

		public void DefaultSettings()
		{
			IntegratorMethod = IntegratorMethod.Path;
			SamplingPattern = SamplingPattern.CMJ;

			// persisted settings
			Verbose = VerboseDefault;

			ShowViewportPropertiesPanel = ShowViewportPropertiesPanelDefault;

			SpotlightFactor = SpotlightFactorDefault;
			PointlightFactor = PointlightFactorDefault;
			SunlightFactor = SunlightFactorDefault;
			ArealightFactor = ArealightFactorDefault;
			PolishFactor = PolishFactorDefault;

			ThrottleMs = ThrottleMsDefault;
			FadeInMs = FadeInMsDefault;
			Threads = ThreadsDefault;
			BumpDistance = BumpDistanceDefault;

			SelectedDeviceStr = SelectedDeviceStrDefault;
			IntermediateSelectedDeviceStr = SelectedDeviceStrDefault;
			AllowSelectedDeviceOverride = AllowSelectedDeviceOverrideDefault;

			UseStartResolution = UseStartResolutionDefault;
			StartResolution = StartResolutionDefault;

			TileX = TileXDefault;
			TileY = TileYDefault;

			MinBounce = MinBounceDefault;
			MaxBounce = MaxBounceDefault;

			NoCaustics = NoCausticsDefault;

			MaxDiffuseBounce = MaxDiffuseBounceDefault;
			MaxGlossyBounce = MaxGlossyBounceDefault;
			MaxTransmissionBounce = MaxTransmissionBounceDefault;

			MaxVolumeBounce = MaxVolumeBounceDefault;

			AaSamples = AaSamplesDefault;

			DiffuseSamples = DiffuseSamplesDefault;
			GlossySamples = GlossySamplesDefault;
			TransmissionSamples = TransmissionSamplesDefault;
			
			AoSamples = AoSamplesDefault;
			
			MeshLightSamples = MeshLightSamplesDefault;
			SubsurfaceSamples = SubsurfaceSamplesDefault;
			VolumeSamples = VolumeSamplesDefault;

			Samples = SamplesDefault;
			Seed = SeedDefault;

			FilterGlossy = FilterGlossyDefault;

			SampleClampDirect = SampleClampDirectDefault;
			SampleClampIndirect = SampleClampIndirectDefault;
			LightSamplingThreshold = LightSamplingThresholdDefault;

			SampleAllLights = SampleAllLightsDefault;
			SampleAllLightsIndirect = SampleAllLightsIndirectDefault;

			SensorWidth = SensorWidthDefault;
			SensorHeight = SensorHeightDefault;

			TransparentMinBounce = TransparentMinBounceDefault;
			TransparentMaxBounce = TransparentMaxBounceDefault;
			TransparentShadows = TransparentShadowsDefault;

			// application settings
			AllowViewportSettingsOverride = AllowViewportSettingsOverrideDefault;
			UseDrawOpenGl = UseDrawOpenGlDefault;
			OpenClDeviceType = OpenClDeviceTypeDefault;
			OpenClKernelType = OpenClKernelTypeDefault;
			CPUSplitKernel = CPUSplitKernelDefault;
			OpenClSingleProgram = OpenClSingleProgramDefault;
			FullHdrSkylight = FullHdrSkylightDefault;
			NoShadows = NoShadowsDefault;
			SaveDebugImages = SaveDebugImagesDefault;
			FlushAtEndOfCreateWorld = FlushAtEndOfCreateWorldDefault;
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
			get { return RcPlugIn.Settings.GetBool("SaveDebugImages", SaveDebugImagesDefault); }
			set { RcPlugIn.Settings.SetBool("SaveDebugImages", value); }
		}

		public bool FlushAtEndOfCreateWorldDefault => false;
		public virtual bool FlushAtEndOfCreateWorld
		{
			get { return RcPlugIn.Settings.GetBool("flushatendofcreateworld", FlushAtEndOfCreateWorldDefault); }
			set { RcPlugIn.Settings.SetBool("flushatendofcreateworld", value); }
		}

		public bool ShowViewportPropertiesPanelDefault => false;
		public virtual bool ShowViewportPropertiesPanel
		{
			get { return RcPlugIn.Settings.GetBool("showviewproportiesdialog", ShowViewportPropertiesPanelDefault); }
			set { RcPlugIn.Settings.SetBool("showviewproportiesdialog", value); }
		}

		public bool VerboseDefault => false;
		public virtual bool Verbose
		{
			get { return RcPlugIn.Settings.GetBool("verbose", VerboseDefault); }
			set { RcPlugIn.Settings.SetBool("verbose", value); }
		}

		public bool ShowMaxPassesDefault = false;
		/// <summary>
		/// Set to true to show the maximum passes count in the HUD, i.e. 48/100. For
		/// false it would show just 48.
		/// </summary>
		public virtual bool ShowMaxPasses
		{
			get { return RcPlugIn.Settings.GetBool("maxpasses", ShowMaxPassesDefault); }
			set { RcPlugIn.Settings.SetBool("maxpasses", value); }
		}

		public float SpotlightFactorDefault => 40.0f;
		public virtual float SpotlightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble("spotlightfactor", SpotlightFactorDefault); }
			set { RcPlugIn.Settings.SetDouble("spotlightfactor", value); }
		}

		public float PointlightFactorDefault => 40.0f;
		public virtual float PointlightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble("pointlightfactor", PointlightFactorDefault); }
			set { RcPlugIn.Settings.SetDouble("pointlightfactor", value); }
		}

		public float SunlightFactorDefault => 3.2f;
		public virtual float SunlightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble("sunlightfactor", SunlightFactorDefault); }
			set { RcPlugIn.Settings.SetDouble("sunlightfactor", value); }
		}

		public float ArealightFactorDefault => 17.2f;
		public virtual float ArealightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble("arealightfactor", ArealightFactorDefault); }
			set { RcPlugIn.Settings.SetDouble("arealightfactor", value); }
		}

		public float PolishFactorDefault => 0.09f;
		public virtual float PolishFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble("polishfactor", PolishFactorDefault); }
			set { RcPlugIn.Settings.SetDouble("polishfactor", value); }
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
			get { return RcPlugIn.Settings.GetInteger("throttlems", ThrottleMsDefault); }
			set { RcPlugIn.Settings.SetInteger("throttlems", value); }
		}

		public int FadeInMsDefault => 10;
		/// <summary>
		/// Speed of result fade-in. Higher values is slower fade in. In milliseconds.
		/// Denotes time to wait between 1% increments towards full render result.
		/// </summary>
		public virtual int FadeInMs
		{
			get { return RcPlugIn.Settings.GetInteger("fadeinms", FadeInMsDefault); }
			set { RcPlugIn.Settings.SetInteger("fadeinms", value); }
		}

		public int ThreadsDefault => Math.Max(1, Environment.ProcessorCount - 2);
		/// <summary>
		/// Set the amount of rendering threads to create. Especially useful for CPU rendering where
		/// one doesn't want to use 100% CPU to retain responsiveness. By default set to
		/// (logical) processor count - 2, at minimum 1.
		/// </summary>
		public virtual int Threads
		{
			get { return RcPlugIn.Settings.GetInteger("threads", ThreadsDefault); }
			set { RcPlugIn.Settings.SetInteger("threads", value); }
		}

		public int TileXDefault => 128;
		/// <summary>
		/// Set the width of the render tile.
		/// </summary>
		public virtual int TileX
		{
			get { return RcPlugIn.Settings.GetInteger("tilex", TileXDefault); }
			set { RcPlugIn.Settings.SetInteger("tilex", value); }
		}

		public int TileYDefault => 128;
		/// <summary>
		/// Set the height of the render tile.
		/// </summary>
		public virtual int TileY
		{
			get { return RcPlugIn.Settings.GetInteger("tiley", TileYDefault); }
			set { RcPlugIn.Settings.SetInteger("tiley", value); }
		}

		public float BumpDistanceDefault => 0.01f;
		public virtual float BumpDistance
		{
			get { return (float)RcPlugIn.Settings.GetDouble("bumpdistance", BumpDistanceDefault); }
			set { RcPlugIn.Settings.SetDouble("bumpdistance", value); }
		}
		public int SelectedDeviceDefault => -1;
		[Obsolete("Device selection setting has changed to be a string to cope with multi-devices.")]
		public virtual int SelectedDevice
		{
			get { return RcPlugIn.Settings.GetInteger("selecteddevice", SelectedDeviceDefault); }
			set { RcPlugIn.Settings.SetInteger("selecteddevice", value); }
		}

		public string SelectedDeviceStrDefault => "-1";
		public virtual string SelectedDeviceStr
		{
			get { return RcPlugIn.Settings.GetString("selecteddevicestr", SelectedDeviceStrDefault); }
			set { RcPlugIn.Settings.SetString("selecteddevicestr", value); }
		}

		public virtual string IntermediateSelectedDeviceStr
		{
			get { return RcPlugIn.Settings.GetString("intermediateselecteddevicestr", SelectedDeviceStr); }
			set { RcPlugIn.Settings.SetString("intermediateselecteddevicestr", value); }
		}

		public bool AllowSelectedDeviceOverrideDefault => false;
		public virtual bool AllowSelectedDeviceOverride
		{
			get { return RcPlugIn.Settings.GetBool("allowselecteddeviceoverride", AllowSelectedDeviceOverrideDefault); }
			set { RcPlugIn.Settings.SetBool("allowselecteddeviceoverride", value); }
		}

		public bool UseStartResolutionDefault => true;
		public bool UseStartResolution
		{
			get { return RcPlugIn.Settings.GetBool("usestartresolution", UseStartResolutionDefault); }
			set { RcPlugIn.Settings.SetBool("usestartresolution", value); }
		}

		public int StartResolutionDefault => 64;
		public int StartResolution
		{
			get { return UseStartResolution ? RcPlugIn.Settings.GetInteger("startresolution", StartResolutionDefault) : int.MaxValue; }
			set { RcPlugIn.Settings.SetInteger("startresolution", value); }
		}

		public virtual IntegratorMethod IntegratorMethod { get; set; }
		public int MinBounceDefault => 3;
		public virtual int MinBounce
		{
			get { return RcPlugIn.Settings.GetInteger("minbounce", MinBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("minbounce", value); }
		}
		public int MaxBounceDefault => 128;
		public virtual int MaxBounce
		{
			get { return RcPlugIn.Settings.GetInteger("maxbounce", MaxBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("maxbounce", value); }
		}
		public bool NoCausticsDefault => false;
		public virtual bool NoCaustics
		{
			get { return RcPlugIn.Settings.GetBool("nocaustics", NoCausticsDefault); }
			set { RcPlugIn.Settings.SetBool("nocaustics", value); }
		}
		public int MaxDiffuseBounceDefault => 2;
		public virtual int MaxDiffuseBounce
		{
			get { return RcPlugIn.Settings.GetInteger("maxdiffusebounce", MaxDiffuseBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("maxdiffusebounce", value); }
		}
		public int MaxGlossyBounceDefault => 32;
		public virtual int MaxGlossyBounce
		{
			get { return RcPlugIn.Settings.GetInteger("maxglossybounce", MaxGlossyBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("maxglossybounce", value); }
		}
		public int MaxTransmissionBounceDefault => 32;
		public virtual int MaxTransmissionBounce
		{
			get { return RcPlugIn.Settings.GetInteger("maxtransmissionbounce", MaxTransmissionBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("maxtransmissionbounce", value); }
		}
		public int MaxVolumeBounceDefault => 32;
		public virtual int MaxVolumeBounce
		{
			get { return RcPlugIn.Settings.GetInteger("maxglossybounce", MaxVolumeBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("maxglossybounce", value); }
		}
		public int AaSamplesDefault => 8;
		public virtual int AaSamples
		{
			get { return RcPlugIn.Settings.GetInteger("aasamples", AaSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("aasamples", value); }
		}
		public int DiffuseSamplesDefault => 128;
		public virtual int DiffuseSamples
		{
			get { return RcPlugIn.Settings.GetInteger("diffusesamples", DiffuseSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("diffusesamples", value); }
		}
		public int GlossySamplesDefault => 128;
		public virtual int GlossySamples
		{
			get { return RcPlugIn.Settings.GetInteger("glossysamples", GlossySamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("glossysamples", value); }
		}
		public int TransmissionSamplesDefault => 128;
		public virtual int TransmissionSamples
		{
			get { return RcPlugIn.Settings.GetInteger("transmissionsamples", TransmissionSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("transmissionsamples", value); }
		}
		public int AoSamplesDefault => 2;
		public virtual int AoSamples
		{
			get { return RcPlugIn.Settings.GetInteger("aosamples", AoSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("aosamples", value); }
		}
		public int MeshLightSamplesDefault => 2;
		public virtual int MeshLightSamples
		{
			get { return RcPlugIn.Settings.GetInteger("meshlightsamples", MeshLightSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("meshlightsamples", value); }
		}
		public int SubsurfaceSamplesDefault => 2;
		public virtual int SubsurfaceSamples
		{
			get { return RcPlugIn.Settings.GetInteger("subsurfacesamples", SubsurfaceSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("subsurfacesamples", value); }
		}
		public int VolumeSamplesDefault => 2;
		public virtual int VolumeSamples
		{
			get { return RcPlugIn.Settings.GetInteger("volumesamples", VolumeSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("volumesamples", value); }
		}
		public int SamplesDefault => 1000;
		public virtual int Samples
		{
			get { return RcPlugIn.Settings.GetInteger("samples", SamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("samples", value); }
		}

		public int SeedDefault => 128;
		public virtual int Seed
		{
			get { return RcPlugIn.Settings.GetInteger("seed", SeedDefault); }
			set { RcPlugIn.Settings.SetInteger("seed", value); }
		}
		public virtual SamplingPattern SamplingPattern { get; set; }
		public float FilterGlossyDefault => 0.5f;
		public virtual float FilterGlossy
		{
			get { return (float)RcPlugIn.Settings.GetDouble("filterglossy", FilterGlossyDefault); }
			set { RcPlugIn.Settings.SetDouble("filterglossy", value); }
		}

		public float SampleClampDirectDefault => 1.0f;
		public virtual float SampleClampDirect
		{
			get { return (float)RcPlugIn.Settings.GetDouble("sampleclampdirect", SampleClampDirectDefault); }
			set { RcPlugIn.Settings.SetDouble("sampleclampdirect", value); }
		}
		public float SampleClampIndirectDefault => 5.0f;
		public virtual float SampleClampIndirect
		{
			get { return (float)RcPlugIn.Settings.GetDouble("sampleclampindirect", SampleClampIndirectDefault); }
			set { RcPlugIn.Settings.SetDouble("sampleclampindirect", value); }
		}

		public float LightSamplingThresholdDefault => 0.05f;
		public virtual float LightSamplingThreshold
		{
			get { return (float)RcPlugIn.Settings.GetDouble("lightsamplingthreshold", LightSamplingThresholdDefault); }
			set { RcPlugIn.Settings.SetDouble("lightsamplingthreshold", value); }
		}

		public bool SampleAllLightsDefault => true;
		public virtual bool SampleAllLights
		{
			get { return RcPlugIn.Settings.GetBool("samplealllights", SampleAllLightsDefault); }
			set { RcPlugIn.Settings.SetBool("samplealllights", value); }
		}

		public bool SampleAllLightsIndirectDefault => true;
		public virtual bool SampleAllLightsIndirect
		{
			get { return RcPlugIn.Settings.GetBool("samplealllightsindirect", SampleAllLightsIndirectDefault); }
			set { RcPlugIn.Settings.SetBool("samplealllightsindirect", value); }
		}

		public float SensorWidthDefault => 32.0f;
		public virtual float SensorWidth
		{
			get { return (float)RcPlugIn.Settings.GetDouble("sensorwidth", SensorWidthDefault); }
			set { RcPlugIn.Settings.SetDouble("sensorwidth", value); }
		}
		public float SensorHeightDefault => 18.0f;
		public virtual float SensorHeight
		{
			get { return (float)RcPlugIn.Settings.GetDouble("sensorheight", SensorHeightDefault); }
			set { RcPlugIn.Settings.SetDouble("sensorheight", value); }
		}

		public int TransparentMinBounceDefault => 128;
		public virtual int TransparentMinBounce
		{
			get { return RcPlugIn.Settings.GetInteger("transparentminbounce", TransparentMinBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("transparentminbounce", value); }
		}
		public int TransparentMaxBounceDefault => 128;
		public virtual int TransparentMaxBounce
		{
			get { return RcPlugIn.Settings.GetInteger("transparentmaxbounce", TransparentMaxBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("transparentmaxbounce", value); }
		}
		public bool TransparentShadowsDefault => true;
		public virtual bool TransparentShadows
		{
			get { return RcPlugIn.Settings.GetBool("transparentshadows", TransparentShadowsDefault); }
			set { RcPlugIn.Settings.SetBool("transparentshadows", value); }
		}

		public bool AllowViewportSettingsOverrideDefault => false;
		public virtual bool AllowViewportSettingsOverride
		{
			get { return RcPlugIn.Settings.GetBool("allowviewportsettingsoverride", AllowViewportSettingsOverrideDefault); }
			set {
				var old = AllowViewportSettingsOverride;
				if (old != value) {
					RcPlugIn.Settings.SetBool("allowviewportsettingsoverride", value);
					TriggerApplicationSettingsChanged();
				}
			}
		}
		public bool UseDrawOpenGlDefault => true;
		public virtual bool UseDrawOpenGl
		{
			get { return RcPlugIn.Settings.GetBool("UseDrawOpenGl", UseDrawOpenGlDefault); }
			set {
				var old = UseDrawOpenGl;
				if (old != value) {
					RcPlugIn.Settings.SetBool("UseDrawOpenGl", value);
					TriggerApplicationSettingsChanged();
				}
			}
		}

		public int OpenClDeviceTypeDefault => 0;
		public int OpenClDeviceType
		{
			get { return RcPlugIn.Settings.GetInteger("OpenCLDeviceType", OpenClDeviceTypeDefault); }
			set
			{
				var old = OpenClDeviceType;
				if (old != value)
				{
					RcPlugIn.Settings.SetInteger("OpenCLDeviceType", value);
					TriggerApplicationSettingsChanged();
				}
			}
		}

		public bool OpenClSingleProgramDefault => true;
		public bool OpenClSingleProgram
		{
			get { return RcPlugIn.Settings.GetBool("OpenCLSingleProgram", OpenClSingleProgramDefault); }
			set
			{
				var old = OpenClSingleProgram;
				if (old != value)
				{
					RcPlugIn.Settings.SetBool("OpenCLSingleProgram", value);
					TriggerApplicationSettingsChanged();
				}
			}
		}
		public bool FullHdrSkylightDefault => false;
		public bool FullHdrSkylight
		{
			get { return RcPlugIn.Settings.GetBool("FullHdrSkylight", FullHdrSkylightDefault); }
			set
			{
				var old = FullHdrSkylight;
				if (old != value)
				{
					RcPlugIn.Settings.SetBool("FullHdrSkylight", value);
				}
			}
		}
		public bool NoShadowsDefault => false;
		public bool NoShadows
		{
			get { return RcPlugIn.Settings.GetBool("NoShadows", NoShadowsDefault); }
			set
			{
				var old = NoShadows;
				if (old != value)
				{
					RcPlugIn.Settings.SetBool("NoShadows", value);
				}
			}
		}

		public int OpenClKernelTypeDefault => -1;
		public int OpenClKernelType
		{
			get { return RcPlugIn.Settings.GetInteger("OpenCLKernelType", OpenClKernelTypeDefault); }
			set
			{
				var old = OpenClKernelType;
				if (old != value)
				{
					RcPlugIn.Settings.SetInteger("OpenCLKernelType", value);
					TriggerApplicationSettingsChanged();
				}
			}
		}
		public bool CPUSplitKernelDefault => false;
		public bool CPUSplitKernel
		{
			get { return RcPlugIn.Settings.GetBool("CPUSplitKernel", CPUSplitKernelDefault); }
			set
			{
				var old = CPUSplitKernel;
				if (old != value)
				{
					RcPlugIn.Settings.SetBool("CPUSplitKernel", value);
					TriggerApplicationSettingsChanged();
				}
			}
		}

		private void TriggerApplicationSettingsChanged()
		{
			ApplicationSettingsChanged?.Invoke(this, new ApplicationChangedEventArgs(this));
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

		public event EventHandler<ApplicationChangedEventArgs> ApplicationSettingsChanged;
	}

	public interface IAllSettings : IApplicationSettings, IViewportSettings { }

	public interface IApplicationSettings
	{
		bool AllowViewportSettingsOverride { get; set; }

		bool UseDrawOpenGl { get; set; }

		int OpenClDeviceType { get; set; }
		bool OpenClSingleProgram { get; set; }
		int OpenClKernelType { get; set; }

		bool CPUSplitKernel { get; set; }

		bool FullHdrSkylight { get; set; }

		bool NoShadows { get; set; }
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

		bool UseStartResolution { get; set; }

		int StartResolution { get; set; }

		string SelectedDeviceStr { get; set; }

		string IntermediateSelectedDeviceStr { get; set; }

		bool AllowSelectedDeviceOverride { get; }
		Device RenderDevice { get; }

		uint IntegratorHash { get; }
	}

	public class ApplicationChangedEventArgs : EventArgs
	{
		public IApplicationSettings Settings { get; private set; }
		public ApplicationChangedEventArgs(IApplicationSettings aps) { Settings = aps; }
	}
	public class ViewportSettingsChangedArgs : EventArgs
	{
		public IViewportSettings Settings { get; private set; }
		public ViewportSettingsChangedArgs(IViewportSettings vps) { Settings = vps; }
	}
}
