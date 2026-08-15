/**
Copyright 2014-2024 Robert McNeel and Associates

Licensed under the Apache License, Version 2.0 (the SettingNames.License);
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an SettingNames.AS IS BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
**/

using ccl;
using Rhino;
using Rhino.Collections;
using RhinoCyclesCore.Core;
using System;

namespace RhinoCyclesCore.Settings
{
	public class EngineDocumentSettings : IAllSettings
	{
		internal ArchivableDictionary mDict;
		internal EngineDocumentSettings(EngineDocumentSettings eds) { mDict = eds.mDict; }
		public EngineDocumentSettings(uint docSerialNumber) {
			mDict = (RhinoDoc.FromRuntimeSerialNumber(docSerialNumber))?.RenderSettings?.UserDictionary;
		}
		internal EngineDocumentSettings(ArchivableDictionary dictionary)
		{
			mDict = dictionary;
		}
#region Document settings
		public IntegratorMethod IntegratorMethod
		{
			get => RcCore.It.AllSettings.IntegratorMethod;
			set => throw new InvalidOperationException();
		}
		public uint IntegratorHash
		{
			get
			{
				uint rem = 0xdeadbeef;
				rem = RhinoMath.CRC32(rem, Seed);
				rem = RhinoMath.CRC32(rem, Samples);
				rem = RhinoMath.CRC32(rem, UseDocumentSamples ? 1 : 0);
				rem = RhinoMath.CRC32(rem, DiffuseSamples);
				rem = RhinoMath.CRC32(rem, GlossySamples);
				rem = RhinoMath.CRC32(rem, TransmissionSamples);
				rem = RhinoMath.CRC32(rem, MaxBounce);
				rem = RhinoMath.CRC32(rem, MaxDiffuseBounce);
				rem = RhinoMath.CRC32(rem, MaxGlossyBounce);
				rem = RhinoMath.CRC32(rem, MaxVolumeBounce);
				rem = RhinoMath.CRC32(rem, MaxTransmissionBounce);
				rem = RhinoMath.CRC32(rem, TransparentMaxBounce);
				rem = RhinoMath.CRC32(rem, UseAdaptiveSampling ? 1 : 0);
				rem = RhinoMath.CRC32(rem, AdaptiveMinSamples);
				rem = RhinoMath.CRC32(rem, AdaptiveThreshold);
				rem = RhinoMath.CRC32(rem, FilterGlossy);
				rem = RhinoMath.CRC32(rem, SampleClampIndirect);
				rem = RhinoMath.CRC32(rem, IsProductPreset ? 1 : 0);

				RcCore.It.AddLogStringIfVerbose($"\t\t-- EngineDocumentSettings.IntegratorHash: {rem}. UseAdaptiveSampling {UseAdaptiveSampling}. Seed {Seed}");

				return rem;
			}
		}
		public virtual int Samples
		{
			get => Math.Max(1, mDict.GetInteger(SettingNames.Samples, RcCore.It.AllSettings.Samples));
			set => mDict[SettingNames.Samples] = Math.Max(1, value);
		}
		public virtual bool UseDocumentSamples
		{
			get => mDict.GetBool(SettingNames.UseDocumentSamples, RcCore.It.AllSettings.UseDocumentSamples);
			set => mDict[SettingNames.UseDocumentSamples] = value;
		}
		public int TextureBakeQuality
		{
			get {
				var quali = mDict.GetInteger(SettingNames.TextureBakeQuality, RcCore.It.AllSettings.TextureBakeQuality);
				return Math.Max(0, Math.Min(4, quali));
			}
			set {
				var quali  = Math.Max(0, Math.Min(4, value));
				mDict[SettingNames.TextureBakeQuality] = quali;
			}
		}
		public int Seed
		{
			get => mDict.GetInteger(SettingNames.Seed, RcCore.It.AllSettings.Seed);
			set => throw new InvalidOperationException();
		}
		public virtual int DiffuseSamples
		{
			get => mDict.GetInteger(SettingNames.DiffuseSamples, RcCore.It.AllSettings.DiffuseSamples);
			set => throw new InvalidOperationException();
		}
		public virtual int GlossySamples
		{
			get => mDict.GetInteger(SettingNames.GlossySamples, RcCore.It.AllSettings.GlossySamples);
			set => throw new InvalidOperationException();
		}
		public virtual int TransmissionSamples
		{
			get => mDict.GetInteger(SettingNames.TransmissionSamples, RcCore.It.AllSettings.TransmissionSamples);
			set => throw new InvalidOperationException();
		}
		public virtual int MaxBounce
		{
			get => mDict.GetInteger(SettingNames.MaxBounce, RcCore.It.AllSettings.MaxBounce);
			set => throw new InvalidOperationException();
		}
		public virtual int MaxDiffuseBounce
		{
			get => mDict.GetInteger(SettingNames.MaxDiffuseBounce, RcCore.It.AllSettings.MaxDiffuseBounce);
			set => throw new InvalidOperationException();
		}
		public virtual int MaxGlossyBounce
		{
			get => mDict.GetInteger(SettingNames.MaxGlossyBounce, RcCore.It.AllSettings.MaxGlossyBounce);
			set => throw new InvalidOperationException();
		}
		public virtual int MaxVolumeBounce
		{
			get => mDict.GetInteger(SettingNames.MaxVolumeBounce, RcCore.It.AllSettings.MaxVolumeBounce);
			set => throw new InvalidOperationException();
		}
		public virtual int MaxTransmissionBounce
		{
			get => mDict.GetInteger(SettingNames.MaxTransmissionBounce, RcCore.It.AllSettings.MaxTransmissionBounce);
			set => throw new InvalidOperationException();
		}
		public virtual int TransparentMaxBounce
		{
			get => mDict.GetInteger(SettingNames.TransparentMaxBounce, RcCore.It.AllSettings.TransparentMaxBounce);
			set => throw new InvalidOperationException();
		}

