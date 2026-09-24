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
using Rhino.Runtime.Notifications;
using Rhino.UI;
using RhinoCyclesCore.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
				RcCore.It.PurgeOldLogs(false);
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
				// RH-96272: Ensure CUDA compiler cache is on a local path.
				RcCore.It.EnsureLocalCudaCachePath();

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
		/// <summary>
		/// Note in the Notifications panel that no GPU is left, so the user finds out they are
		/// rendering on the CPU without opening the Rhino Render options page. RH-98701.
		/// </summary>
		/// <summary>
		/// A backend can come up, find nothing it can use, and report no failure at all - the
		/// tabs simply never appear. failed_gpus_mask() is 0 in that case, so the failure path
		/// above never runs. Catch it by comparing what the system reports against what Cycles
		/// ended up offering. RH-98701.
		/// </summary>
		private static void CheckForMissingGpus()
		{
			try
			{
				// Windows only: without an independent list of adapters there is nothing to compare
				// against, and we would be guessing.
				var systemGpus = DisplayDeviceInfo.GpuDeviceInfos();
				if (systemGpus == null || systemGpus.Count == 0) return;

				var devices = Device.Devices.ToList();
				if (devices.Any(d => d.IsGpu))
				{
					RhinoCyclesCore.Utilities.ForgetGpuAbsent();
					return;
				}

				var names = string.Join(", ", systemGpus.Select(g => g.Name));
				var offered = string.Join(", ", devices.Select(d => d.Type.ToString()));
				RhinoCyclesCore.Utilities.RecordGpuAbsent(names, offered);
				RcCore.It.AddLogString($"No GPU available to Cycles although the system reports {names}; offering {offered}");
			}
			catch (Exception ex)
			{
				RcCore.It.AddLogString($"Could not check for missing GPUs: {ex.Message}");
			}
		}

		/// <param name="firstTime">
		/// True when a failure was recorded in this session, i.e. the user has not been shown
		/// this yet. The panel is only pushed to the front then; on later starts the warning is
		/// still listed, but quietly.
		/// </param>
		private static void NotifyAboutDisabledGpus(bool firstTime)
		{
			var names = RhinoCyclesCore.Utilities.DisabledGpuNames;
			var absent = RhinoCyclesCore.Utilities.GpuAbsentRecord;
			if (string.IsNullOrEmpty(names) && string.IsNullOrEmpty(absent)) return;

			// RH-98730: one backend failing is not news while another GPU still renders - the
			// Rhino Render options page names the one that is off. Only speak up when we really
			// did fall back to the CPU.
			if (Device.Devices.Any(d => d.IsGpu))
			{
				RcCore.It.AddLogString($"Not notifying about switched off GPU {names}; a usable GPU remains");
				return;
			}

			// Init runs on its own thread; notifications are UI-thread only.
			RhinoApp.InvokeOnUiThread(new Action(() =>
			{
				try
				{
					var note = new Notification
					{
						// RH-98730: anything above Info forces the panel open again and again.
						SeverityLevel = Notification.Severity.Info,
						Title = Localization.LocalizeString("Rhino Render is using the CPU", 114),
						// Description is the one line the Notifications panel lists; Message is the detail
						// shown when the notification is opened.
						Description = string.IsNullOrEmpty(names)
							? Localization.LocalizeString("No GPU found - using the CPU.", 115)
							// RH-98730: worded so it reads for one backend and for several.
							: string.Format(Localization.LocalizeString("{0} switched off - using the CPU.", 116), names),
						Message = string.IsNullOrEmpty(names)
							? Localization.LocalizeString("Rhino Render found no usable GPU, so it falls back to the CPU, which is much slower. This is usually a graphics driver that is too old for the card. Update the graphics driver and restart Rhino.", 117)
							: string.Format(
								Localization.LocalizeString("{0} failed to start, so Rhino Render and Raytraced fall back to the CPU, which is much slower. This is usually a graphics driver that is too old for the card. Update the driver, then try again.", 118),
								names),
						ConfirmButtonTitle = string.IsNullOrEmpty(names) ? null : Localization.LocalizeString("Retry GPUs", 119),
						CancelButtonTitle = string.IsNullOrEmpty(names) ? Localization.LocalizeString("Close", 120) : Localization.LocalizeString("Keep CPU", 121),
					};
					note["RhinoCycles"] = "disabled-gpus";
					note.ButtonClicked = (button) =>
					{
						if (button != ButtonType.Confirm) return;
						if (string.IsNullOrEmpty(RhinoCyclesCore.Utilities.DisabledGpuNames)) return;
						RhinoCyclesCore.Utilities.EnableGpuBackends();
						NotificationCenter.Notifications.Remove(note);
					};
					NotificationCenter.Notifications.Add(note);

					// Bring the panel forward only when this is news - nagging at every start is worse
					// than the tab staying closed. GUID of Commands.UI.NotificationsPanel, whose plug-in
					// is not referenceable from here.
					if (firstTime)
					{
						Panels.OpenPanel(new Guid("9A0FA999-295D-4D77-B160-074FA2CD8E6D"), true);
						RcCore.It.AddLogString($"Opened the Notifications panel for switched off GPUs: {names}");
					}
				}
				catch (Exception ex)
				{
					RcCore.It.AddLogString($"Could not post the disabled-GPU notification: {ex.Message}");
				}
			}));
		}

		public void InitialiseCSycles()
		{
			lock(InitialiseLock)
			{
				if(!RcCore.It.Initialised && !RcCore.It.InitialisationFailed)
				{
					RcCore.It.AddLogString("InitialiseCSycles entry");

					// RH-96737: This runs on a background thread, so any exception escaping
					// here (e.g. a DllNotFoundException because ccycles or one of its native
					// dependencies cannot be loaded under Rhino.Inside) would be unhandled and
					// abort the whole host process. Guard it: log, mark Cycles unavailable, and
					// keep the host alive instead of crashing it.
					try
					{

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
						DeviceTypeMask retried = RhinoCyclesCore.Utilities.ClearStaleGpuDisables();
						if (retried != 0)
						{
							RcCore.It.AddLogString($"RhinoCycles GPU {retried} was disabled in a different GPU/driver/Rhino environment; trying again");
						}
						DeviceTypeMask previouslyDisabled = RhinoCyclesCore.Utilities.DisabledGpus;
						CSycles.initialise(DeviceTypeMask.All & ~previouslyDisabled);

						DeviceTypeMask failed = CSycles.failed_gpus_mask();
						foreach (DeviceType t in Enum.GetValues(typeof(DeviceType)))
						{
							var bit = (DeviceTypeMask)(1u << (int)t);
							if ((failed & bit) == 0) continue;
							var initError = CSycles.gpu_init_error(t);
							bool newlyRecorded = RhinoCyclesCore.Utilities.DisableGpu(bit, initError, failed);
							RcCore.It.AddLogString($"RhinoCycles GPU {t} failed to initialise; disabled for next start{(newlyRecorded ? " (recorded)" : "")}: {initError}");
						}

						CheckForMissingGpus();
						NotifyAboutDisabledGpus(RhinoCyclesCore.Utilities.TakeGpuAnnouncement());

						if (RcCore.It.AllSettings.StartGpuKernelCompiler)
						{
							RcCore.It.InitialiseGpuKernels();
						}
					}
					RcCore.It.Initialised = true;

					RcCore.It.TriggerInitialisationCompleted(this);
					RcCore.It.AddLogString("InitialiseCSycles exit");
					}
					catch (Exception ex)
					{
						// Do not let this escape the background thread - it would abort the host.
						RcCore.It.InitialisationFailed = true;
						RcCore.It.AddLogString($"InitialiseCSycles failed; Cycles is unavailable: {ex}");
					}
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
