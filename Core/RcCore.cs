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

using ccl;
using Rhino;
using Rhino.Runtime;
using Rhino.UI;
using RhinoCyclesCore.Settings;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace RhinoCyclesCore.Core
{
	public sealed class RcCore
	{

		#region helper functions to get relative path between two paths
		public static string GetRelativePath(string fromPath, string toPath)
		{
			bool hit = false;
			int l = 1;
			// find length of common path.
			if (toPath.StartsWith(fromPath))
			{
				hit = true;
				l = fromPath.Length + 1;
			} else {
				while (!hit)
				{
					if (l > fromPath.Length) break;

					string ss = fromPath.Substring(0, l);
					if (!toPath.StartsWith(ss))
					{
						hit = true;
						break;
					}
					l++;
				}
			}

			if (!hit) throw new ArgumentException("Paths must have common start");

			// we found a hit, now determine the relative jump
			string remainder = fromPath.Substring(l - 1);
			string toremainder = toPath.Substring(l - 1);
			var sp = remainder.Split(Path.DirectorySeparatorChar);
			List<string> relp = new List<string>(sp);
			relp = relp.FindAll(x => x.Length > 0);
			for(int i = 0; i < relp.Count; i++)
			{
				relp[i] = "..";
			}

			// add the path of the remainder in toPath
			relp.Add(toremainder);

			// combine into final relative path.
			var relpstr = System.IO.Path.Combine(relp.ToArray());
			// add a dot if string starts with a directory separator
			if (relpstr.StartsWith(System.IO.Path.DirectorySeparatorChar.ToString())) relpstr = "." + relpstr;

			return relpstr;
		}

		#endregion


		public void InitializeResourceManager()
		{
			Properties.Resources.Culture = CultureInfo.InvariantCulture;
		}

		public void TriggerInitialisationCompleted(object sender)
		{
			InitialisationCompleted?.Invoke(sender, EventArgs.Empty);
		}

		/// <summary>
		/// Event signalling that CCSycles initialisation has been completed.
		/// </summary>
		public event EventHandler InitialisationCompleted;
		/// <summary>
		/// Flag to keep track of CSycles initialisation
		/// </summary>
		public bool Initialised { get; set; }

		/// <summary>
		/// Event triggred when a rendering device is ready.
		/// </summary>
		public event EventHandler DeviceKernelReady;

		/// <summary>
		/// Flag to tell us when Rhino has completed its initialisation.
		/// </summary>
		public bool AppInitialised { get; set; }

		/// <summary>
		/// Get the path used to look up .cubins (absolute)
		/// </summary>
		public string KernelPath { get; set; }

		/// <summary>
		/// Get the path where runtime created data like compiled kernels and BVH caches are stored.
		/// </summary>
		public string DataUserPath { get; set; }

		public string GpuCompilePath => Path.Combine(DataUserPath, "gpus");

		/// <summary>
		/// Get the path used to look up .cubins (relative)
		/// </summary>
		public string KernelPathRelative { get; set; }

		public string PluginPath { get; set; }

		public string AppPath { get; set; }

		public ApplicationAndDocumentSettings AllSettings { get; }

		private RcCore() {
			AppInitialised = false;
			if(AllSettings == null)
				AllSettings = new ApplicationAndDocumentSettings();
		}

		/// <summary>
		/// The one RcCore instance
		/// </summary>
		public static RcCore It { get; } = new RcCore();

		public static void OutputDebugString(string msg)
		{
#if OUTPUTDEBUGSTRINGS
			RhinoApp.OutputDebugString(msg);
#endif
		}

		ConcurrentDictionary<IntPtr, Session> sessions = new ConcurrentDictionary<IntPtr, Session>();
		/// <summary>
		/// Shut down Cycles on all levels. Wait for all active session to complete.
		/// </summary>
		public void Shutdown() {
			ClearLogQueues();

			ReleaseActiveSessions();
			int count;
			int timer = 0;
			if(checkGpuKernelCompilationCompletedThread!=null) {
				stopCheckingForGpuKernelCompileFinished = true;
				try
				{
					checkGpuKernelCompilationCompletedThread.Join();
				}
				finally {
					checkGpuKernelCompilationCompletedThread = null;
				}
			}
			if(kernelCompilerThread!=null)
			{
				try
				{
					kernelCompilerThread.Join(500);
				}
				finally {
					kernelCompilerThread = null;
				}
			}
			while((count = sessions.Count) > 0 ) {
				if(timer%50==0)
					AddLogString($"Number of sessions we wait for {count}\n");
				Thread.Sleep(10);
				timer++;
			}
			AddLogString($"All sessions cleaned up\n");
			gpuDevicesReadiness.Clear();
			CSycles.shutdown();
			logTw.Close();
			logTw.Dispose();
		}

		ConcurrentDictionary<IntPtr, Session> active_sessions = new ConcurrentDictionary<IntPtr, Session>();

		/// <summary>
		/// Create a ccl.Session
		/// </summary>
		/// <param name="sessionParameters"></param>
		/// <returns></returns>
		public Session CreateSession(SessionParameters sessionParameters)
		{
			var session = new Session(sessionParameters);
			AddLogStringIfVerbose($"Created session {session.Id}.\n");

			active_sessions[session.Id] = session;

			return session;
		}

		public void ReleaseSession(Session session)
		{
			if(active_sessions.ContainsKey(session.Id))
			{
				session.QuickCancel();
				while(!active_sessions.TryRemove(key: session.Id, value: out _))
				{
					Thread.Sleep(10);
				}
				session.Dispose();
			}
		}

		public void ReleaseActiveSessions()
		{
			WaitUntilLockedThenUnlockPreviewRendererLock();
			var sessions = active_sessions.Values.ToList();
			foreach (var session in sessions)
			{
				ReleaseSession(session);
			}
		}

		/// <summary>
		/// Check if given device is ready to be used for rendering. If it is the
		/// returned tuple will have isDeviceReady set to true and actualDevice is
		/// the device checked.
		///
		/// If it is not ready isDeviceReady will be set to false and the
		/// actualDevice will be set to the default device (Cpu).
		/// </summary>
		/// <param name="device">Device to check for readiness</param>
		/// <returns>Tuple with isDeviceReady set to true and device in actualDevice
		/// if device is ready.
		///
		/// Otherwise isDeviceReady will be false and actualDevice Device.Default.
		/// </returns>
		public (bool isDeviceReady, Device actualDevice) IsDeviceReady(Device device) {
			if (device.Type != DeviceType.Multi)
			{
				lock (accessGpuKernelDevicesReadiness)
				{
					var devReadiness = gpuDevicesReadiness.Find(
						d =>
							d.DeviceAndPath.Device.Equals(device)
					);
					if (devReadiness.IsReady) return (true, device);
				}
			}
			else
			{
				lock (accessGpuKernelDevicesReadiness)
				{
					bool allSubdevicesReady =
						!(from devreadiness in gpuDevicesReadiness
						where device.Subdevices.Contains(devreadiness.DeviceAndPath.Device)
						select devreadiness.IsReady).Any(b => b == false);
					if (allSubdevicesReady) return (true, device);
				}
			}
			return (false, Device.Default);
		}

		/// <summary>
		/// The main OpenCL compiler thread.
		/// </summary>
		Thread kernelCompilerThread = null;
		/// <summary>
		/// Thread that will check if compiles have finished.
		/// </summary>
		Thread checkGpuKernelCompilationCompletedThread = null;
		/// <summary>
		/// Start the process to initialize OpenCL for Cycles in a separate thread.
		/// Before starting the process create a list of all OpenCL devices and
		/// initialize their readiness state to false.
		/// </summary>
		public void InitialiseGpuKernels()
		{
			InitialiseGpuDeviceReadinessList();

			DeviceKernelReady?.Invoke(this, EventArgs.Empty);

			kernelCompilerThread = new Thread(StartCompileGpuKernels);
			kernelCompilerThread.Start();

			checkGpuKernelCompilationCompletedThread = new Thread(CheckGpuKernelCompileFinished);
			checkGpuKernelCompilationCompletedThread.Start();
		}

		private void InitialiseGpuDeviceReadinessList()
		{
			gpuDevicesReadiness.Clear();

			foreach(Device device in Device.Devices)
			{
				gpuDevicesReadiness.Add((new (device, GenerateGpuDeviceInfo(device)), device.IsCpu));
			}
		}

		/// <summary>
		/// Lock to be used for protecting preview render engine usage
		/// </summary>
		public readonly object PreviewRendererLock = new object();
		public void WaitUntilLockedThenUnlockPreviewRendererLock()
		{
			lock(PreviewRendererLock)
			{
				RcCore.It.AddLogStringIfVerbose("WaitUntilLockedThenUnlockPreviewRenderer: locked");
			}
		}

		/// <summary>
		/// lock object for access to gpuDevicesReadiness
		/// </summary>
		private readonly object accessGpuKernelDevicesReadiness = new object();
		/// <summary>
		/// List holding OpenCL devices and their readiness state.
		/// </summary>
		List<(DeviceAndPath DeviceAndPath, bool IsReady)> gpuDevicesReadiness = new List<(DeviceAndPath, bool)>();
		/// <summary>
		/// List of SHA256 hashes created from device.NiceName + driver date + rhino app version.
		/// The names are used to create a file with that name when the OpenCL compilation
		/// has finished successfully.
		/// </summary>
		List<string> deviceFileNames = new List<string>();
		/// <summary>
		/// available devices on the system. needed mostly for driver date
		/// </summary>
		List<(string DeviceName, string DriverDate)> availableGpuDevices = (from devinfo in DisplayDeviceInfo.GpuDeviceInfos() select (DeviceName: devinfo.Name, DriverDate: devinfo.DriverDateAsString)).ToList();
		bool stopCheckingForGpuKernelCompileFinished = false;
		public void CheckGpuKernelCompileFinished()
		{
			if(gpuDevicesReadiness.Count == 0) return;

			do
			{
				Thread.Sleep(100);
				for (int idx = 0; idx < gpuDevicesReadiness.Count; idx++)
				{
					(DeviceAndPath device, bool isReady) = gpuDevicesReadiness[idx];

					// first handle non-multi devices
					if (!device.Device.IsMulti && File.Exists(path: device.Path) && !isReady)
					{
						lock (accessGpuKernelDevicesReadiness)
						{
							gpuDevicesReadiness[idx] = (device, true);
							DeviceKernelReady?.Invoke(this, EventArgs.Empty);
						}
						if(HostUtils.RunningOnWindows && It.AllSettings.RenderDevice.Equals(device.Device))
						{
							ToggleViewportsRunningRealtime();
						}
					}
					// then handle multi device
					if(device.Device.IsMulti && !isReady) {
						bool allSubdevicesReady =
							!(from devreadiness in gpuDevicesReadiness
							where device.Device.Subdevices.Contains(devreadiness.DeviceAndPath.Device)
							select devreadiness.IsReady).Any(b => b == false);

						if(allSubdevicesReady)
						{
							lock (accessGpuKernelDevicesReadiness)
							{
								gpuDevicesReadiness[idx] = (device, true);
								DeviceKernelReady?.Invoke(this, EventArgs.Empty);
							}
						}
					}
				}
				bool ready = true;
				lock (accessGpuKernelDevicesReadiness)
				{
					ready = gpuDevicesReadiness.Aggregate(true, (state, next) => state && next.IsReady);
				}
				if (ready)
				{
					break;
				}
			}
			while (!stopCheckingForGpuKernelCompileFinished);
		}

		private static void ToggleViewportsRunningRealtime()
		{
			var dmwire = Rhino.Display.DisplayModeDescription.FindByName("Wireframe");
			var dmray = Rhino.Display.DisplayModeDescription.FindByName("Raytraced");
			foreach (RhinoDoc doc in RhinoDoc.OpenDocuments())
			{
				if (doc.Views.ActiveView == null) continue;

				string activeViewName = doc.Views.ActiveView.ActiveViewport.Name;
				foreach (var view in doc.Views)
				{
					if (view.RealtimeDisplayMode != null && view.RealtimeDisplayMode.HudProductName().Contains("Cycles"))
					{
						RhinoApp.InvokeOnUiThread(() =>
						{
							_ = RhinoApp.RunScript($"_SetActiveViewport \"{view.ActiveViewport.Name}\"", false);
							_ = RhinoApp.RunScript("_SetDisplayMode _Wireframe", false);
							_ = RhinoApp.RunScript("_SetDisplayMode _Raytraced", false);
						}, new object[]{ });
					}
				}
				_ = RhinoApp.RunScript($"_SetActiveViewport \"{activeViewName}\"", false);
			}
		}

		private void EnsureGpuCompilePath()
		{
			if(!Directory.Exists(path: GpuCompilePath))
			{
				Directory.CreateDirectory(path: GpuCompilePath);
			}
		}

		private void PurgeStaleCompileFiles()
		{
			foreach (string ext in new string[] { "*.compiling", "*.task" })
			{
				var foundFiles = Directory.EnumerateFiles(path: GpuCompilePath, ext);
				foreach (string fileName in foundFiles)
				{
					TimeSpan span = DateTime.Now - File.GetLastWriteTime(fileName);
					if (span.TotalMinutes > 30)
					{
						try
						{
							File.Delete(fileName);
						}
						finally { }
					}
				}
			}
		}

		private string GenerateGpuDeviceInfo(Device device)
		{
			var info = "";
			foreach (var gpudev in availableGpuDevices)
			{
				if (gpudev.DeviceName.Contains(device.NiceName) || device.NiceName.Contains(gpudev.DeviceName))
				{
					using (SHA256 sha = SHA256.Create())
					{
						var hash = sha.ComputeHash(
							buffer: Encoding.UTF8.GetBytes(s: $"{device.NiceName}{gpudev.DriverDate}{RhinoApp.Version}")
						);

						info = Path.Combine(
							GpuCompilePath,
							string.Concat(values: hash.Select(b => b.ToString(format: "x2"))));
						break;
					}
				}
			}
			if(string.IsNullOrEmpty(info))
			{
					using (SHA256 sha = SHA256.Create())
					{
						var hash = sha.ComputeHash(
							buffer: Encoding.UTF8.GetBytes($"{device.NiceName}{RhinoApp.Version}")
						);

						info = Path.Combine(
							GpuCompilePath,
							string.Concat(values: hash.Select(b => b.ToString(format: "x2")))
						);
					}
			}

			return info;
		}

		Random rng = new Random();
		/// <summary>
		/// Write GPU data to file to use in RhinoCyclesKernelCompile
		/// For given device list.
		/// </summary>
		/// <returns>Return path to file</returns>
		private string WriteGpuDevicesFile(List<Device> devices)
		{
			string gpuTaskFileName;
			using(SHA256 gpuTaskFileSha = SHA256.Create())
			{
				var hash = gpuTaskFileSha.ComputeHash(
					buffer: Encoding.UTF8.GetBytes($"{DateTime.UtcNow} {rng.NextDouble()}" )
				);
				gpuTaskFileName = Path.Combine(
					GpuCompilePath,
					string.Concat(values: hash.Select(b => b.ToString(format: "x2"))) + ".task"
				);
			}

			using (TextWriter tw = new StreamWriter(
						path: gpuTaskFileName,
						append: false,
						encoding: Encoding.UTF8))
			{
				foreach (Device device in devices)
				{
					var info = GenerateGpuDeviceInfo(device: device);
					tw.WriteLine($"{device.Id} || {info}");
				}
				tw.Close();
			}

			return gpuTaskFileName;
		}

		public string CompileLogStdOut { get; set; } = Localization.LocalizeString("Not started", 81);
		public string CompileLogStdErr { get; set; } = "";

		public bool CompileProcessFinished { get; set; } = false;
		public bool CompileProcessError { get; set; } = false;

		public DateTime CompileStartTime { get; set; } = DateTime.MinValue;
		public DateTime CompileEndTime { get; set; } = DateTime.MinValue;

		public string GetFormattedCompileLog()
		{
			SetCompileLog();
			string compout = Localization.LocalizeString("COMPILER OUTPUT", 82);
			string errlog = Localization.LocalizeString("ERROR LOG", 83);
			string compstart = Localization.LocalizeString("Compile start time", 84);
			string compend =   Localization.LocalizeString("Compile end time  ", 85);
			var compendfinal = $"{compend}: {CompileEndTime}";
			if(CompileEndTime.Equals(DateTime.MinValue)) {
				compendfinal = "";
			}

			var log = $"{compout}:\n\n{CompileLogStdOut}\n\n{errlog}:\n\n{CompileLogStdErr}\n\n{compstart}: {CompileStartTime}\n{compendfinal}\n";

			if(CompileStartTime.Equals(DateTime.MinValue)) {
				log = Localization.LocalizeString("Kernel compilation not started", 86);
			}
			return log;
		}

		/// <summary>
		/// Set up the ProcessStartInfo instance used for
		/// running the kernel compiler in a separate process
		/// </summary>
		/// <param name="compileTaskFile"></param>
		/// <returns></returns>
		private ProcessStartInfo SetupProcessStartInfo(string compileTaskFile)
		{
			AddLogString("RcCore SetupProcessStartInfo entry");
			Assembly assembly = Assembly.GetExecutingAssembly();
			string assemblyDirectory = Path.GetDirectoryName(path: assembly.Location);
			AddLogString($"RcCore SetupProcessStartInfo {assembly.Location}");
			string programToRun = Path.Combine(assemblyDirectory, "RhinoCyclesKernelCompiler");
			string argumentsToProgramToRun = $"\"{KernelPath}\" \"{compileTaskFile}\"";
#if ON_RUNTIME_WIN
#if DEBUG
			argumentsToProgramToRun += " inception";
#endif
#endif

			if(HostUtils.RunningOnWindows) {
				programToRun += ".exe";
			}

			// On MacOS we need to run the DLL instead of the created executable, since
			// the executable is going to be CPU specific. Running the DLL through
			// dotnet will work on both Intel and Apple Silicon
			// The dotnet folder we want lives two directories up from this
			// assembly.
			if(HostUtils.RunningOnOSX) {
				DirectoryInfo di = new DirectoryInfo(path: assemblyDirectory).Parent.Parent;
				var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x86_64";
				string dotnet_path = Path.Combine(di.FullName, "dotnet", arch, "dotnet");
				string dll = $"{programToRun}.dll";
				if(!File.Exists(path: dll)) {
					throw new FileNotFoundException($"{dll}");
				}
				argumentsToProgramToRun = $"\"{dll}\" {argumentsToProgramToRun}";
				programToRun = $"{dotnet_path}";
			}

			ProcessStartInfo startInfo = new ProcessStartInfo(
						fileName: programToRun,
						arguments: argumentsToProgramToRun)
			{
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				WorkingDirectory = assemblyDirectory,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8,
			};

			if(HostUtils.RunningOnWindows) {
				startInfo.CreateNoWindow = true;
			}
			if(HostUtils.RunningOnOSX) {
				var dylib_path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assembly.Location), "..", "..", "..", ".."));
				AddLogString($"RcCore SetupProcessStartInfo: dylib_path = {dylib_path}");
				startInfo.EnvironmentVariables.Add("DYLD_FALLBACK_LIBRARY_PATH", $"{dylib_path}");
				startInfo.Environment.Add("DYLD_FALLBACK_LIBRARY_PATH", $"{dylib_path}");
				startInfo.Environment.Add("USE_MONOLITHIC_COMPILER", "1");
			}

			AddLogString("RcCore SetupProcessStartInfo exit");
			return startInfo;
		}

		private List<List<Device>> GetDeviceListings() {
			var cd = AllSettings.RenderDevice;
			if(cd.IsMulti)
			{
				cd = cd.FirstFromMulti;
			}

			List<Device> currentDeviceGroup = (from d in Device.Devices where d.IsGpu && d.Type.Equals(cd.Type) select d).ToList();
			List<Device> otherDeviceGroup = (from d in Device.Devices where d.IsGpu && !d.Type.Equals(cd.Type) select d).ToList();
			List<Device> cudaGroup = (from d in otherDeviceGroup where d.Type.Equals(DeviceType.Cuda) select d).ToList();
			List<Device> optixGroup = (from d in otherDeviceGroup where d.Type.Equals(DeviceType.Optix) select d).ToList();
			otherDeviceGroup = (from d in otherDeviceGroup where !cudaGroup.Contains(d) && !optixGroup.Contains(d) && d.Type != DeviceType.Multi select d).ToList();

			List < List < Device >> deviceListings = new List<List<Device>>
			{
				currentDeviceGroup,
				otherDeviceGroup,
				cudaGroup,
				optixGroup,
			};

			return deviceListings;
		}

		/// <summary>
		/// Compile GPU kernels if necessary
		/// </summary>
		public void StartCompileGpuKernels()
		{
			EnsureGpuCompilePath();
			PurgeStaleCompileFiles();
			ClearLogQueues(LogQueues.CompileOutputs);
			CompileProcessFinished = false;
			CompileProcessError = false;

			compileStdOut.Enqueue(Localization.LocalizeString("Compile started, waiting for results...", 87) + "\n");
			compileStdErr.Enqueue(Localization.LocalizeString("No errors.", 88));
			CompileStartTime = DateTime.Now;
			CompileEndTime = DateTime.MinValue;

			List<List<Device>> deviceListings = GetDeviceListings();
			foreach (List<Device> deviceListing in deviceListings)
			{
				if (deviceListing.Count == 0) continue;

				var compileTaskFile = WriteGpuDevicesFile(deviceListing);
				string startProcessString = Localization.LocalizeString("Start compile process with device count:", 89);
				compileStdOut.Enqueue($"{startProcessString} {deviceListing.Count} ({deviceListing[0].Type})\n");

				try
				{
					ProcessStartInfo startInfo = SetupProcessStartInfo(compileTaskFile);

					var process = new Process();
					process.StartInfo = startInfo;
					process.OutputDataReceived += new DataReceivedEventHandler((sender,e) => {
						if(e.Data != null)
						{
							compileStdOut.Enqueue(e.Data);
							SetCompileLog();
						}
					});
					process.ErrorDataReceived += new DataReceivedEventHandler((sender,e) => {
						if(e.Data != null)
						{
							compileStdErr.Enqueue(e.Data);
							SetCompileLog();
						}
					});
					process.Start();
					process.BeginOutputReadLine();
					process.BeginErrorReadLine();
					//CompileLogStdOut += $"{process.StandardOutput.ReadToEnd()}";

					SetCompileLog();

					process.WaitForExit();
					CompileProcessError = process.ExitCode != 0;
					if (CompileProcessError)
					{
						string compile_failed = Localization.LocalizeString("Compile failed", 90);
						string compile_error_code = Localization.LocalizeString("Error code", 91);
						compileStdOut.Enqueue($"{compile_failed} {CompileLogStdOut}");
						compileStdErr.Enqueue($"{compile_error_code}: {process.ExitCode}\n\n{process.StandardError.ReadToEnd()}");
					}
					process.Close();
				}
				catch (Exception processException)
				{
					compileStdErr.Enqueue($"{processException}\n\n{processException.StackTrace}");
					CompileProcessError = true;
				}
			}
			SetCompileLog();

			CompileProcessFinished = true;
			CompileEndTime = DateTime.Now;

		} /* end of StartCompileGpuKernel() */

		[Flags]
		enum LogQueues {
			RhinoCycles = 0b0000_0001,
			CompileStdOut = 0b0000_0010,
			CompileStdErr = 0b0000_0100,
			CompileOutputs = CompileStdOut | CompileStdErr,
			All = RhinoCycles | CompileStdOut | CompileStdErr,
		}

		private void ClearLogQueues(LogQueues whichQueues = LogQueues.All)
		{
			if((whichQueues & LogQueues.RhinoCycles) == LogQueues.RhinoCycles) while(logStrings.Count > 0) logStrings.TryDequeue(out string _);
			if((whichQueues & LogQueues.CompileStdOut) == LogQueues.CompileStdOut) while(compileStdOut.Count > 0) compileStdOut.TryDequeue(out string _);
			if((whichQueues & LogQueues.CompileStdErr) == LogQueues.CompileStdErr) while(compileStdErr.Count > 0) compileStdErr.TryDequeue(out string _);
		}

		public void SetCompileLog()
		{
			StringBuilder sb = new StringBuilder();
			foreach(string logLine in compileStdOut) {
				sb.AppendLine(logLine);
			}
			CompileLogStdOut = sb.ToString();
			StringBuilder sberr = new StringBuilder();
			foreach(string logLine in compileStdErr) {
				sberr.AppendLine(logLine);
			}
			CompileLogStdErr = sberr.ToString();
		}

		private bool EnsureCompilerIsNotRunning()
		{
			if (HostUtils.RunningOnWindows)
			{
				Process[] processes = Process.GetProcesses();
				processes = (
					from process
					in processes
					where process.ProcessName.Contains("RhinoCycles")
					select process)
				.ToArray();

				foreach (Process process in processes)
				{
					try
					{
						process.Kill();
					}
					finally { }
				}
			}
			else if(HostUtils.RunningOnOSX)
			{
				ProcessStartInfo startInfo = new ProcessStartInfo("ps", "aux")
				{
					RedirectStandardOutput = true
				};
				Process psaux = Process.Start(startInfo);
				string psauxres = psaux.StandardOutput.ReadToEnd();
				psaux.WaitForExit();
				string[] reslines = psauxres.Split(separator: '\n');
				reslines = (
					from l in reslines
					where l.Contains("dotnet") && l.Contains("RhinoCyclesKernelCompiler")
					select l
				).ToArray();

				string[] separator = new string[] { " " };
				foreach(var l in reslines) {
					var prts = l.Split(separator: separator, options: StringSplitOptions.RemoveEmptyEntries);
					if (prts.Length < 1) continue;
					Process.Start(fileName: "kill", arguments: $"-9 {prts[1]}");
				}
			}
			return true;
		}

		private bool ClearOutGpusFolder()
		{
			bool isSuccess = true;
			DirectoryInfo di = new DirectoryInfo(path: GpuCompilePath);
			if (di.Exists)
			{
				try
				{
					di.Delete(recursive: true);
				}
				catch (Exception)
				{
					isSuccess = false;
				}
			}
			return isSuccess;
		}

		private bool ClearOutCacheFolder()
		{
			bool isSuccess = true;
			string cachepath = "unknown";
			if(HostUtils.RunningOnWindows) {
				string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
				cachepath = Path.Combine(appdata, "NVIDIA", "ComputeCache");
			}
			else if(HostUtils.RunningOnOSX)
			{
				string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
				cachepath= Path.Combine(home, ".cache", "cycles");
			}
			DirectoryInfo di = new DirectoryInfo(path: cachepath);
			if (di.Exists)
			{
				try
				{
					di.Delete(recursive: true);
				}
				catch (Exception)
				{
					isSuccess = false;
				}
			}
			return isSuccess;
		}

		public void RecompileKernels()
		{
			if (!EnsureCompilerIsNotRunning()) return;

			if (!ClearOutGpusFolder()) return;

			if (!ClearOutCacheFolder()) return;

			InitialiseGpuKernels();

		}
		ConcurrentQueue<string> logStrings = new ConcurrentQueue<string>();
		ConcurrentQueue<string> compileStdOut = new ConcurrentQueue<string>();
		ConcurrentQueue<string> compileStdErr = new ConcurrentQueue<string>();

		Dictionary<StopwatchType, Stopwatch> stopwatches = new Dictionary<StopwatchType, Stopwatch>();

		TextWriter logTw = null;

		public void InitializeLog()
		{
			DateTime now = DateTime.Now;
			int pid = Process.GetCurrentProcess().Id;
			string logPath = Path.Combine(DataUserPath, $"RhinoCycles{now:yyyyMMddmmHHss}-{pid}.log");
			logTw = File.CreateText(logPath);
		}

		private void InitializeStopwatches()
		{
			stopwatches[StopwatchType.Core] = new Stopwatch();
			stopwatches[StopwatchType.Render] = new Stopwatch();
			stopwatches[StopwatchType.Viewport] = new Stopwatch();
			stopwatches[StopwatchType.Preview] = new Stopwatch();
		}

		public void StartLogStopwatch(string marker, StopwatchType swtype = StopwatchType.Core)
		{
			if(stopwatches.Count == 0) {
				InitializeStopwatches();
			}

			Stopwatch stopWatch = stopwatches[swtype];

			string logstr = $"\n\n==============================> {marker} ({swtype})\n\n";

			logTw.Write(logstr);

			logStrings.Enqueue(logstr);
			stopWatch.Restart();
		}

		public void PurgeOldLogs()
		{
			IEnumerable<string> entries = Directory.EnumerateFiles(DataUserPath, "RhinoCycles*.log");
			foreach(string entry in entries)
			{
				if (File.Exists(entry)) {
					TimeSpan age = DateTime.Now - File.GetLastWriteTime(entry);
					if(age.TotalDays > 3) {
						File.Delete(entry);
					}
				}
			}
		}
		public enum StopwatchType
		{
			Core,
			Render,
			Viewport,
			Preview,
		}

		private TimeSpan GetStopwatchElapsed(StopwatchType type)
		{
			if(stopwatches.TryGetValue(type, out Stopwatch sw)) {
				return sw.Elapsed;
			}
			return TimeSpan.MaxValue;
		}

		private void StopwatchRestart(StopwatchType type)
		{
			if(stopwatches.TryGetValue(type, out Stopwatch sw)) {
				sw.Restart();
			}
		}
		/// <summary>
		/// Add given string to log. Decorates with timestamp, timespan since previous log line and suffix a newline character
		/// </summary>
		/// <param name="log">String to log</param>
		public void AddLogString(string log, StopwatchType swtype = StopwatchType.Core) {
			string logstr = $"{DateTime.Now} :: {GetStopwatchElapsed(swtype)} |> {log}\n";
			try
			{
				logTw.Write(logstr);
				logTw.Flush();
			}
			catch (Exception) { } finally { }
			logStrings.Enqueue(logstr);
			StopwatchRestart(swtype);
		}

		/// <summary>
		/// Like AddLogString, but only if advanced setting RhinoCycles.VerboseLogging is set to true
		/// </summary>
		/// <param name="log">String to log</param>
		public void AddLogStringIfVerbose(string log)
		{
			if(It.AllSettings.VerboseLogging) {
				AddLogString(log);
			}
		}
		/// <summary>
		/// Get the log as a single string
		/// </summary>
		/// <returns>The log</returns>
		public string GetLog() {
			StringBuilder sb = new StringBuilder();
			foreach(string logString in logStrings)
			{
				sb.Append(logString);
			}

			return sb.ToString();
		}
	}
}