		public int TileX
		{
			get => mDict.GetInteger(SettingNames.TileX, RcCore.It.AllSettings.TileX);
			set => throw new InvalidOperationException();
		}
		public int TileY
		{
			get => mDict.GetInteger(SettingNames.TileY, RcCore.It.AllSettings.TileY);
			set => throw new InvalidOperationException();
		}
		public bool UseStartResolution
		{
			get => RcCore.It.AllSettings.UseStartResolution;
			set => throw new InvalidOperationException();
		}
		public int StartResolution
		{
			get => RcCore.It.AllSettings.StartResolution;
			set => throw new InvalidOperationException();
		}

		public float SpotLightFactor
		{
			get => (float)mDict.GetDouble(SettingNames.SpotLightFactor, RcCore.It.AllSettings.SpotLightFactor);
			set => throw new InvalidOperationException();
		}
		public float PointLightFactor
		{
			get => (float)mDict.GetDouble(SettingNames.PointLightFactor, RcCore.It.AllSettings.PointLightFactor);
			set => throw new InvalidOperationException();
		}
		public float SunLightFactor
		{
			get => (float)mDict.GetDouble(SettingNames.SunLightFactor, RcCore.It.AllSettings.SunLightFactor);
			set => throw new InvalidOperationException();
		}
		public float LinearLightFactor
		{
			get => (float)mDict.GetDouble(SettingNames.LinearLightFactor, RcCore.It.AllSettings.LinearLightFactor);
			set => throw new InvalidOperationException();
		}
		public float AreaLightFactor
		{
			get => (float)mDict.GetDouble(SettingNames.AreaLightFactor, RcCore.It.AllSettings.AreaLightFactor);
			set => throw new InvalidOperationException();
		}
		public float PolishFactor
		{
			get => (float)mDict.GetDouble(SettingNames.PolishFactor, RcCore.It.AllSettings.PolishFactor);
			set => throw new InvalidOperationException();
		}

