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

using ccl;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using RhinoCyclesCore;
using RhinoCyclesCore.Settings;
using RhinoCyclesCore.RenderEngines;
using System;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Reflection;
using System.Drawing.Printing;
using System.Text;

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
			var sp = remainder.Split(System.IO.Path.DirectorySeparatorChar);
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
				while(!active_sessions.TryRemove(session.Id, out _))
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
					if (File.Exists(device.Path) && !isReady)
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
			if(!Directory.Exists(GpuCompilePath))
			{
				Directory.CreateDirectory(GpuCompilePath);
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
						var hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"{device.NiceName}{gpudev.DriverDate}{RhinoApp.Version}"));

						info = Path.Combine(GpuCompilePath, string.Concat(hash.Select(b => b.ToString("x2"))));
						break;
					}
				}
			}
			if(string.IsNullOrEmpty(info))
			{
					using (SHA256 sha = SHA256.Create())
					{
						var hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"{device.NiceName}{RhinoApp.Version}"));

						info = Path.Combine(GpuCompilePath, string.Concat(hash.Select(b => b.ToString("x2"))));
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
				var hash = gpuTaskFileSha.ComputeHash(Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString()));
				gpuTaskFileName = Path.Combine(GpuCompilePath, string.Concat(hash.Select(b => b.ToString("x2"))) + ".task");
			}

			using (TextWriter tw = new StreamWriter(gpuTaskFileName, false, Encoding.UTF8))
			{
				foreach (Device device in Device.Devices)
				{
					var info = GenerateGpuDeviceInfo(device);
					tw.WriteLine($"{device.Id} || {info}");
				}
				tw.Close();
			}

			return gpuTaskFileName;
		}

		/// <summary>
		/// Compile OpenCL if necessary
		/// </summary>
		public void StartCompileGpuKernels()
		{
			EnsureGpuCompilePath();
			var compileTaskFile = WriteGpuDevicesFile();

			var assembly = Assembly.GetExecutingAssembly();
			var compiler = Path.Combine(Path.GetDirectoryName(assembly.Location), "RhinoCyclesKernelCompiler");
#if ON_RUNTIME_WIN
			compiler += ".exe";
#endif
			var exists = File.Exists(compiler);
			Console.WriteLine(exists);
			var args = $"\"{KernelPath}\" \"{compileTaskFile}\"";
			ProcessStartInfo startInfo = new ProcessStartInfo(compiler, args)
			{
				//FileName = compiler,
				//Arguments = args,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardErrorEncoding = System.Text.Encoding.UTF8,
				StandardOutputEncoding = System.Text.Encoding.UTF8,
			};
			var dylib_path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(assembly.Location), "..", "..", "..", ".."));
			startInfo.EnvironmentVariables.Add("DYLD_FALLBACK_LIBRARY_PATH", $"{dylib_path}");
			startInfo.Environment.Add("DYLD_FALLBACK_LIBRARY_PATH", $"{dylib_path}");

			var process = Process.Start(startInfo);

			var stdout = process.StandardOutput.ReadToEnd();
			//var stderr = process.StandardError.ReadToEnd();

			Console.WriteLine(stdout);
			//Console.WriteLine(stderr);

			process.WaitForExit();

		} /* end of StartCompileGpuKernel() */
	}
}
