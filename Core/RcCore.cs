/**
Copyright 2014-2023 Robert McNeel and Associates

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
using RhinoCyclesCore.Settings;
using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Reflection;
using System.Text;
using Rhino.UI;
using Rhino.Runtime;

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
					RhinoApp.OutputDebugString($"Number of sessions we wait for {count}\n");
				Thread.Sleep(10);
				timer++;
			}
			RhinoApp.OutputDebugString($"All sessions cleaned up\n");
			gpuDevicesReadiness.Clear();
			CSycles.shutdown();
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
			RhinoApp.OutputDebugString($"Created session {session.Id}.\n");

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
			lock(accessGpuKernelDevicesReadiness) {
				var devReadiness = gpuDevicesReadiness.Find(
					d =>
						d.DeviceAndPath.Device.Equals(device)
				);
				if(devReadiness.IsReady) return (true, device);
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
					if (File.Exists(path: device.Path) && !isReady)
					{
						lock (accessGpuKernelDevicesReadiness)
						{
							gpuDevicesReadiness[idx] = (device, true);
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

		private void EnsureGpuCompilePath()
		{
			if(!Directory.Exists(path: GpuCompilePath))
			{
				Directory.CreateDirectory(path: GpuCompilePath);
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

			/// <summary>
			/// Write GPU data to file to use in RhinoCyclesKernelCompile
			/// </summary>
			/// <returns>Return path to file</returns>
			private string WriteGpuDevicesFile()
		{
			string gpuTaskFileName;
			using(SHA256 gpuTaskFileSha = SHA256.Create())
			{
				var hash = gpuTaskFileSha.ComputeHash(
					buffer: Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString())
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
				foreach (Device device in Device.Devices)
				{
					var info = GenerateGpuDeviceInfo(device: device);
					tw.WriteLine($"{device.Id} || {info}");
				}
				tw.Close();
			}

			return gpuTaskFileName;
		}

		public string CompileLogStdOut { get; set; } = LOC.STR("Not started");
		public string CompileLogStdErr { get; set; } = "";

		public bool CompileProcessFinished { get; set; } = false;
		public bool CompileProcessError { get; set; } = false;

		public DateTime CompileStartTime { get; set; } = DateTime.MinValue;
		public DateTime CompileEndTime { get; set; } = DateTime.MinValue;

		/// <summary>
		/// Set up the ProcessStartInfo instance used for
		/// running the kernel compiler in a separate process
		/// </summary>
		/// <param name="compileTaskFile"></param>
		/// <returns></returns>
		private ProcessStartInfo SetupProcessStartInfo(string compileTaskFile)
		{
			var assembly = Assembly.GetExecutingAssembly();
			string assemblyDirectory = Path.GetDirectoryName(path: assembly.Location);
			string programToRun = Path.Combine(assemblyDirectory, "RhinoCyclesKernelCompiler");
			var argumentsToProgramToRun = $"\"{KernelPath}\" \"{compileTaskFile}\"";

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
				startInfo.EnvironmentVariables.Add("DYLD_FALLBACK_LIBRARY_PATH", $"{dylib_path}");
				startInfo.Environment.Add("DYLD_FALLBACK_LIBRARY_PATH", $"{dylib_path}");
			}

			return startInfo;
		}

		/// <summary>
		/// Compile GPU kernels if necessary
		/// </summary>
		public void StartCompileGpuKernels()
		{
			EnsureGpuCompilePath();
			var compileTaskFile = WriteGpuDevicesFile();
			CompileProcessFinished = false;
			CompileProcessError = false;

			CompileLogStdOut = LOC.STR("Compile started, waiting for results...") + "\n";
			CompileLogStdErr = LOC.STR("No errors.");
			CompileStartTime = DateTime.Now;
			CompileEndTime = DateTime.MinValue;

			try {
				ProcessStartInfo startInfo = SetupProcessStartInfo(compileTaskFile);

				var process = Process.Start(startInfo);
				CompileLogStdOut = process.StandardOutput.ReadToEnd();

				process.WaitForExit();
				CompileProcessError = process.ExitCode != 0;
				if(CompileProcessError)
				{
					string compile_failed = LOC.STR("Compile failed");
					string compile_error_code = LOC.STR("Error code");
					CompileLogStdOut = $"{compile_failed} {CompileLogStdOut}";
					CompileLogStdErr = $"{compile_error_code}: {process.ExitCode}\n\n{process.StandardError.ReadToEnd()}";
				}
			} catch (Exception processException)
			{
				CompileLogStdErr = $"{processException}\n\n{processException.StackTrace}";
				CompileProcessError = true;
			}

			CompileProcessFinished = true;
			CompileEndTime = DateTime.Now;

		} /* end of StartCompileGpuKernel() */

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
	}
}