		public float BumpDistance
		{
			get => (float)mDict.GetDouble(SettingNames.BumpDistance, RcCore.It.AllSettings.BumpDistance);
			set => throw new InvalidOperationException();
		}
		public float NormalStrengthFactor
		{
			get => (float)mDict.GetDouble(SettingNames.NormalStrengthFactor, RcCore.It.AllSettings.NormalStrengthFactor);
			set => throw new InvalidOperationException();
		}
		public float BumpStrengthFactor
		{
			get => (float)mDict.GetDouble(SettingNames.BumpStrengthFactor, RcCore.It.AllSettings.BumpStrengthFactor);
			set => throw new InvalidOperationException();
		}

		public bool NoCaustics
		{
			get => mDict.GetBool(SettingNames.NoCaustics, RcCore.It.AllSettings.NoCaustics);
			set => throw new InvalidOperationException();
		}
		public bool CausticsReflective
		{
			get => mDict.GetBool(SettingNames.CausticsReflective, RcCore.It.AllSettings.CausticsReflective);
			set => throw new InvalidOperationException();
		}
		public bool CausticsRefractive
		{
			get => mDict.GetBool(SettingNames.CausticsRefractive, RcCore.It.AllSettings.CausticsRefractive);
			set => throw new InvalidOperationException();
		}

		public int AaSamples
		{
			get => mDict.GetInteger(SettingNames.AaSamples, RcCore.It.AllSettings.AaSamples);
			set => throw new InvalidOperationException();
		}

		public int AoBounces
		{
			get => mDict.GetInteger(SettingNames.AoBounces, RcCore.It.AllSettings.AoBounces);
			set => throw new InvalidOperationException();
		}
		public float AoFactor
		{
			get => mDict.GetFloat(SettingNames.AoFactor, RcCore.It.AllSettings.AoFactor);
			set => throw new InvalidOperationException();
		}
		public float AoDistance
		{
			get => mDict.GetFloat(SettingNames.AoDistance, RcCore.It.AllSettings.AoDistance);
			set => throw new InvalidOperationException();
		}
		public float AoAdditiveFactor
		{
			get => mDict.GetFloat(SettingNames.AoAdditiveFactor, RcCore.It.AllSettings.AoAdditiveFactor);
			set => throw new InvalidOperationException();
		}

		public int MeshLightSamples
		{
			get => mDict.GetInteger(SettingNames.MeshLightSamples, RcCore.It.AllSettings.MeshLightSamples);
			set => throw new InvalidOperationException();
		}
		public int SubsurfaceSamples
		{
			get => mDict.GetInteger(SettingNames.SubSurfaceSamples, RcCore.It.AllSettings.SubsurfaceSamples);
			set => throw new InvalidOperationException();
		}
		public int VolumeSamples
		{
			get => mDict.GetInteger(SettingNames.VolumeSamples, RcCore.It.AllSettings.VolumeSamples);
			set => throw new InvalidOperationException();
		}

		public float FilterGlossy
		{
			get => (float)mDict.GetDouble(SettingNames.FilterGlossy, RcCore.It.AllSettings.FilterGlossy);
			set => throw new InvalidOperationException();
		}

		public RenderPresetHelpers.Presets RenderPreset
		{
			get
			{
				if (mDict.ContainsKey(SettingNames.RenderPreset))
					return (RenderPresetHelpers.Presets)mDict.GetInteger(SettingNames.RenderPreset, (int)RenderPresetHelpers.DefaultPreset);

				return RenderPresetHelpers.PresetFromValues(new EngineDocumentSettings(mDict));
			}
			set => throw new InvalidOperationException();
		}

		public bool IsProductPreset => (RenderPresetHelpers.ProductPreset(this) == RenderPresetHelpers.Presets.Product);

