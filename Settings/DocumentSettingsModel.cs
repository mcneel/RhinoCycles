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
using System.Runtime.InteropServices;
using RhinoCyclesCore.Core;
using Rhino;
using Rhino.UI.Controls;
using System.ComponentModel;
using Rhino.Collections;
using System;
using ccl;

namespace RhinoCyclesCore.Settings
{
	/// <summary>
	/// View model of document settings. Used for in the integrator section.


	/// Because this model is used also in application settings all settings interface is implemented.
	///
	/// </summary>
	[Guid("cae9d284-03b0-4d1c-aa46-b431bc9a7ea2")]
	public class DocumentSettingsModel : CollapsibleSectionViewModel, INotifyPropertyChanged, IAllSettings
	{
		public DocumentSettingsModel(ICollapsibleSection section) : base(section)
		{
		}

#pragma warning disable CS0067
		public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067
		private ArchivableDictionary RenderSettingsForRead()
		{
			var rs = GetData(Rhino.UI.Controls.DataSource.ProviderIds.RhinoSettings, false, true) as Rhino.Render.DataSources.RhinoSettings;
			var renderSettings = rs.GetRenderSettings();
			var dictionary = renderSettings.UserDictionary;
			return dictionary;
		}

		private Rhino.Render.DataSources.RhinoSettings RenderSettingsForWrite()
		{
			return GetData(Rhino.UI.Controls.DataSource.ProviderIds.RhinoSettings, true, true) as Rhino.Render.DataSources.RhinoSettings;
		}

		private void CommitRenderSettings(string propName)
		{
			Commit(Rhino.UI.Controls.DataSource.ProviderIds.RhinoSettings);
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
		}

		private void GetInt(string valueName, int defaultValue, out int value)
		{
			var dictionary = RenderSettingsForRead();
			value = dictionary.GetInteger(valueName, defaultValue);
		}

		private void SetInt(string valueName, int value)
		{
			var rs = RenderSettingsForWrite();
			var renderSettings = rs.GetRenderSettings();
			var dictionary = renderSettings.UserDictionary;

			dictionary[valueName] = value;

			rs.SetRenderSettings(renderSettings);
			CommitRenderSettings(valueName);
		}
		private void GetBool(string valueName, bool defaultValue, out bool value)
		{
			var dictionary = RenderSettingsForRead();
			value = dictionary.GetBool(valueName, defaultValue);
		}

		private void SetBool(string valueName, bool value)
		{
			var rs = RenderSettingsForWrite();
			var renderSettings = rs.GetRenderSettings();
			var dictionary = renderSettings.UserDictionary;

			dictionary[valueName] = value;

			rs.SetRenderSettings(renderSettings);
			CommitRenderSettings(valueName);
		}

		private void GetFloat(string valueName, float defaultValue, out float value)
		{
			var dictionary = RenderSettingsForRead();

			value = (float)dictionary.GetDouble(valueName, defaultValue);
		}

		private void SetFloat(string valueName, float value)
		{
			var rs = RenderSettingsForWrite();
			var renderSettings = rs.GetRenderSettings();
			var dictionary = renderSettings.UserDictionary;

			dictionary[valueName] = value;

			rs.SetRenderSettings(renderSettings);
			CommitRenderSettings(valueName);
		}

		/*private void GetString(string valueName, string defaultValue, out string value)
		{
			var dictionary = RenderSettingsForRead();

			value = dictionary.GetString(valueName, defaultValue);
		}

		private void SetString(string valueName, string value)
		{
			var rs = RenderSettingsForWrite();
			var renderSettings = rs.GetRenderSettings();
			var dictionary = renderSettings.UserDictionary;

			dictionary[valueName] = value;

			rs.SetRenderSettings(renderSettings);
			CommitRenderSettings(valueName);
		}*/

		#region Document settings
		public IntegratorMethod IntegratorMethod { get => RcCore.It.AllSettings.IntegratorMethod; set => RcCore.It.AllSettings.IntegratorMethod = value; }
		public uint IntegratorHash
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

