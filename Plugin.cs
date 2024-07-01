/**
Copyright 2014-2024 Robert McNeel and Associates

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

using ccl;
using Rhino;
using Rhino.PlugIns;
using Rhino.Render;
using Rhino.Runtime;
using Rhino.UI;
using RhinoCyclesCore.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

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

		protected override LoadReturnCode OnLoad(ref string errorMessage)
		{
			string os = HostUtils.RunningOnWindows ? "Windows" : "MacOS";
			if(!pluginLoaded) {
				var dataPath = SettingsDirectory;
				var userPath = Path.Combine(dataPath, "..", "data");
				userPath = Path.GetFullPath(userPath);
				if(!Directory.Exists(userPath)) {
					Directory.CreateDirectory(userPath);
				}

				RcCore.It.DataUserPath = userPath;

				RcCore.It.InitializeLog();
				RcCore.It.PurgeOldLogs();
				RcCore.It.StartLogStopwatch("OnLoad");
				RcCore.It.AddLogString($"Running on {os}");
				RcCore.It.AddLogString("RhinoCycles OnLoad entry");
				Stopwatch sw = new Stopwatch();
				sw.Start();
				pluginLoaded = true;
				RhinoApp.Initialized += RhinoApp_Initialized;
				RcCore.It.InitializeResourceManager();

				ccl.Utilities.RegisterConsoleWriter(RcCore.It.AddLogStringIfVerbose);

				// code got moved to separate DLL so use that to register from.
				var rccoreass = typeof(RcCore).Assembly;
				RcCore.It.AddLogString("RhinoCycles OnLoad: RegisterContent start");
				RenderContent.RegisterContent(rccoreass, Id);
				RcCore.It.AddLogString("RhinoCycles OnLoad: RegisterContent end");

				var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
				RcCore.It.PluginPath = path;
				var kernelPath = Path.Combine(path, "RhinoCycles");
				RcCore.It.KernelPath = kernelPath;
				var appPath = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);
				RcCore.It.AppPath = appPath;
				kernelPath = RcCore.GetRelativePath(appPath, kernelPath);
				RcCore.It.KernelPathRelative = kernelPath;

				RcCore.It.Initialised = false;
#if ON_RUNTIME_WIN
				RhinoCyclesCore.RenderEngine._MonitorPixelCount = (int)(RhinoEtoApp.MainWindow.Screen.Bounds.Width * RhinoEtoApp.MainWindow.Screen.Bounds.Height);
#else
				RhinoCyclesCore.RenderEngine._MonitorPixelCount = (int)(Eto.Forms.Screen.PrimaryScreen.Bounds.Width * Eto.Forms.Screen.PrimaryScreen.Bounds.Height);
#endif
				AsyncInitialise();

				var timeTaken = sw.Elapsed;
				RcCore.It.AddLogString($"RhinoCycles loaded in: {timeTaken}");
				RcCore.It.AddLogString("RhinoCycles OnLoad exit");
			}
			return LoadReturnCode.Success;
		}

		private void RhinoApp_Initialized(object sender, EventArgs e)
		{
			RcCore.It.AddLogString("RhinoApp_Initialized");
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
					RcCore.It.AddLogString("InitialiseCSycles entry");

					// Curtis RH-79171: Ensure that we don't load ccycles.dll during OnLoad, it
					// can add a 15-40 second delay on initial/first startup due to Windows Defender

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

					if (File.Exists(Path.Combine(SettingsDirectory, "disable_gpus")) ||
					  Rhino.RhinoApp.IsSafeModeEnabled
						)
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
						if (RcCore.It.AllSettings.StartGpuKernelCompiler)
						{
							RcCore.It.InitialiseGpuKernels();
						}
					}
					RcCore.It.Initialised = true;

					RcCore.It.TriggerInitialisationCompleted(this);
					RcCore.It.AddLogString("InitialiseCSycles exit");
				}
			}
		}


		protected override void OnShutdown()
		{
			RcCore.It.AddLogString("OnShutdown start");
			RhinoApp.Initialized -= RhinoApp_Initialized;
			/* Clean up everything from C[CS]?ycles. */
			RcCore.It.AddLogString("RcCore.It.Shutdown start");
			RcCore.It.Shutdown();
			RcCore.It.AddLogString("RcCore.It.Shutdown end");
			RcCore.It.AddLogString("base.OnShutdown start");
			base.OnShutdown();
			RcCore.It.AddLogString("base.OnShutdown end");
			RcCore.It.AddLogString("OnShutdown exit");
		}

		protected override void OptionsDialogPages(List<Rhino.UI.OptionsDialogPage> pages)
		{
			var optionsPage = new RhinoCyclesCore.Settings.OptionsDialogPage();
			pages.Add(optionsPage);
			base.OptionsDialogPages(pages);
		}

		public override bool IsTextureSupported(RenderTexture texture)
		{
			if (texture == null ||
				texture.TypeId == ContentUuids.AdvancedDotTextureType ||
				texture.TypeId == ContentUuids.ResampleTextureType)
			{
				return false;
			}

			return true;
		}
	}
}
