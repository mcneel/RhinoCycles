/**
Copyright 2014-2021 Robert McNeel and Associates

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

namespace RhinoCyclesCore.Settings
{
	public class ApplicationAndDocumentSettings : IAllSettings
	{
		public static readonly PlugIn RcPlugIn = PlugIn.Find(new Guid("9BC28E9E-7A6C-4B8F-A0C6-3D05E02D1B97"));

		public ApplicationAndDocumentSettings()
		{
			IntegratorMethod = IntegratorMethod.Path;
			SamplingPattern = SamplingPattern.CMJ;

			// persisted settings
			Verbose = Verbose;

			SpotLightFactor = SpotLightFactor;
			PointLightFactor = PointLightFactor;
			SunLightFactor = SunLightFactor;
			LinearLightFactor = LinearLightFactor;
			AreaLightFactor = AreaLightFactor;
			PolishFactor = PolishFactor;

			ThrottleMs = ThrottleMs;
			Threads = Threads;
			BumpDistance = BumpDistance;
			NormalStrengthFactor = NormalStrengthFactor;
			BumpStrengthFactor = BumpStrengthFactor;

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
			CausticsReflective = CausticsReflective;
			CausticsRefractive = CausticsRefractive;

			MaxDiffuseBounce = MaxDiffuseBounce;
			MaxGlossyBounce = MaxGlossyBounce;
			MaxTransmissionBounce = MaxTransmissionBounce;

			MaxVolumeBounce = MaxVolumeBounce;

			AaSamples = AaSamples;

			DiffuseSamples = DiffuseSamples;
			GlossySamples = GlossySamples;
			TransmissionSamples = TransmissionSamples;

			AoBounces = AoBounces;
			AoAdditiveFactor = AoAdditiveFactor;
			AoDistance = AoDistance;
			AoFactor = AoFactor;

			MeshLightSamples = MeshLightSamples;
			SubsurfaceSamples = SubsurfaceSamples;
			VolumeSamples = VolumeSamples;

			Samples = Samples;
			Seed = Seed;

			FilterGlossy = FilterGlossy;

			SampleClampDirect = SampleClampDirect;
			SampleClampIndirect = SampleClampIndirect;
			LightSamplingThreshold = LightSamplingThreshold;

			UseDirectLight = UseDirectLight;
			UseIndirectLight = UseIndirectLight;

			Blades = Blades;
			BladesRotation = BladesRotation;
			ApertureRatio = ApertureRatio;
			ApertureFactor = ApertureFactor;

			SensorWidth = SensorWidth;
			SensorHeight = SensorHeight;

			TransparentMaxBounce = TransparentMaxBounce;

			SssMethod = SssMethod;

			// application settings
			OpenClDeviceType = OpenClDeviceType;
			OpenClKernelType = OpenClKernelType;
			CPUSplitKernel = CPUSplitKernel;
			OpenClSingleProgram = OpenClSingleProgram;
			NoShadows = NoShadows;
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
			Verbose = DefaultEngineSettings.Verbose;

			SpotLightFactor = DefaultEngineSettings.SpotLightFactor;
			PointLightFactor = DefaultEngineSettings.PointLightFactor;
			SunLightFactor = DefaultEngineSettings.SunLightFactor;
			LinearLightFactor = DefaultEngineSettings.LinearLightFactor;
			AreaLightFactor = DefaultEngineSettings.AreaLightFactor;
			PolishFactor = DefaultEngineSettings.PolishFactor;

			ThrottleMs = DefaultEngineSettings.ThrottleMs;
			Threads = DefaultEngineSettings.Threads;
			BumpDistance = DefaultEngineSettings.BumpDistance;
			NormalStrengthFactor = DefaultEngineSettings.NormalStrengthFactor;
			BumpStrengthFactor = DefaultEngineSettings.BumpStrengthFactor;

			SelectedDeviceStr = DefaultEngineSettings.SelectedDeviceStr;
			IntermediateSelectedDeviceStr = DefaultEngineSettings.SelectedDeviceStr;
			AllowSelectedDeviceOverride = DefaultEngineSettings.AllowSelectedDeviceOverride;

			UseStartResolution = DefaultEngineSettings.UseStartResolution;
			StartResolution = DefaultEngineSettings.StartResolution;

			DpiScale = DefaultEngineSettings.DpiScale;

			TileX = DefaultEngineSettings.TileX;
			TileY = DefaultEngineSettings.TileY;

			MaxBounce = DefaultEngineSettings.MaxBounce;

			NoCaustics = DefaultEngineSettings.NoCaustics;
			CausticsReflective = DefaultEngineSettings.CausticsReflective;
			CausticsRefractive = DefaultEngineSettings.CausticsRefractive;

			MaxDiffuseBounce = DefaultEngineSettings.MaxDiffuseBounce;
			MaxGlossyBounce = DefaultEngineSettings.MaxGlossyBounce;
			MaxTransmissionBounce = DefaultEngineSettings.MaxTransmissionBounce;

			MaxVolumeBounce = DefaultEngineSettings.MaxVolumeBounce;

			AaSamples = DefaultEngineSettings.AaSamples;

			DiffuseSamples = DefaultEngineSettings.DiffuseSamples;
			GlossySamples = DefaultEngineSettings.GlossySamples;
			TransmissionSamples = DefaultEngineSettings.TransmissionSamples;

			AoBounces = DefaultEngineSettings.AoBounces;

			MeshLightSamples = DefaultEngineSettings.MeshLightSamples;
			SubsurfaceSamples = DefaultEngineSettings.SubSurfaceSamples;
			VolumeSamples = DefaultEngineSettings.VolumeSamples;

			Samples = DefaultEngineSettings.Samples;
			Seed = DefaultEngineSettings.Seed;

			FilterGlossy = DefaultEngineSettings.FilterGlossy;

			SampleClampDirect = DefaultEngineSettings.SampleClampDirect;
			SampleClampIndirect = DefaultEngineSettings.SampleClampIndirect;
			LightSamplingThreshold = DefaultEngineSettings.LightSamplingThreshold;

			UseDirectLight = DefaultEngineSettings.UseDirectLight;
			UseIndirectLight = DefaultEngineSettings.UseIndirectLight;

			Blades = DefaultEngineSettings.Blades;
			BladesRotation = DefaultEngineSettings.BladesRotation;
			ApertureRatio = DefaultEngineSettings.ApertureRatio;
			ApertureFactor = DefaultEngineSettings.ApertureFactor;

			SensorWidth = DefaultEngineSettings.SensorWidth;
			SensorHeight = DefaultEngineSettings.SensorHeight;

			TransparentMaxBounce = DefaultEngineSettings.TransparentMaxBounce;

			SssMethod = DefaultEngineSettings.SssMethod;

			// application settings
			OpenClDeviceType = DefaultEngineSettings.OpenClDeviceType;
			OpenClKernelType = DefaultEngineSettings.OpenClKernelType;
			CPUSplitKernel = DefaultEngineSettings.CPUSplitKernel;
			OpenClSingleProgram = DefaultEngineSettings.OpenClSingleProgram;
			NoShadows = DefaultEngineSettings.NoShadows;
			SaveDebugImages = DefaultEngineSettings.SaveDebugImages;
			DebugSimpleShaders = DefaultEngineSettings.DebugSimpleShaders;
			DebugNoOverrideTileSize = DefaultEngineSettings.DebugNoOverrideTileSize;
			FlushAtEndOfCreateWorld = DefaultEngineSettings.FlushAtEndOfCreateWorld;
			PreviewSamples = DefaultEngineSettings.PreviewSamples;
		}

		public bool RenderDeviceIsCuda => RenderDevice.IsMultiCuda || RenderDevice.IsCuda;

		public Device RenderDevice
		{
			get
			{
				return Device.DeviceFromString(Device.ValidDeviceString(SelectedDeviceStr));
			}
		}

		public virtual bool SaveDebugImages
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.SaveDebugImages, DefaultEngineSettings.SaveDebugImages); }
			set { RcPlugIn.Settings.SetBool(SettingNames.SaveDebugImages, value); }
		}

		public virtual bool DebugSimpleShaders
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.DebugSimpleShaders, DefaultEngineSettings.DebugSimpleShaders); }
			set { RcPlugIn.Settings.SetBool(SettingNames.DebugSimpleShaders, value); }
		}

		public virtual bool DebugNoOverrideTileSize
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.DebugNoOverrideTileSize, DefaultEngineSettings.DebugNoOverrideTileSize); }
			set { RcPlugIn.Settings.SetBool(SettingNames.DebugNoOverrideTileSize, value); }
		}

		public virtual bool FlushAtEndOfCreateWorld
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.FlushAtEndOfCreateWorld, DefaultEngineSettings.FlushAtEndOfCreateWorld); }
			set { RcPlugIn.Settings.SetBool(SettingNames.FlushAtEndOfCreateWorld, value); }
		}

		public virtual int PreviewSamples
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.PreviewSamples, DefaultEngineSettings.PreviewSamples); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.PreviewSamples, value); }

		}

		public virtual bool Verbose
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.Verbose, DefaultEngineSettings.Verbose); }
			set { RcPlugIn.Settings.SetBool(SettingNames.Verbose, value); }
		}

		/// <summary>
		/// Set to true to show the maximum passes count in the HUD, i.e. 48/100. For
		/// false it would show just 48.
		/// </summary>
		public virtual bool ShowMaxPasses
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.MaxPasses, DefaultEngineSettings.ShowMaxPasses); }
			set { RcPlugIn.Settings.SetBool(SettingNames.MaxPasses, value); }
		}

		public virtual float SpotLightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.SpotLightFactor, DefaultEngineSettings.SpotLightFactor); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.SpotLightFactor, value); }
		}

		public virtual float PointLightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.PointLightFactor, DefaultEngineSettings.PointLightFactor); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.PointLightFactor, value); }
		}

		public virtual float SunLightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.SunLightFactor, DefaultEngineSettings.SunLightFactor); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.SunLightFactor, value); }
		}

		public virtual float LinearLightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.LinearLightFactor, DefaultEngineSettings.LinearLightFactor); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.LinearLightFactor, value); }
		}

		public virtual float AreaLightFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.AreaLightFactor, DefaultEngineSettings.AreaLightFactor); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.AreaLightFactor, value); }
		}

		public virtual float PolishFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.PolishFactor, DefaultEngineSettings.PolishFactor); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.PolishFactor, value); }
		}

		/// <summary>
		/// On systems where the (only) GPU is the primary device it can happen that
		/// the system becomes very sluggish while using Raytraced. Set this to a number
		/// greater than zero to introduce a throttle. Note that the number is a sleep
		/// duration in milliseconds.
		/// </summary>
		public virtual int ThrottleMs
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.ThrottleMs, DefaultEngineSettings.ThrottleMs); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.ThrottleMs, value); }
		}

		/// <summary>
		/// Set the amount of rendering threads to create. Especially useful for Cpu rendering where
		/// one doesn't want to use 100% Cpu to retain responsiveness. By default set to
		/// (logical) processor count - 2, at minimum 1.
		/// </summary>
		public virtual int Threads
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.Threads, DefaultEngineSettings.Threads); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.Threads, value); }
		}

		/// <summary>
		/// Set the width of the render tile.
		/// </summary>
		public virtual int TileX
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.TileX, DefaultEngineSettings.TileX); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.TileX, value); }
		}

		/// <summary>
		/// Set the height of the render tile.
		/// </summary>
		public virtual int TileY
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.TileY, DefaultEngineSettings.TileY); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.TileY, value); }
		}

		public virtual float NormalStrengthFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.NormalStrengthFactor, DefaultEngineSettings.NormalStrengthFactor); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.NormalStrengthFactor, value); }
		}

		public virtual float BumpStrengthFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.BumpStrengthFactor, DefaultEngineSettings.BumpStrengthFactor); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.BumpStrengthFactor, value); }
		}

		public virtual float BumpDistance
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.BumpDistance, DefaultEngineSettings.BumpDistance); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.BumpDistance, value); }
		}

		public virtual string SelectedDeviceStr
		{
			get { return RcPlugIn.Settings.GetString(SettingNames.SelectedDeviceStr, DefaultEngineSettings.SelectedDeviceStr); }
			set { RcPlugIn.Settings.SetString(SettingNames.SelectedDeviceStr, value); }
		}

		public virtual string IntermediateSelectedDeviceStr
		{
			get { return RcPlugIn.Settings.GetString(SettingNames.IntermediateSelectedDeviceStr, SelectedDeviceStr); }
			set { RcPlugIn.Settings.SetString(SettingNames.IntermediateSelectedDeviceStr, value); }
		}

		public virtual bool AllowSelectedDeviceOverride
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.AllowSelectedDeviceOverride, DefaultEngineSettings.AllowSelectedDeviceOverride); }
			set { RcPlugIn.Settings.SetBool(SettingNames.AllowSelectedDeviceOverride, value); }
		}

		public virtual bool UseStartResolution
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.UseStartResolution, DefaultEngineSettings.UseStartResolution); }
			set { RcPlugIn.Settings.SetBool(SettingNames.UseStartResolution, value); }
		}

		public virtual int StartResolution
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.StartResolution, DefaultEngineSettings.StartResolution); }
			set { if (value < 1) value = 1;  RcPlugIn.Settings.SetInteger(SettingNames.StartResolution, value); }
		}

		public virtual float DpiScale
		{
			get { return Math.Max(1.0f, (float)RcPlugIn.Settings.GetDouble(SettingNames.DpiScale, DefaultEngineSettings.DpiScale)); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.DpiScale, value); }
		}

		public virtual IntegratorMethod IntegratorMethod { get; set; }
		public virtual uint IntegratorHash
		{
			get
			{
				uint rem = 0xdeadbeef;
				rem = RhinoMath.CRC32(rem, Seed);
				rem = RhinoMath.CRC32(rem, Samples);
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
		public virtual int MaxBounce
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.MaxBounce, DefaultEngineSettings.MaxBounce); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.MaxBounce, value); }
		}
		public virtual bool NoCaustics
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.NoCaustics, DefaultEngineSettings.NoCaustics); }
			set { RcPlugIn.Settings.SetBool(SettingNames.NoCaustics, value); }
		}
		public virtual bool CausticsReflective
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.CausticsReflective, DefaultEngineSettings.CausticsReflective); }
			set { RcPlugIn.Settings.SetBool(SettingNames.CausticsReflective, value); }
		}
		public virtual bool CausticsRefractive
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.CausticsRefractive, DefaultEngineSettings.CausticsRefractive); }
			set { RcPlugIn.Settings.SetBool(SettingNames.CausticsRefractive, value); }
		}
		public virtual int MaxDiffuseBounce
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.MaxDiffuseBounce, DefaultEngineSettings.MaxDiffuseBounce); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.MaxDiffuseBounce, value); }
		}
		public virtual int MaxGlossyBounce
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.MaxGlossyBounce, DefaultEngineSettings.MaxGlossyBounce); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.MaxGlossyBounce, value); }
		}
		public virtual int MaxTransmissionBounce
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.MaxTransmissionBounce, DefaultEngineSettings.MaxTransmissionBounce); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.MaxTransmissionBounce, value); }
		}
		public virtual int MaxVolumeBounce
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.MaxVolumeBounce, DefaultEngineSettings.MaxVolumeBounce); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.MaxVolumeBounce, value); }
		}
		public virtual int AaSamples
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.AaSamples, DefaultEngineSettings.AaSamples); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.AaSamples, value); }
		}
		public virtual int DiffuseSamples
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.DiffuseSamples, DefaultEngineSettings.DiffuseSamples); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.DiffuseSamples, value); }
		}
		public virtual int GlossySamples
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.GlossySamples, DefaultEngineSettings.GlossySamples); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.GlossySamples, value); }
		}
		public virtual int TransmissionSamples
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.TransmissionSamples, DefaultEngineSettings.TransmissionSamples); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.TransmissionSamples, value); }
		}
		public virtual int AoBounces
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.AoBounces, DefaultEngineSettings.AoBounces); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.AoBounces, value); }
		}
		public virtual float AoFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.AoFactor, DefaultEngineSettings.AoFactor); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.AoFactor, value); }
		}
		public virtual float AoDistance
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.AoDistance, DefaultEngineSettings.AoDistance); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.AoDistance, value); }
		}
		public virtual float AoAdditiveFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.AoAdditiveFactor, DefaultEngineSettings.AoAdditiveFactor); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.AoAdditiveFactor, value); }
		}
		public virtual int MeshLightSamples
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.MeshLightSamples, DefaultEngineSettings.MeshLightSamples); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.MeshLightSamples, value); }
		}
		public virtual int SubsurfaceSamples
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.SubSurfaceSamples, DefaultEngineSettings.SubSurfaceSamples); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.SubSurfaceSamples, value); }
		}
		public virtual int VolumeSamples
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.VolumeSamples, DefaultEngineSettings.VolumeSamples); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.VolumeSamples, value); }
		}
		public virtual int Samples
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.Samples, DefaultEngineSettings.Samples); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.Samples, Math.Max(1, value)); }
		}
		public virtual bool UseDocumentSamples
		{
			get => RcPlugIn.Settings.GetBool(SettingNames.UseDocumentSamples, DefaultEngineSettings.UseDocumentSamples);
			set => RcPlugIn.Settings.SetBool(SettingNames.UseDocumentSamples, value);
		}
		public virtual int TextureBakeQuality
		{
			get {
				var quali = RcPlugIn.Settings.GetInteger(SettingNames.TextureBakeQuality, DefaultEngineSettings.TextureBakeQuality);
				return Math.Max(0, Math.Min(3, quali));
			}
			set {
				var quali  = Math.Max(0, Math.Min(3, value));
				RcPlugIn.Settings.SetInteger(SettingNames.TextureBakeQuality, quali);
			}
		}

		public virtual int Seed
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.Seed, DefaultEngineSettings.Seed); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.Seed, value); }
		}

		public virtual SamplingPattern SamplingPattern { get; set; }

		public virtual float FilterGlossy
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.FilterGlossy, DefaultEngineSettings.FilterGlossy); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.FilterGlossy, value); }
		}

		public virtual float SampleClampDirect
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.SampleClampDirect, DefaultEngineSettings.SampleClampDirect); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.SampleClampDirect, value); }
		}

		public virtual float SampleClampIndirect
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.SampleClampIndirect, DefaultEngineSettings.SampleClampIndirect); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.SampleClampIndirect, value); }
		}

		public virtual float LightSamplingThreshold
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.LightSamplingThreshold, DefaultEngineSettings.LightSamplingThreshold); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.LightSamplingThreshold, value); }
		}

		public virtual bool UseDirectLight
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.UseDirectLight, DefaultEngineSettings.UseDirectLight); }
			set { RcPlugIn.Settings.SetBool(SettingNames.UseDirectLight, value); }
		}

		public virtual bool UseIndirectLight
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.UseIndirectLight, DefaultEngineSettings.UseIndirectLight); }
			set { RcPlugIn.Settings.SetBool(SettingNames.UseIndirectLight, value); }
		}

		public virtual int Blades
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.Blades, DefaultEngineSettings.Blades); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.Blades, value); }
		}

		public virtual float BladesRotation
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.BladesRotation, DefaultEngineSettings.BladesRotation); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.BladesRotation, value); }
		}
		public virtual float ApertureRatio
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.ApertureRatio, DefaultEngineSettings.ApertureRatio); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.ApertureRatio, value); }
		}

		public virtual float ApertureFactor
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.ApertureFactor, DefaultEngineSettings.ApertureFactor); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.ApertureFactor, value); }
		}

		public virtual float SensorWidth
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.SensorWidth, DefaultEngineSettings.SensorWidth); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.SensorWidth, value); }
		}

		public virtual float SensorHeight
		{
			get { return (float)RcPlugIn.Settings.GetDouble(SettingNames.SensorHeight, DefaultEngineSettings.SensorHeight); }
			set { RcPlugIn.Settings.SetDouble(SettingNames.SensorHeight, value); }
		}

		public virtual int TransparentMaxBounce
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.TransparentMaxBounce, DefaultEngineSettings.TransparentMaxBounce); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.TransparentMaxBounce, value); }
		}

		public virtual int SssMethod {
			get { return RcPlugIn.Settings.GetInteger(SettingNames.SssMethod, DefaultEngineSettings.SssMethod); }
			set { RcPlugIn.Settings.SetInteger(SettingNames.SssMethod, value); }
		}

		public virtual int OpenClDeviceType
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.OpenCLDeviceType, DefaultEngineSettings.OpenClDeviceType); }
			set
			{
				var old = OpenClDeviceType;
				if (old != value)
				{
					RcPlugIn.Settings.SetInteger(SettingNames.OpenCLDeviceType, value);
				}
			}
		}

		public virtual bool OpenClSingleProgram
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.OpenCLSingleProgram, DefaultEngineSettings.OpenClSingleProgram); }
			set
			{
				var old = OpenClSingleProgram;
				if (old != value)
				{
					RcPlugIn.Settings.SetBool(SettingNames.OpenCLSingleProgram, value);
				}
			}
		}
		public virtual bool NoShadows
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.NoShadows, DefaultEngineSettings.NoShadows); }
			set
			{
				var old = NoShadows;
				if (old != value)
				{
					RcPlugIn.Settings.SetBool(SettingNames.NoShadows, value);
				}
			}
		}
		public virtual int OpenClKernelType
		{
			get { return RcPlugIn.Settings.GetInteger(SettingNames.OpenCLKernelType, DefaultEngineSettings.OpenClKernelType); }
			set
			{
				var old = OpenClKernelType;
				if (old != value)
				{
					RcPlugIn.Settings.SetInteger(SettingNames.OpenCLKernelType, value);
				}
			}
		}
		public virtual bool CPUSplitKernel
		{
			get { return RcPlugIn.Settings.GetBool(SettingNames.CPUSplitKernel, DefaultEngineSettings.CPUSplitKernel); }
			set
			{
				var old = CPUSplitKernel;
				if (old != value)
				{
					RcPlugIn.Settings.SetBool(SettingNames.CPUSplitKernel, value);
				}
			}
		}
	}
}
