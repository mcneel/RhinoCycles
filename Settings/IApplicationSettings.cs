/**
Copyright 2014-2024 Robert McNeel and Associates

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

namespace RhinoCyclesCore.Settings
{
	public interface IApplicationSettings
	{
		string SelectedDeviceStr { get; set; }
		string IntermediateSelectedDeviceStr { get; set; }
		int ThrottleMs { get; set; }
		int Threads { get; set; }

		bool ExperimentalCpuInMulti { get; set; }


		int OpenClDeviceType { get; set; }
		bool OpenClSingleProgram { get; set; }
		int OpenClKernelType { get; set; }

		bool CPUSplitKernel { get; set; }

		int PixelSize { get; set; }
		float OldDpiScale { get; set; }

		int PreviewSamples { get; set; }
		bool UseStartResolution { get; set; }
		int StartResolution { get; set; }

		bool DumpMaterialShaderGraph { get; set; }
		bool DumpEnvironmentShaderGraph { get; set; }

		bool StartGpuKernelCompiler { get; set; }
		bool VerboseLogging { get; set; }
		int RetentionDays { get; set; }

		int TriggerPostEffectsSample { get; set; }

		bool UseLightTree { get; set; }
		bool UseAdaptiveSampling { get; set; }
		int AdaptiveMinSamples { get; set; }
		float AdaptiveThreshold { get; set; }
	}
}