		public float SampleClampDirect
		{
			get => (float)mDict.GetDouble(SettingNames.SampleClampDirect, RcCore.It.AllSettings.SampleClampDirect);
			set => throw new InvalidOperationException();
		}
		public float SampleClampIndirect
		{
			get => (float)mDict.GetDouble(SettingNames.SampleClampIndirect, RcCore.It.AllSettings.SampleClampIndirect);
			set => throw new InvalidOperationException();
		}
		public float LightSamplingThreshold
		{
			get => (float)mDict.GetDouble(SettingNames.LightSamplingThreshold, RcCore.It.AllSettings.LightSamplingThreshold);
			set => throw new InvalidOperationException();
		}

		public bool UseDirectLight
		{
			get => mDict.GetBool(SettingNames.UseDirectLight, RcCore.It.AllSettings.UseDirectLight);
			set => throw new InvalidOperationException();
		}
		public bool UseIndirectLight
		{
			get => mDict.GetBool(SettingNames.UseIndirectLight, RcCore.It.AllSettings.UseIndirectLight);
			set => throw new InvalidOperationException();
		}

		public int Blades
		{
			get => mDict.GetInteger(SettingNames.Blades, RcCore.It.AllSettings.Blades);
			set => throw new InvalidOperationException();
		}
		public float BladesRotation
		{
			get => (float)mDict.GetDouble(SettingNames.BladesRotation, RcCore.It.AllSettings.BladesRotation);
			set => throw new InvalidOperationException();
		}
		public float ApertureRatio
		{
			get => (float)mDict.GetDouble(SettingNames.ApertureRatio, RcCore.It.AllSettings.ApertureRatio);
			set => throw new InvalidOperationException();
		}
		public float ApertureFactor
		{
			get => (float)mDict.GetDouble(SettingNames.ApertureFactor, RcCore.It.AllSettings.ApertureFactor);
			set => throw new InvalidOperationException();
		}

		public float SensorWidth
		{
			get => (float)mDict.GetDouble(SettingNames.SensorWidth, RcCore.It.AllSettings.SensorWidth);
			set => throw new InvalidOperationException();
		}
		public float SensorHeight
		{
			get => (float)mDict.GetDouble(SettingNames.SensorHeight, RcCore.It.AllSettings.SensorHeight);
			set => throw new InvalidOperationException();
		}

		public int SssMethod
		{
			get => mDict.GetInteger(SettingNames.SssMethod, RcCore.It.AllSettings.SssMethod);
			set => throw new InvalidOperationException();
		}
		public bool AllowSelectedDeviceOverride { get => RcCore.It.AllSettings.AllowSelectedDeviceOverride; }
		public Device RenderDevice { get => Device.DeviceFromString(Device.ValidDeviceString(SelectedDeviceStr)); }
		public virtual bool ShowMaxPasses
		{
			get { return mDict.GetBool(SettingNames.MaxPasses, RcCore.It.AllSettings.ShowMaxPasses); }
			set { mDict[SettingNames.MaxPasses] = value; }
		}
#endregion

#region Application/Global settings
		public string SelectedDeviceStr
		{
			get => RcCore.It.AllSettings.SelectedDeviceStr;
			set => RcCore.It.AllSettings.SelectedDeviceStr = value;
		}
		public string IntermediateSelectedDeviceStr
		{
			get => mDict.GetString(SettingNames.IntermediateSelectedDeviceStr, RcCore.It.AllSettings.SelectedDeviceStr);
			set => mDict[SettingNames.IntermediateSelectedDeviceStr] = value;
		}
		public int ThrottleMs
		{
			get => RcCore.It.AllSettings.ThrottleMs;
			set => throw new InvalidOperationException();
		}
		public virtual int Threads
		{
			get => mDict.GetInteger(SettingNames.Threads, RcCore.It.AllSettings.Threads);
			set => throw new InvalidOperationException();
		}
		public virtual bool ExperimentalCpuInMulti
		{
			get => mDict.GetBool(SettingNames.ExperimentalCpuInMulti, RcCore.It.AllSettings.ExperimentalCpuInMulti);
			set => throw new InvalidOperationException();
		}


