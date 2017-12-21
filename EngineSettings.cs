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

			DpiScale = DpiScale;

			TileX = TileX;
			TileY = TileY;

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

			Blades = Blades;
			BladesRotation = BladesRotation;
			ApertureRatio = ApertureRatio;

			SensorWidth = SensorWidth;
			SensorHeight = SensorHeight;

			TransparentMaxBounce = TransparentMaxBounce;

			// application settings
			AllowViewportSettingsOverride = AllowViewportSettingsOverride;
			UseDrawOpenGl = UseDrawOpenGl;
			UseFastDraw = UseFastDraw;
			OpenClDeviceType = OpenClDeviceType;
			OpenClKernelType = OpenClKernelType;
			CPUSplitKernel = CPUSplitKernel;
			OpenClSingleProgram = OpenClSingleProgram;
			NoShadows = NoShadows;
			RaytracedClippingPlanes = RaytracedClippingPlanes;
			SaveDebugImages = SaveDebugImages;
			DebugSimpleShaders = DebugSimpleShaders;
			DebugNoOverrideTileSize = DebugNoOverrideTileSize;
			FlushAtEndOfCreateWorld = FlushAtEndOfCreateWorld;
			PreviewSamples = PreviewSamples;
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

			DpiScale = DpiScaleDefault;

			TileX = TileXDefault;
			TileY = TileYDefault;

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

			Blades = BladesDefault;
			BladesRotation = BladesRotationDefault;
			ApertureRatio = ApertureRatioDefault;

			SensorWidth = SensorWidthDefault;
			SensorHeight = SensorHeightDefault;

			TransparentMaxBounce = TransparentMaxBounceDefault;

			// application settings
			AllowViewportSettingsOverride = AllowViewportSettingsOverrideDefault;
			UseDrawOpenGl = UseDrawOpenGlDefault;
			UseFastDraw = UseFastDrawDefault;
			OpenClDeviceType = OpenClDeviceTypeDefault;
			OpenClKernelType = OpenClKernelTypeDefault;
			CPUSplitKernel = CPUSplitKernelDefault;
			OpenClSingleProgram = OpenClSingleProgramDefault;
			NoShadows = NoShadowsDefault;
			RaytracedClippingPlanes = RaytracedClippingPlanesDefault;
			SaveDebugImages = SaveDebugImagesDefault;
			DebugSimpleShaders = DebugSimpleShadersDefault;
			DebugNoOverrideTileSize = DebugNoOverrideTileSizeDefault;
			FlushAtEndOfCreateWorld = FlushAtEndOfCreateWorldDefault;
			PreviewSamples = PreviewSamplesDefault;
		}

		public bool RenderDeviceIsCuda => RenderDevice.IsMultiCuda || RenderDevice.IsCuda;

		public bool RenderDeviceIsOpenCl => RenderDevice.IsMultiOpenCl || RenderDevice.IsOpenCl;

		public Device RenderDevice
		{
			get
			{
				return Device.DeviceFromString(Device.ValidDeviceString(SelectedDeviceStr));
			}
		}

		public bool SaveDebugImagesDefault => false;

		public virtual bool SaveDebugImages
		{
			get { return RcPlugIn.Settings.GetBool("SaveDebugImages", SaveDebugImagesDefault); }
			set { RcPlugIn.Settings.SetBool("SaveDebugImages", value); }
		}

		public bool DebugSimpleShadersDefault => false;

		public virtual bool DebugSimpleShaders
		{
			get { return RcPlugIn.Settings.GetBool("DebugSimpleShaders", DebugSimpleShadersDefault); }
			set { RcPlugIn.Settings.SetBool("DebugSimpleShaders", value); }
		}

		public bool DebugNoOverrideTileSizeDefault => false;

		public virtual bool DebugNoOverrideTileSize
		{
			get { return RcPlugIn.Settings.GetBool("DebugNoOverrideTileSize", DebugNoOverrideTileSizeDefault); }
			set { RcPlugIn.Settings.SetBool("DebugNoOverrideTileSize", value); }
		}

		public bool FlushAtEndOfCreateWorldDefault => false;
		public virtual bool FlushAtEndOfCreateWorld
		{
			get { return RcPlugIn.Settings.GetBool("FlushAtEndOfCreateWorld", FlushAtEndOfCreateWorldDefault); }
			set { RcPlugIn.Settings.SetBool("FlushAtEndOfCreateWorld", value); }
		}

		public int PreviewSamplesDefault => 50;
		public virtual int PreviewSamples
		{
			get { return RcPlugIn.Settings.GetInteger("PreviewSamples", PreviewSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("PreviewSamples", value); }

		}

		public bool ShowViewportPropertiesPanelDefault => false;
		public virtual bool ShowViewportPropertiesPanel
		{
			get { return RcPlugIn.Settings.GetBool("ShowViewPropertiesDialog", ShowViewportPropertiesPanelDefault); }
			set { RcPlugIn.Settings.SetBool("ShowViewPropertiesDialog", value); }
		}

		public bool VerboseDefault => false;
		public virtual bool Verbose
		{
			get { return RcPlugIn.Settings.GetBool("Verbose", VerboseDefault); }
			set { RcPlugIn.Settings.SetBool("Verbose", value); }
		}

		public bool ShowMaxPassesDefault = false;
		/// <summary>
		/// Set to true to show the maximum passes count in the HUD, i.e. 48/100. For
		/// false it would show just 48.
		/// </summary>
		public virtual bool ShowMaxPasses
		{
			get { return RcPlugIn.Settings.GetBool("MaxPasses", ShowMaxPassesDefault); }
			set { RcPlugIn.Settings.SetBool("MaxPasses", value); }
		}

		public float SpotlightFactorDefault => 40.0f;
		public virtual float SpotlightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble("SpotLightFactor", SpotlightFactorDefault); }
			set { RcPlugIn.Settings.SetDouble("SpotLightFactor", value); }
		}

		public float PointlightFactorDefault => 40.0f;
		public virtual float PointlightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble("PointLightFactor", PointlightFactorDefault); }
			set { RcPlugIn.Settings.SetDouble("PointLightFactor", value); }
		}

		public float SunlightFactorDefault => 3.2f;
		public virtual float SunlightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble("SunLightFactor", SunlightFactorDefault); }
			set { RcPlugIn.Settings.SetDouble("SunLightFactor", value); }
		}

		public float ArealightFactorDefault => 17.2f;
		public virtual float ArealightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble("AreaLightFactor", ArealightFactorDefault); }
			set { RcPlugIn.Settings.SetDouble("AreaLightFactor", value); }
		}

		public float PolishFactorDefault => 0.09f;
		public virtual float PolishFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble("PolishFactor", PolishFactorDefault); }
			set { RcPlugIn.Settings.SetDouble("PolishFactor", value); }
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
			get { return RcPlugIn.Settings.GetInteger("ThrottleMs", ThrottleMsDefault); }
			set { RcPlugIn.Settings.SetInteger("ThrottleMs", value); }
		}

		public int FadeInMsDefault => 10;
		/// <summary>
		/// Speed of result fade-in. Higher values is slower fade in. In milliseconds.
		/// Denotes time to wait between 1% increments towards full render result.
		/// </summary>
		public virtual int FadeInMs
		{
			get { return RcPlugIn.Settings.GetInteger("FadeInMs", FadeInMsDefault); }
			set { RcPlugIn.Settings.SetInteger("FadeInMs", value); }
		}

		public int ThreadsDefault => Math.Max(1, Environment.ProcessorCount - 2);
		/// <summary>
		/// Set the amount of rendering threads to create. Especially useful for CPU rendering where
		/// one doesn't want to use 100% CPU to retain responsiveness. By default set to
		/// (logical) processor count - 2, at minimum 1.
		/// </summary>
		public virtual int Threads
		{
			get { return RcPlugIn.Settings.GetInteger("Threads", ThreadsDefault); }
			set { RcPlugIn.Settings.SetInteger("Threads", value); }
		}

		public int TileXDefault => 128;
		/// <summary>
		/// Set the width of the render tile.
		/// </summary>
		public virtual int TileX
		{
			get { return RcPlugIn.Settings.GetInteger("TileX", TileXDefault); }
			set { RcPlugIn.Settings.SetInteger("TileX", value); }
		}

		public int TileYDefault => 128;
		/// <summary>
		/// Set the height of the render tile.
		/// </summary>
		public virtual int TileY
		{
			get { return RcPlugIn.Settings.GetInteger("TileY", TileYDefault); }
			set { RcPlugIn.Settings.SetInteger("TileY", value); }
		}

		public float BumpDistanceDefault => 0.01f;
		public virtual float BumpDistance
		{
			get { return (float)RcPlugIn.Settings.GetDouble("BumpDistance", BumpDistanceDefault); }
			set { RcPlugIn.Settings.SetDouble("BumpDistance", value); }
		}
		public int SelectedDeviceDefault => -1;
		[Obsolete("Device selection setting has changed to be a string to cope with multi-devices.")]
		public virtual int SelectedDevice
		{
			get { return RcPlugIn.Settings.GetInteger("SelectedDevice", SelectedDeviceDefault); }
			set { RcPlugIn.Settings.SetInteger("SelectedDevice", value); }
		}

		public string SelectedDeviceStrDefault => "-1";
		public virtual string SelectedDeviceStr
		{
			get { return RcPlugIn.Settings.GetString("SelectedDeviceStr", SelectedDeviceStrDefault); }
			set { RcPlugIn.Settings.SetString("SelectedDeviceStr", value); }
		}

		public virtual string IntermediateSelectedDeviceStr
		{
			get { return RcPlugIn.Settings.GetString("IntermediateSelectedDeviceStr", SelectedDeviceStr); }
			set { RcPlugIn.Settings.SetString("IntermediateSelectedDeviceStr", value); }
		}

		public bool AllowSelectedDeviceOverrideDefault => false;
		public virtual bool AllowSelectedDeviceOverride
		{
			get { return RcPlugIn.Settings.GetBool("AllowSelectedDeviceOverride", AllowSelectedDeviceOverrideDefault); }
			set { RcPlugIn.Settings.SetBool("AllowSelectedDeviceOverride", value); }
		}

		public bool UseStartResolutionDefault => RenderEngine.OnHighDpi ? true : false;
		public bool UseStartResolution
		{
			get { return RcPlugIn.Settings.GetBool("UseStartResolution", UseStartResolutionDefault); }
			set { RcPlugIn.Settings.SetBool("UseStartResolution", value); }
		}

		public int StartResolutionDefault => 64;
		public int StartResolution
		{
			get { return RcPlugIn.Settings.GetInteger("StartResolution", StartResolutionDefault); }
			set { if (value < 1) value = 1;  RcPlugIn.Settings.SetInteger("StartResolution", value); }
		}

		public float DpiScaleDefault => 1.0f;
		public float DpiScale
		{
			get { return (float)RcPlugIn.Settings.GetDouble("DpiScale", DpiScaleDefault); }
			set { RcPlugIn.Settings.SetDouble("DpiScale", value); }
		}

		public virtual IntegratorMethod IntegratorMethod { get; set; }

		public int MaxBounceDefault => 32;
		public virtual int MaxBounce
		{
			get { return RcPlugIn.Settings.GetInteger("MaxBounce", MaxBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("MaxBounce", value); }
		}
		public bool NoCausticsDefault => false;
		public virtual bool NoCaustics
		{
			get { return RcPlugIn.Settings.GetBool("NoCaustics", NoCausticsDefault); }
			set { RcPlugIn.Settings.SetBool("NoCaustics", value); }
		}
		public int MaxDiffuseBounceDefault => 2;
		public virtual int MaxDiffuseBounce
		{
			get { return RcPlugIn.Settings.GetInteger("MaxDiffuseBounce", MaxDiffuseBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("MaxDiffuseBounce", value); }
		}
		public int MaxGlossyBounceDefault => 32;
		public virtual int MaxGlossyBounce
		{
			get { return RcPlugIn.Settings.GetInteger("MaxGlossyBounce", MaxGlossyBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("MaxGlossyBounce", value); }
		}
		public int MaxTransmissionBounceDefault => 32;
		public virtual int MaxTransmissionBounce
		{
			get { return RcPlugIn.Settings.GetInteger("MaxTransmissionBounce", MaxTransmissionBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("MaxTransmissionBounce", value); }
		}
		public int MaxVolumeBounceDefault => 32;
		public virtual int MaxVolumeBounce
		{
			get { return RcPlugIn.Settings.GetInteger("MaxVolumeBounce", MaxVolumeBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("MaxVolumeBounce", value); }
		}
		public int AaSamplesDefault => 8;
		public virtual int AaSamples
		{
			get { return RcPlugIn.Settings.GetInteger("AaSamples", AaSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("AaSamples", value); }
		}
		public int DiffuseSamplesDefault => 128;
		public virtual int DiffuseSamples
		{
			get { return RcPlugIn.Settings.GetInteger("DiffuseSamples", DiffuseSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("DiffuseSamples", value); }
		}
		public int GlossySamplesDefault => 128;
		public virtual int GlossySamples
		{
			get { return RcPlugIn.Settings.GetInteger("GlossySamples", GlossySamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("GlossySamples", value); }
		}
		public int TransmissionSamplesDefault => 128;
		public virtual int TransmissionSamples
		{
			get { return RcPlugIn.Settings.GetInteger("TransmissionSamples", TransmissionSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("TransmissionSamples", value); }
		}
		public int AoSamplesDefault => 2;
		public virtual int AoSamples
		{
			get { return RcPlugIn.Settings.GetInteger("AoSamples", AoSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("AoSamples", value); }
		}
		public int MeshLightSamplesDefault => 2;
		public virtual int MeshLightSamples
		{
			get { return RcPlugIn.Settings.GetInteger("MeshLightSamples", MeshLightSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("MeshLightSamples", value); }
		}
		public int SubsurfaceSamplesDefault => 2;
		public virtual int SubsurfaceSamples
		{
			get { return RcPlugIn.Settings.GetInteger("SubSurfaceSamples", SubsurfaceSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("SubSurfaceSamples", value); }
		}
		public int VolumeSamplesDefault => 2;
		public virtual int VolumeSamples
		{
			get { return RcPlugIn.Settings.GetInteger("VolumeSamples", VolumeSamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("VolumeSamples", value); }
		}
		public int SamplesDefault => 1000;
		public virtual int Samples
		{
			get { return RcPlugIn.Settings.GetInteger("Samples", SamplesDefault); }
			set { RcPlugIn.Settings.SetInteger("Samples", value); }
		}

		public int SeedDefault => 128;
		public virtual int Seed
		{
			get { return RcPlugIn.Settings.GetInteger("Seed", SeedDefault); }
			set { RcPlugIn.Settings.SetInteger("Seed", value); }
		}
		public virtual SamplingPattern SamplingPattern { get; set; }
		public float FilterGlossyDefault => 0.5f;
		public virtual float FilterGlossy
		{
			get { return (float)RcPlugIn.Settings.GetDouble("FilterGlossy", FilterGlossyDefault); }
			set { RcPlugIn.Settings.SetDouble("FilterGlossy", value); }
		}

		public float SampleClampDirectDefault => 0.0f;
		public virtual float SampleClampDirect
		{
			get { return (float)RcPlugIn.Settings.GetDouble("SampleClampDirect", SampleClampDirectDefault); }
			set { RcPlugIn.Settings.SetDouble("SampleClampDirect", value); }
		}
		public float SampleClampIndirectDefault => 0.0f;
		public virtual float SampleClampIndirect
		{
			get { return (float)RcPlugIn.Settings.GetDouble("SampleClampIndirect", SampleClampIndirectDefault); }
			set { RcPlugIn.Settings.SetDouble("SampleClampIndirect", value); }
		}

		public float LightSamplingThresholdDefault => 0.05f;
		public virtual float LightSamplingThreshold
		{
			get { return (float)RcPlugIn.Settings.GetDouble("LightSamplingThreshold", LightSamplingThresholdDefault); }
			set { RcPlugIn.Settings.SetDouble("LightSamplingThreshold", value); }
		}

		public bool SampleAllLightsDefault => true;
		public virtual bool SampleAllLights
		{
			get { return RcPlugIn.Settings.GetBool("SampleAllLights", SampleAllLightsDefault); }
			set { RcPlugIn.Settings.SetBool("SampleAllLights", value); }
		}

		public bool SampleAllLightsIndirectDefault => true;
		public virtual bool SampleAllLightsIndirect
		{
			get { return RcPlugIn.Settings.GetBool("SampleAllLightsIndirect", SampleAllLightsIndirectDefault); }
			set { RcPlugIn.Settings.SetBool("SampleAllLightsIndirect", value); }
		}

		public uint BladesDefault => 0;
		public virtual uint Blades
		{
			get { return (uint)RcPlugIn.Settings.GetInteger("Blades", (int)BladesDefault); }
			set { RcPlugIn.Settings.SetInteger("Blades", (int)value); }
		}

		public float BladesRotationDefault => 0.0f;
		public virtual float BladesRotation
		{
			get { return (float)RcPlugIn.Settings.GetDouble("BladesRotation", BladesRotationDefault); }
			set { RcPlugIn.Settings.SetDouble("BladesRotation", value); }
		}
		public float ApertureRatioDefault => 1.0f;
		public virtual float ApertureRatio
		{
			get { return (float)RcPlugIn.Settings.GetDouble("ApertureRatio", ApertureRatioDefault); }
			set { RcPlugIn.Settings.SetDouble("ApertureRatio", value); }
		}

		public float SensorWidthDefault => 32.0f;
		public virtual float SensorWidth
		{
			get { return (float)RcPlugIn.Settings.GetDouble("SensorWidth", SensorWidthDefault); }
			set { RcPlugIn.Settings.SetDouble("SensorWidth", value); }
		}
		public float SensorHeightDefault => 18.0f;
		public virtual float SensorHeight
		{
			get { return (float)RcPlugIn.Settings.GetDouble("SensorHeight", SensorHeightDefault); }
			set { RcPlugIn.Settings.SetDouble("SensorHeight", value); }
		}

		public int TransparentMaxBounceDefault => 128;
		public virtual int TransparentMaxBounce
		{
			get { return RcPlugIn.Settings.GetInteger("TransparentMaxBounce", TransparentMaxBounceDefault); }
			set { RcPlugIn.Settings.SetInteger("TransparentMaxBounce", value); }
		}

		public bool AllowViewportSettingsOverrideDefault => false;
		public virtual bool AllowViewportSettingsOverride
		{
			get { return RcPlugIn.Settings.GetBool("AllowViewportSettingsOverride", AllowViewportSettingsOverrideDefault); }
			set {
				var old = AllowViewportSettingsOverride;
				if (old != value) {
					RcPlugIn.Settings.SetBool("AllowViewportSettingsOverride", value);
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
		public bool UseFastDrawDefault => false;
		public virtual bool UseFastDraw
		{
			get { return RcPlugIn.Settings.GetBool("UseFastDraw", UseFastDrawDefault); }
			set {
				var old = UseFastDraw;
				if (old != value) {
					RcPlugIn.Settings.SetBool("UseFastDraw", value);
				}
			}
		}

		public int OpenClDeviceTypeDefault => 4;
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
		public bool RaytracedClippingPlanesDefault => false;
		public bool RaytracedClippingPlanes
		{
			get { return RcPlugIn.Settings.GetBool("RaytracedClippingPlanes", RaytracedClippingPlanesDefault); }
			set
			{
				var old = RaytracedClippingPlanes;
				if (old != value)
				{
					RcPlugIn.Settings.SetBool("RaytracedClippingPlanes", value);
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
				rem = RhinoMath.CRC32(rem, MaxBounce);
				rem = RhinoMath.CRC32(rem, MaxDiffuseBounce);
				rem = RhinoMath.CRC32(rem, MaxGlossyBounce);
				rem = RhinoMath.CRC32(rem, MaxVolumeBounce);
				rem = RhinoMath.CRC32(rem, MaxTransmissionBounce);
				rem = RhinoMath.CRC32(rem, TransparentMaxBounce);

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

		bool NoShadows { get; set; }

		float DpiScale { get; set; }

		bool RaytracedClippingPlanes { get; set; }

		int PreviewSamples { get; set; }
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
		int MaxBounce { get; set; }
		int MaxDiffuseBounce { get; set; }
		int MaxGlossyBounce { get; set; }
		int MaxVolumeBounce { get; set; }
		int MaxTransmissionBounce { get; set; }
		int TransparentMaxBounce { get; set; }

		bool UseStartResolution { get; set; }

		int StartResolution { get; set; }

		bool UseFastDraw { get; set; }

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
