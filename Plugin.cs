/**
Copyright 2014-2017 Robert McNeel and Associates

Licensed under the Apache License, Version 2.0(the "License");
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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using ccl;
using Rhino;
using Rhino.UI;
using Rhino.PlugIns;
using Rhino.Render;
using RhinoCyclesCore.Settings;
using RhinoCyclesCore.Core;

namespace RhinoCycles
{
	public class Plugin : PlugIn
	{
		/// <summary>
		/// Make sure we load AtStartup so that our view mode is
		/// available even when RhinoCycles isn't the current renderer
		/// </summary>
		public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

		private bool pluginLoaded = false;

		private bool IsIntelOpenClSdkInstalled() {
			List<string> proglocs = new List<string>
			{
				Environment.GetEnvironmentVariable("PROGRAMFILES(x86)"),
				Environment.GetEnvironmentVariable("PROGRAMFILES")
			};

			List<string> intelbits = new List<string>
			{
				"Intel\\OpenCL SDK",
				"Common Files\\Intel\\OpenCL"
			};
			foreach (var progloc in proglocs) {
				foreach(var intelbit in intelbits) {
					var directory = $"{progloc}\\{intelbit}";
					if (Directory.Exists(directory))
					{
						return true;
					}

				}
			}
			return false;
		}

		private bool SkipOpenCl() {
#if ON_RUNTIME_WIN
			SkipList skipList = new SkipList(SettingsDirectory);

			var gpuNames = DisplayDeviceInfo.GpuNames();

			bool shouldskip = false;

			foreach (string gpuName in gpuNames)
			{
				shouldskip |= skipList.Hit(gpuName);

			}

			bool v = RcCore.It.AllSettings.OpenClDeviceType == 0 || shouldskip | IsIntelOpenClSdkInstalled();
			return v;
#else
			return true;
#endif
		}
		protected override LoadReturnCode OnLoad(ref string errorMessage)
		{
			if(!pluginLoaded) {
				pluginLoaded = true;
				RhinoApp.Initialized += RhinoApp_Initialized;
				RcCore.It.InitializeResourceManager();

				ccl.Utilities.RegisterConsoleWriter(Rhino.RhinoApp.OutputDebugString);

				// code got moved to separate DLL so use that to register from.
				var rccoreass = typeof(RcCore).Assembly;
				RenderContent.RegisterContent(rccoreass, Id);

				var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
				RcCore.It.PluginPath = path;
				var kernelPath = Path.Combine(path, "RhinoCycles");
				RcCore.It.KernelPath = kernelPath;
				var appPath = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);
				RcCore.It.AppPath = appPath;
				kernelPath = RcCore.GetRelativePath(appPath, kernelPath);
				RcCore.It.KernelPathRelative = kernelPath;

				var dataPath = SettingsDirectory;
				var userPath = Path.Combine(dataPath, "..", "data");
				userPath = Path.GetFullPath(userPath);

				if(!Directory.Exists(userPath)) {
					Directory.CreateDirectory(userPath);
				}

				RcCore.It.DataUserPath = userPath;

				CSycles.path_init(RcCore.It.KernelPath, RcCore.It.DataUserPath);

				// TODO: Is this the right spot?
				IntPtr perlin_noise_array = RenderTexture.GetProceduralPerlinNoiseArrayPointer();
				uint perlin_noise_array_size = RenderTexture.GetProceduralPerlinNoiseArraySize();
				CSycles.set_rhino_perlin_noise_table(perlin_noise_array, perlin_noise_array_size);

				// TODO: Is this the right spot?
				IntPtr impulse_noise_array = RenderTexture.GetProceduralImpulseNoiseArrayPointer();
				uint impulse_noise_array_size = RenderTexture.GetProceduralImpulseNoiseArraySize();
				CSycles.set_rhino_impulse_noise_table(impulse_noise_array, impulse_noise_array_size);

				// TODO: Is this the right spot?
				IntPtr vc_noise_array = RenderTexture.GetProceduralVcNoiseArrayPointer();
				uint vc_noise_array_size = RenderTexture.GetProceduralVcNoiseArraySize();
				CSycles.set_rhino_vc_noise_table(vc_noise_array, vc_noise_array_size);

				// TODO: Is this the right spot?
				IntPtr aaltonen_noise_array = RenderTexture.GetProceduralAaltonenNoiseArrayPointer();
				uint aaltonen_noise_array_size = RenderTexture.GetProceduralAaltonenNoiseArraySize();
				CSycles.set_rhino_aaltonen_noise_table(aaltonen_noise_array, aaltonen_noise_array_size);

				if (RhinoApp.RunningOnVMWare() || SkipOpenCl()) {
					CSycles.debug_set_opencl_device_type(0);
				} else {
					CSycles.debug_set_opencl_device_type(RcCore.It.AllSettings.OpenClDeviceType);
					CSycles.debug_set_opencl_kernel(RcCore.It.AllSettings.OpenClKernelType);
					CSycles.debug_set_opencl_single_program(RcCore.It.AllSettings.OpenClSingleProgram);
				}
				CSycles.debug_set_cpu_kernel(RcCore.It.AllSettings.CPUSplitKernel);

				RcCore.It.Initialised = false;
#if ON_RUNTIME_WIN
				RhinoCyclesCore.RenderEngine._MonitorPixelCount = (int)(RhinoEtoApp.MainWindow.Screen.Bounds.Width * RhinoEtoApp.MainWindow.Screen.Bounds.Height);
#else
				RhinoCyclesCore.RenderEngine._MonitorPixelCount = (int)(Eto.Forms.Screen.PrimaryScreen.Bounds.Width * Eto.Forms.Screen.PrimaryScreen.Bounds.Height);
#endif
				AsyncInitialise();
			}
			return LoadReturnCode.Success;
		}

		private void RhinoApp_Initialized(object sender, EventArgs e)
		{
			RcCore.It.AppInitialised = true;
		}


		private static readonly object InitialiseLock = new object();
		private void AsyncInitialise()
		{
			var t = new Thread(InitialiseCSycles);
			t.Start();
		}

		/// <summary>
		/// Initialise Cycles if necessary.
		/// </summary>
		public void InitialiseCSycles()
		{
			lock(InitialiseLock)
			{
				if(!RcCore.It.Initialised)
				{
					if(File.Exists(Path.Combine(SettingsDirectory, "disable_gpus")) || Rhino.RhinoApp.IsSafeModeEnabled)
					{
						CSycles.initialise(DeviceTypeMask.CPU);
					} else
					{
						try
						{
							CSycles.initialise(DeviceTypeMask.All);
						}
						catch (Exception)
						{
							CSycles.initialise(DeviceTypeMask.CPU);
							RhinoCyclesCore.Utilities.DisableGpus();
						}
						RcCore.It.InitialiseOpenCl();
					}
					RcCore.It.Initialised = true;

					RcCore.It.TriggerInitialisationCompleted(this);
				}
			}
		}


		protected override void OnShutdown()
		{
			RhinoApp.Initialized -= RhinoApp_Initialized;
			/* Clean up everything from C[CS]?ycles. */
			RcCore.It.Shutdown();
			base.OnShutdown();
		}

		protected override void OptionsDialogPages(List<Rhino.UI.OptionsDialogPage> pages)
		{
			var optionsPage = new RhinoCyclesCore.Settings.OptionsDialogPage();
			pages.Add(optionsPage);
			base.OptionsDialogPages(pages);
		}
	}
}