		public int OpenClDeviceType
		{
			get => mDict.GetInteger(SettingNames.OpenClDeviceType, RcCore.It.AllSettings.OpenClDeviceType);
			set => throw new InvalidOperationException();
		}
		public bool OpenClSingleProgram
		{
			get => mDict.GetBool(SettingNames.OpenClSingleProgram, RcCore.It.AllSettings.OpenClSingleProgram);
			set => throw new InvalidOperationException();
		}
		public int OpenClKernelType
		{
			get => mDict.GetInteger(SettingNames.OpenClKernelType, RcCore.It.AllSettings.OpenClKernelType);
			set => throw new InvalidOperationException();
		}

		public bool CPUSplitKernel
		{
			get => mDict.GetBool(SettingNames.CPUSplitKernel, RcCore.It.AllSettings.CPUSplitKernel);
			set => throw new InvalidOperationException();
		}

		public bool NoShadows
		{
			get => mDict.GetBool(SettingNames.NoShadows, RcCore.It.AllSettings.NoShadows);
			set => throw new InvalidOperationException();
		}

		public int PixelSize
		{
			get => Math.Max(1, RcCore.It.AllSettings.PixelSize);
			set => throw new InvalidOperationException();
		}

		public float OldDpiScale
		{
			get => Math.Max(1.0f, RcCore.It.AllSettings.OldDpiScale);
			set => throw new InvalidOperationException();
		}

		public int PreviewSamples
		{
			get => mDict.GetInteger(SettingNames.PreviewSamples, RcCore.It.AllSettings.PreviewSamples);
			set => throw new InvalidOperationException();
		}

		public bool DumpMaterialShaderGraph
		{
			get => RcCore.It.AllSettings.DumpMaterialShaderGraph;
			set => throw new InvalidOperationException();
		}

		public bool DumpEnvironmentShaderGraph
		{
			get => RcCore.It.AllSettings.DumpEnvironmentShaderGraph;
			set => throw new InvalidOperationException();
		}
		public bool StartGpuKernelCompiler
		{
			get => RcCore.It.AllSettings.StartGpuKernelCompiler;
			set => throw new InvalidOperationException();
		}

		public bool VerboseLogging
		{
			get => RcCore.It.AllSettings.VerboseLogging;
			set => throw new InvalidOperationException();
		}

		public int RetentionDays
		{
			get => RcCore.It.AllSettings.RetentionDays;
			set => throw new InvalidOperationException();
		}

		public int TriggerPostEffectsSample
		{
			get => RcCore.It.AllSettings.TriggerPostEffectsSample;
			set => throw new InvalidOperationException();
		}

		public bool UseAdaptiveSampling
		{
			get => mDict.GetBool(SettingNames.UseAdaptiveSampling, RcCore.It.AllSettings.UseAdaptiveSampling);
			set => throw new InvalidOperationException();
		}

		public bool UseLightTree
		{
			get => RcCore.It.AllSettings.UseLightTree;
			set => throw new InvalidOperationException();
		}

		public int AdaptiveMinSamples
		{
			get => mDict.GetInteger(SettingNames.AdaptiveMinSamples, RcCore.It.AllSettings.AdaptiveMinSamples);
			set => throw new InvalidOperationException();
		}

		public float AdaptiveThreshold
		{
			get => (float)mDict.GetDouble(SettingNames.AdaptiveThreshold, RcCore.It.AllSettings.AdaptiveThreshold);
			set => throw new InvalidOperationException();
		}

		public float JiggleFactor
		{
			get => RcCore.It.AllSettings.JiggleFactor;
			set => throw new InvalidOperationException();
		}

		public float GpJiggleDistance
		{
			get => RcCore.It.AllSettings.GpJiggleDistance;
			set => throw new InvalidOperationException();
		}

		public bool SkipPreview
		{
			get => RcCore.It.AllSettings.SkipPreview;
			set => throw new InvalidOperationException();
		}

#endregion
	}
}
