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
			get => mDict.GetBool(SettingNames.UseDocumentSamples, DefaultEngineSettings.UseDocumentSamples);
			set => mDict[SettingNames.UseDocumentSamples] = value;
		}
		public int TextureBakeQuality
		{
			get {
				var quali = mDict.GetInteger(SettingNames.TextureBakeQuality, DefaultEngineSettings.TextureBakeQuality);
				return Math.Max(0, Math.Min(4, quali));
			}
			set {
				var quali  = Math.Max(0, Math.Min(4, value));
				mDict[SettingNames.TextureBakeQuality] = quali;
			}
		}
		public int Seed
		{
			get => mDict.GetInteger(SettingNames.Seed, DefaultEngineSettings.Seed);
			set => throw new InvalidOperationException();
		}
		public virtual int DiffuseSamples
		{
			get => mDict.GetInteger(SettingNames.DiffuseSamples, DefaultEngineSettings.DiffuseSamples);
			set => throw new InvalidOperationException();
		}
		public virtual int GlossySamples
		{
			get => mDict.GetInteger(SettingNames.GlossySamples, DefaultEngineSettings.GlossySamples);
			set => throw new InvalidOperationException();
		}
		public virtual int TransmissionSamples
		{
			get => mDict.GetInteger(SettingNames.TransmissionSamples, DefaultEngineSettings.TransmissionSamples);
			set => throw new InvalidOperationException();
		}
		public virtual int MaxBounce
		{
			get => mDict.GetInteger(SettingNames.MaxBounce, DefaultEngineSettings.MaxBounce);
			set => throw new InvalidOperationException();
		}
		public virtual int MaxDiffuseBounce
		{
			get => mDict.GetInteger(SettingNames.MaxDiffuseBounce, DefaultEngineSettings.MaxDiffuseBounce);
			set => throw new InvalidOperationException();
		}
		public virtual int MaxGlossyBounce
		{
			get => mDict.GetInteger(SettingNames.MaxGlossyBounce, DefaultEngineSettings.MaxGlossyBounce);
			set => throw new InvalidOperationException();
		}
		public virtual int MaxVolumeBounce
		{
			get => mDict.GetInteger(SettingNames.MaxVolumeBounce, DefaultEngineSettings.MaxVolumeBounce);
			set => throw new InvalidOperationException();
		}
		public virtual int MaxTransmissionBounce
		{
			get => mDict.GetInteger(SettingNames.MaxTransmissionBounce, DefaultEngineSettings.MaxTransmissionBounce);
			set => throw new InvalidOperationException();
		}
		public virtual int TransparentMaxBounce
		{
			get => mDict.GetInteger(SettingNames.TransparentMaxBounce, DefaultEngineSettings.TransparentMaxBounce);
			set => throw new InvalidOperationException();
		}

		public int TileX
		{
			get => mDict.GetInteger(SettingNames.TileX, DefaultEngineSettings.TileX);
			set => throw new InvalidOperationException();
		}
		public int TileY
		{
			get => mDict.GetInteger(SettingNames.TileY, DefaultEngineSettings.TileY);
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
			get => (float)mDict.GetDouble(SettingNames.SpotLightFactor, DefaultEngineSettings.SpotLightFactor);
			set => throw new InvalidOperationException();
		}
		public float PointLightFactor
		{
			get => (float)mDict.GetDouble(SettingNames.PointLightFactor, DefaultEngineSettings.PointLightFactor);
			set => throw new InvalidOperationException();
		}
		public float SunLightFactor
		{
			get => (float)mDict.GetDouble(SettingNames.SunLightFactor, DefaultEngineSettings.SunLightFactor);
			set => throw new InvalidOperationException();
		}
		public float LinearLightFactor
		{
			get => (float)mDict.GetDouble(SettingNames.LinearLightFactor, DefaultEngineSettings.LinearLightFactor);
			set => throw new InvalidOperationException();
		}
		public float AreaLightFactor
		{
			get => (float)mDict.GetDouble(SettingNames.AreaLightFactor, DefaultEngineSettings.AreaLightFactor);
			set => throw new InvalidOperationException();
		}
		public float PolishFactor
		{
			get => (float)mDict.GetDouble(SettingNames.PolishFactor, DefaultEngineSettings.PolishFactor);
			set => throw new InvalidOperationException();
		}

		public float BumpDistance
		{
			get => (float)mDict.GetDouble(SettingNames.BumpDistance, DefaultEngineSettings.BumpDistance);
			set => throw new InvalidOperationException();
		}
		public float NormalStrengthFactor
		{
			get => (float)mDict.GetDouble(SettingNames.NormalStrengthFactor, DefaultEngineSettings.NormalStrengthFactor);
			set => throw new InvalidOperationException();
		}
		public float BumpStrengthFactor
		{
			get => (float)mDict.GetDouble(SettingNames.BumpStrengthFactor, DefaultEngineSettings.BumpStrengthFactor);
			set => throw new InvalidOperationException();
		}

		public bool NoCaustics
		{
			get => mDict.GetBool(SettingNames.NoCaustics, DefaultEngineSettings.NoCaustics);
			set => throw new InvalidOperationException();
		}
		public bool CausticsReflective
		{
			get => mDict.GetBool(SettingNames.CausticsReflective, DefaultEngineSettings.CausticsReflective);
			set => throw new InvalidOperationException();
		}
		public bool CausticsRefractive
		{
			get => mDict.GetBool(SettingNames.CausticsRefractive, DefaultEngineSettings.CausticsRefractive);
			set => throw new InvalidOperationException();
		}

		public int AaSamples
		{
			get => mDict.GetInteger(SettingNames.AaSamples, DefaultEngineSettings.AaSamples);
			set => throw new InvalidOperationException();
		}

		public int AoBounces
		{
			get => mDict.GetInteger(SettingNames.AoBounces, DefaultEngineSettings.AoBounces);
			set => throw new InvalidOperationException();
		}
		public float AoFactor
		{
			get => mDict.GetFloat(SettingNames.AoFactor, DefaultEngineSettings.AoFactor);
			set => throw new InvalidOperationException();
		}
		public float AoDistance
		{
			get => mDict.GetFloat(SettingNames.AoDistance, DefaultEngineSettings.AoDistance);
			set => throw new InvalidOperationException();
		}
		public float AoAdditiveFactor
		{
			get => mDict.GetFloat(SettingNames.AoAdditiveFactor, DefaultEngineSettings.AoAdditiveFactor);
			set => throw new InvalidOperationException();
		}

		public int MeshLightSamples
		{
			get => mDict.GetInteger(SettingNames.MeshLightSamples, DefaultEngineSettings.MeshLightSamples);
			set => throw new InvalidOperationException();
		}
		public int SubsurfaceSamples
		{
			get => mDict.GetInteger(SettingNames.SubSurfaceSamples, DefaultEngineSettings.SubSurfaceSamples);
			set => throw new InvalidOperationException();
		}
		public int VolumeSamples
		{
			get => mDict.GetInteger(SettingNames.VolumeSamples, DefaultEngineSettings.VolumeSamples);
			set => throw new InvalidOperationException();
		}

		public float FilterGlossy
		{
			get => (float)mDict.GetDouble(SettingNames.FilterGlossy, DefaultEngineSettings.FilterGlossy);
			set => throw new InvalidOperationException();
		}

		public float SampleClampDirect
		{
			get => (float)mDict.GetDouble(SettingNames.SampleClampDirect, DefaultEngineSettings.SampleClampDirect);
			set => throw new InvalidOperationException();
		}
		public float SampleClampIndirect
		{
			get => (float)mDict.GetDouble(SettingNames.SampleClampIndirect, DefaultEngineSettings.SampleClampIndirect);
			set => throw new InvalidOperationException();
		}
		public float LightSamplingThreshold
		{
			get => (float)mDict.GetDouble(SettingNames.LightSamplingThreshold, DefaultEngineSettings.LightSamplingThreshold);
			set => throw new InvalidOperationException();
		}

		public bool UseDirectLight
		{
			get => mDict.GetBool(SettingNames.UseDirectLight, DefaultEngineSettings.UseDirectLight);
			set => throw new InvalidOperationException();
		}
		public bool UseIndirectLight
		{
			get => mDict.GetBool(SettingNames.UseIndirectLight, DefaultEngineSettings.UseIndirectLight);
			set => throw new InvalidOperationException();
		}

		public int Blades
		{
			get => mDict.GetInteger(SettingNames.Blades, DefaultEngineSettings.Blades);
			set => throw new InvalidOperationException();
		}
		public float BladesRotation
		{
			get => (float)mDict.GetDouble(SettingNames.BladesRotation, DefaultEngineSettings.BladesRotation);
			set => throw new InvalidOperationException();
		}
		public float ApertureRatio
		{
			get => (float)mDict.GetDouble(SettingNames.ApertureRatio, DefaultEngineSettings.ApertureRatio);
			set => throw new InvalidOperationException();
		}
		public float ApertureFactor
		{
			get => (float)mDict.GetDouble(SettingNames.ApertureFactor, DefaultEngineSettings.ApertureFactor);
			set => throw new InvalidOperationException();
		}

		public float SensorWidth
		{
			get => (float)mDict.GetDouble(SettingNames.SensorWidth, DefaultEngineSettings.SensorWidth);
			set => throw new InvalidOperationException();
		}
		public float SensorHeight
		{
			get => (float)mDict.GetDouble(SettingNames.SensorHeight, DefaultEngineSettings.SensorHeight);
			set => throw new InvalidOperationException();
		}

		public int SssMethod
		{
			get => mDict.GetInteger(SettingNames.SssMethod, DefaultEngineSettings.SssMethod);
			set => throw new InvalidOperationException();
		}
		public bool AllowSelectedDeviceOverride { get => RcCore.It.AllSettings.AllowSelectedDeviceOverride; }
		public Device RenderDevice { get => Device.DeviceFromString(Device.ValidDeviceString(SelectedDeviceStr)); }
		public virtual bool ShowMaxPasses
		{
			get { return mDict.GetBool(SettingNames.MaxPasses, DefaultEngineSettings.ShowMaxPasses); }
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
			get => mDict.GetString(SettingNames.IntermediateSelectedDeviceStr, DefaultEngineSettings.SelectedDeviceStr);
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
			get => mDict.GetInteger(SettingNames.OpenClDeviceType, DefaultEngineSettings.OpenClDeviceType);
			set => throw new InvalidOperationException();
		}
		public bool OpenClSingleProgram
		{
			get => mDict.GetBool(SettingNames.OpenClSingleProgram, DefaultEngineSettings.OpenClSingleProgram);
			set => throw new InvalidOperationException();
		}
		public int OpenClKernelType
		{
			get => mDict.GetInteger(SettingNames.OpenClKernelType, DefaultEngineSettings.OpenClKernelType);
			set => throw new InvalidOperationException();
		}

		public bool CPUSplitKernel
		{
			get => mDict.GetBool(SettingNames.CPUSplitKernel, DefaultEngineSettings.CPUSplitKernel);
			set => throw new InvalidOperationException();
		}

		public bool NoShadows
		{
			get => mDict.GetBool(SettingNames.NoShadows, DefaultEngineSettings.NoShadows);
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
			get => mDict.GetInteger(SettingNames.PreviewSamples, DefaultEngineSettings.PreviewSamples);
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

		public bool UseAdaptiveSampling
		{
			get => RcCore.It.AllSettings.UseAdaptiveSampling;
			set => throw new InvalidOperationException();
		}

		public bool UseLightTree
		{
			get => RcCore.It.AllSettings.UseLightTree;
			set => throw new InvalidOperationException();
		}

		public int AdaptiveMinSamples
		{
			get => RcCore.It.AllSettings.AdaptiveMinSamples;
			set => throw new InvalidOperationException();
		}

		public float AdaptiveThreshold
		{
			get => RcCore.It.AllSettings.AdaptiveThreshold;
			set => throw new InvalidOperationException();
		}

#endregion
	}
}