		public int Samples
		{
			get
			{
				GetInt(SettingNames.Samples, DefaultEngineSettings.Samples, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.Samples, value);
		}
		public virtual bool UseDocumentSamples
		{
			get
			{
				GetBool(SettingNames.UseDocumentSamples, DefaultEngineSettings.UseDocumentSamples, out bool outVal);
				return outVal;
			}
			set => SetBool(SettingNames.UseDocumentSamples, value);
		}
		public virtual int TextureBakeQuality
		{
			get {
				GetInt(SettingNames.TextureBakeQuality, DefaultEngineSettings.TextureBakeQuality, out int quali);
				return Math.Max(0, Math.Min(4, quali));
			}
			set {
				var quali  = Math.Max(0, Math.Min(4, value));
				SetInt(SettingNames.TextureBakeQuality, quali);
			}
		}
		public int Seed
		{
			get
			{
				GetInt(SettingNames.Seed, DefaultEngineSettings.Seed, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.Seed, value);
		}
		public int DiffuseSamples
		{
			get
			{
				GetInt(SettingNames.DiffuseSamples, DefaultEngineSettings.DiffuseSamples, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.DiffuseSamples, value);
		}
		public int GlossySamples
		{
			get
			{
				GetInt(SettingNames.GlossySamples, DefaultEngineSettings.GlossySamples, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.GlossySamples, value);
		}
		public int TransmissionSamples
		{
			get
			{
				GetInt(SettingNames.TransmissionSamples, DefaultEngineSettings.TransmissionSamples, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.TransmissionSamples, value);
		}
		public int MaxBounce
		{
			get
			{
				GetInt(SettingNames.MaxBounce, DefaultEngineSettings.MaxBounce, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.MaxBounce, value);
		}
		public int MaxDiffuseBounce
		{
			get
			{
				GetInt(SettingNames.MaxDiffuseBounce, DefaultEngineSettings.MaxDiffuseBounce, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.MaxDiffuseBounce, value);
		}
		public int MaxGlossyBounce
		{
			get
			{
				GetInt(SettingNames.MaxGlossyBounce, DefaultEngineSettings.MaxGlossyBounce, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.MaxGlossyBounce, value);
		}
		public int MaxVolumeBounce
		{
			get
			{
				GetInt(SettingNames.MaxVolumeBounce, DefaultEngineSettings.MaxVolumeBounce, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.MaxVolumeBounce, value);
		}
		public int MaxTransmissionBounce
		{
			get
			{
				GetInt(SettingNames.MaxTransmissionBounce, DefaultEngineSettings.MaxTransmissionBounce, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.MaxTransmissionBounce, value);
		}
		public int TransparentMaxBounce
		{
			get
			{
				GetInt(SettingNames.TransparentMaxBounce, DefaultEngineSettings.TransparentMaxBounce, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.TransparentMaxBounce, value);
		}

		public int TileX
		{
			get
			{
				GetInt(SettingNames.TileX, DefaultEngineSettings.TileX, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.TileX, value);
		}
		public int TileY
		{
			get
			{
				GetInt(SettingNames.TileY, DefaultEngineSettings.TileY, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.TileY, value);
		}
		public bool UseStartResolution
		{
			get
			{
				GetBool(SettingNames.UseStartResolution, DefaultEngineSettings.UseStartResolution, out bool outVal);
				return outVal;
			}
			set => SetBool(SettingNames.UseStartResolution, value);
		}
		public int StartResolution
		{
			get
			{
				GetInt(SettingNames.StartResolution, DefaultEngineSettings.StartResolution, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.StartResolution, value);
		}

		public float SpotLightFactor
		{
			get
			{
				GetFloat(SettingNames.SpotLightFactor, DefaultEngineSettings.SpotLightFactor, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.SpotLightFactor, value);
		}
		public float PointLightFactor
		{
			get
			{
				GetFloat(SettingNames.PointLightFactor, DefaultEngineSettings.PointLightFactor, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.PointLightFactor, value);
		}
		public float SunLightFactor
		{
			get
			{
				GetFloat(SettingNames.SunLightFactor, DefaultEngineSettings.SunLightFactor, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.SunLightFactor, value);
		}
		public float LinearLightFactor
		{
			get
			{
				GetFloat(SettingNames.LinearLightFactor, DefaultEngineSettings.LinearLightFactor, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.LinearLightFactor, value);
		}
		public float AreaLightFactor
		{
			get
			{
				GetFloat(SettingNames.AreaLightFactor, DefaultEngineSettings.AreaLightFactor, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.AreaLightFactor, value);
		}
		public float PolishFactor
		{
			get
			{
				GetFloat(SettingNames.PolishFactor, DefaultEngineSettings.PolishFactor, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.PolishFactor, value);
		}

		public float BumpDistance
		{
			get
			{
				GetFloat(SettingNames.BumpDistance, DefaultEngineSettings.BumpDistance, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.BumpDistance, value);
		}
		public float NormalStrengthFactor
		{
			get
			{
				GetFloat(SettingNames.NormalStrengthFactor, DefaultEngineSettings.NormalStrengthFactor, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.NormalStrengthFactor, value);
		}
		public float BumpStrengthFactor
		{
			get
			{
				GetFloat(SettingNames.BumpStrengthFactor, DefaultEngineSettings.BumpStrengthFactor, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.BumpStrengthFactor, value);
		}

		public bool NoCaustics
		{
			get
			{
				GetBool(SettingNames.NoCaustics, DefaultEngineSettings.NoCaustics, out bool outVal);
				return outVal;
			}
			set => SetBool(SettingNames.NoCaustics, value);
		}

		public int AaSamples
		{
			get
			{
				GetInt(SettingNames.AaSamples, DefaultEngineSettings.AaSamples, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.AaSamples, value);
		}

		public int AoSamples
		{
			get
			{
				GetInt(SettingNames.AoSamples, DefaultEngineSettings.AoSamples, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.AoSamples, value);
		}

		public int MeshLightSamples
		{
			get
			{
				GetInt(SettingNames.MeshLightSamples, DefaultEngineSettings.MeshLightSamples, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.MeshLightSamples, value);
		}
		public int SubsurfaceSamples
		{
			get
			{
				GetInt(SettingNames.SubSurfaceSamples, DefaultEngineSettings.SubSurfaceSamples, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.SubSurfaceSamples, value);
		}
		public int VolumeSamples
		{
			get
			{
				GetInt(SettingNames.VolumeSamples, DefaultEngineSettings.VolumeSamples, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.VolumeSamples, value);
		}

		public float FilterGlossy
		{
			get
			{
				GetFloat(SettingNames.FilterGlossy, DefaultEngineSettings.FilterGlossy, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.FilterGlossy, value);
		}

		public float SampleClampDirect
		{
			get
			{
				GetFloat(SettingNames.SampleClampDirect, DefaultEngineSettings.SampleClampDirect, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.SampleClampDirect, value);
		}
		public float SampleClampIndirect
		{
			get
			{
				GetFloat(SettingNames.SampleClampIndirect, DefaultEngineSettings.SampleClampIndirect, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.SampleClampIndirect, value);
		}
		public float LightSamplingThreshold
		{
			get
			{
				GetFloat(SettingNames.LightSamplingThreshold, DefaultEngineSettings.LightSamplingThreshold, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.LightSamplingThreshold, value);
		}

		public bool SampleAllLights
		{
			get
			{
				GetBool(SettingNames.SampleAllLights, DefaultEngineSettings.SampleAllLights, out bool outVal);
				return outVal;
			}
			set => SetBool(SettingNames.SampleAllLights, value);
		}
		public bool SampleAllLightsIndirect
		{
			get
			{
				GetBool(SettingNames.SampleAllLightsIndirect, DefaultEngineSettings.SampleAllLightsIndirect, out bool outVal);
				return outVal;
			}
			set => SetBool(SettingNames.SampleAllLightsIndirect, value);
		}

		public int Blades
		{
			get
			{
				GetInt(SettingNames.Blades, DefaultEngineSettings.Blades, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.Blades, value);
		}
		public float BladesRotation
		{
			get
			{
				GetFloat(SettingNames.BladesRotation, DefaultEngineSettings.BladesRotation, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.BladesRotation, value);
		}
		public float ApertureRatio
		{
			get
			{
				GetFloat(SettingNames.ApertureRatio, DefaultEngineSettings.ApertureRatio, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.ApertureRatio, value);
		}
		public float ApertureFactor
		{
			get
			{
				GetFloat(SettingNames.ApertureFactor, DefaultEngineSettings.ApertureFactor, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.ApertureFactor, value);
		}

		public float SensorWidth
		{
			get
			{
				GetFloat(SettingNames.SensorWidth, DefaultEngineSettings.SensorWidth, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.SensorWidth, value);
		}
		public float SensorHeight
		{
			get
			{
				GetFloat(SettingNames.SensorHeight, DefaultEngineSettings.SensorHeight, out float outVal);
				return outVal;
			}
			set => SetFloat(SettingNames.SensorHeight, value);
		}

		public int SssMethod
		{
			get
			{
				GetInt(SettingNames.SssMethod, DefaultEngineSettings.SssMethod, out int outVal);
				return outVal;
			}
			set => SetInt(SettingNames.SssMethod, value);
		}
		public bool AllowSelectedDeviceOverride => RcCore.It.AllSettings.AllowSelectedDeviceOverride;
		public ccl.Device RenderDevice => RcCore.It.AllSettings.RenderDevice;
		public virtual bool ShowMaxPasses
		{
			get { GetBool(SettingNames.MaxPasses, DefaultEngineSettings.ShowMaxPasses, out bool outVal); return outVal; }
			set => SetBool(SettingNames.MaxPasses, value);
		}
		#endregion

		#region Application / Global settings
		public string SelectedDeviceStr
		{
			get
			{
				return RcCore.It.AllSettings.SelectedDeviceStr;
			}
			set => throw new InvalidOperationException();
		}
		public string IntermediateSelectedDeviceStr
		{
			get
			{
				return RcCore.It.AllSettings.IntermediateSelectedDeviceStr;
			}
			set => throw new InvalidOperationException();
		}
		public int ThrottleMs
		{
			get
			{
				return RcCore.It.AllSettings.ThrottleMs;
			}
			set => throw new InvalidOperationException();
		}
		public int Threads
		{
			get { return RcCore.It.AllSettings.Threads; }
			set => throw new InvalidOperationException();
		}


		public int OpenClDeviceType
		{
			get
			{
				return RcCore.It.AllSettings.OpenClDeviceType;
			}
			set => throw new InvalidOperationException();
		}
		public bool OpenClSingleProgram
		{
			get
			{
				return RcCore.It.AllSettings.OpenClSingleProgram;
			}
			set => throw new InvalidOperationException();
		}
		public int OpenClKernelType
		{
			get
			{
				return RcCore.It.AllSettings.OpenClKernelType;
			}
			set => throw new InvalidOperationException();
		}

		public bool CPUSplitKernel
		{
			get
			{
				return RcCore.It.AllSettings.CPUSplitKernel;
			}
			set => throw new InvalidOperationException();
		}

		public bool NoShadows
		{
			get
			{
				return RcCore.It.AllSettings.NoShadows;
			}
			set => throw new InvalidOperationException();
		}

		public float DpiScale
		{
			get
			{
				return RcCore.It.AllSettings.DpiScale;
			}
			set => throw new InvalidOperationException();
		}

		public int PreviewSamples
		{
			get
			{
				return RcCore.It.AllSettings.PreviewSamples;
			}
			set => throw new InvalidOperationException();
		}
		#endregion

	}
}
