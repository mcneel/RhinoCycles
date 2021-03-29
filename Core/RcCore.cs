/**
Copyright 2014-2017 Robert McNeel and Associates

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

		ConcurrentDictionary<uint, Session> sessions = new ConcurrentDictionary<uint, Session>();
		/// <summary>
		/// Shut down Cycles on all levels. Wait for all active session to complete.
		/// </summary>
		public void Shutdown() {
			int count;
			int timer = 0;
			if(checkOpenClCompilationCompletedThread!=null) {
				stopCheckingForOpenClCompileFinished = true;
				try
				{
					checkOpenClCompilationCompletedThread.Join();
				}
				finally {
					checkOpenClCompilationCompletedThread = null;
				}
			}
			if(openClCompilerThread!=null)
			{
				try
				{
					openClCompilerThread.Join();
				}
				finally {
					openClCompilerThread = null;
				}
			}
			while((count = sessions.Count) > 0 ) {
				if(timer%50==0)
					RhinoApp.OutputDebugString($"Number of sessions we wait for {count}\n");
				Thread.Sleep(10);
				timer++;
			}
			RhinoApp.OutputDebugString($"All sessions cleaned up\n");
			openClDevicesReadiness.Clear();
			CSycles.shutdown();
		}

		private readonly object sessionsLock = new object();
		/// <summary>
		/// Create a ccl.Session and register with central system so we can later ensure
		/// we wait on all sessions to fully complete before shutting down CSycles.
		///
		/// Sessions created with this function have to be released/destroyed using
		/// the function ReleaseSession
		/// </summary>
		/// <param name="client"></param>
		/// <param name="sessionParameters"></param>
		/// <returns></returns>
		public Session CreateSession(Client client, SessionParameters sessionParameters) {
			lock (sessionsLock)
			{
				var session = new Session(client, sessionParameters);

				if (sessions.ContainsKey(session.Id))
				{
					RhinoApp.OutputDebugString($"Session {session.Id} already exists\n");
				}

				sessions[session.Id] = session;
				RhinoApp.OutputDebugString($"Created session {session.Id}.\n");

				return session;
			}
		}

		/// <summary>
		/// Release and destroy session created by CreateSession.
		/// </summary>
		/// <param name="session"></param>
		public void ReleaseSession(Session session) {
			lock (sessionsLock)
			{
				if (sessions.ContainsKey(session.Id))
				{
					RhinoApp.OutputDebugString($"Releasing session {session.Id}.\n");
					Session tempSession;
					while (!sessions.TryRemove(session.Id, out tempSession))
					{
						Thread.Sleep(10);
					}
					if (tempSession != null)
					{
						tempSession.EndRun();
						tempSession.Destroy();
					}
					RhinoApp.OutputDebugString($"Session {session.Id} released.\n");

				}
			}
		}

		/// <summary>
		/// Check if given device is ready to be used for rendering. If it is the
		/// returned tuple will have isDeviceReady set to true and actualDevice is
		/// the device checked.
		///
		/// If it is not ready isDeviceReady will be set to false and the
		/// actualDevice will be set to the default device (CPU).
		/// </summary>
		/// <param name="device">Device to check for readiness</param>
		/// <returns>Tuple with isDeviceReady set to true and device in actualDevice
		/// if device is ready.
		///
		/// Otherwise isDeviceReady will be false and actualDevice Device.Default.
		/// </returns>
		public (bool isDeviceReady, Device actualDevice) IsDeviceReady(Device device) {
			if(!device.IsOpenCl) return (true, device);
			lock(accessOpenClDevicesReadiness) {
				var devReadiness = openClDevicesReadiness.Find(d => d.Device.Equals(device));
				if(devReadiness.IsReady) return (true, device);
			}
			return (false, Device.Default);

		}

		/// <summary>
		/// The main OpenCL compiler thread.
		/// </summary>
		Thread openClCompilerThread = null;
		/// <summary>
		/// Thread that will check if compiles have finished.
		/// </summary>
		Thread checkOpenClCompilationCompletedThread = null;
		/// <summary>
		/// Start the process to initialize OpenCL for Cycles in a separate thread.
		/// Before starting the process create a list of all OpenCL devices and
		/// initialize their readiness state to false.
		/// </summary>
		public void InitialiseOpenCl()
		{
			lock (accessOpenClDevicesReadiness)
			{
				foreach (var device in ccl.Device.Devices)
				{
					if (device.IsOpenCl)
					{
						openClDevicesReadiness.Add((Device: device, IsReady: false));
					}
				}
			}
			openClCompilerThread = new Thread(StartCompileOpenCl);
			openClCompilerThread.Start();

			checkOpenClCompilationCompletedThread = new Thread(CheckOpenClCompileFinished);
			checkOpenClCompilationCompletedThread.Start();
		}

		/// <summary>
		/// lock object for access to openClDevicesReadiness
		/// </summary>
		private readonly object accessOpenClDevicesReadiness = new object();
		/// <summary>
		/// List holding OpenCL devices and their readiness state.
		/// </summary>
		List<(ccl.Device Device, bool IsReady)> openClDevicesReadiness = new List<(ccl.Device, bool)>();
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

		bool stopCheckingForOpenClCompileFinished = false;
		public void CheckOpenClCompileFinished()
		{
			if(openClDevicesReadiness.Count == 0) return;

			do
			{
				Thread.Sleep(1000);
				for (int idx = 0; idx < deviceFileNames.Count; idx++)
				{
					var deviceFileName = deviceFileNames[idx];
					(Device device, bool isReady) = openClDevicesReadiness[idx];
					if (File.Exists(deviceFileName) && !isReady)
					{
						lock (accessOpenClDevicesReadiness)
						{
							openClDevicesReadiness[idx] = (device, true);
						}
					}
				}
				bool ready = true;
				lock (accessOpenClDevicesReadiness)
				{
					ready = openClDevicesReadiness.Aggregate(true, (state, next) => state && next.IsReady);
				}
				if (ready)
				{
					break;
				}
			}
			while (!stopCheckingForOpenClCompileFinished);
		}

		/// <summary>
		/// Compile OpenCL if necessary
		/// </summary>
		public void StartCompileOpenCl()
		{
			// no opencl devices to compile for
			if (openClDevicesReadiness.Count == 0) return;

			// make a list of just the devices, so we can update the openClDevicesReadiness
			// list when needed
			List<Device> devicesToCheck;
			lock (accessOpenClDevicesReadiness)
			{
				devicesToCheck = (from d in openClDevicesReadiness select d.Device).ToList();
			}

			// Compute the SHA256 hash for each device on the concatenated string
			// containing device nice name, device driver and Rhino version.
			deviceFileNames = new List<string>(devicesToCheck.Count);
			foreach(ccl.Device device in devicesToCheck) {
				foreach(var gpudev in availableGpuDevices) {
					if(gpudev.DeviceName.Contains(device.NiceName) || device.NiceName.Contains(gpudev.DeviceName)) {
						using(SHA256 sha = SHA256Managed.Create()) {
							var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"{device.NiceName}{gpudev.DriverDate}{RhinoApp.Version}"));
							deviceFileNames.Add(Path.Combine(DataUserPath, string.Concat(hash.Select(b => b.ToString("x2")))));
							break;
						}
					}
				}
			}

			// bail if we don't have any device file names
			if(deviceFileNames.Count == 0) return;

			// bail if device filenames and devices counts are different
			if(deviceFileNames.Count != devicesToCheck.Count) return;

			// now check if we have compiled for all available OpenCL devices
			bool needAnyCompile = (from devFile in deviceFileNames select !File.Exists(devFile)).Aggregate(false, (accum, res) => accum | res);

			// early bail if no compile needed
			if (!needAnyCompile)
			{
				for (int idx = 0; idx < openClDevicesReadiness.Count; idx++)
				{
					openClDevicesReadiness[idx] = (openClDevicesReadiness[idx].Device, true);
				}
				return;
			}

			// wait until Rhino has been initialized fully. We can't open documents
			// before that, or we risk making Rhino go crazy.
			while (!RcCore.It.AppInitialised)
			{
				Thread.Sleep(10);
			}

			for (int idx = 0; idx < devicesToCheck.Count; idx++)
			{
				var renderDevice = devicesToCheck[idx];
				var compiledFileName = deviceFileNames[idx];
				var compilingLock = $"{compiledFileName}.compiling";

				// no need to start a compile when we have this file already
				// just mark device as ready
				if (File.Exists(compiledFileName))
				{
					lock (accessOpenClDevicesReadiness)
					{
						openClDevicesReadiness[idx] = (renderDevice, true);
					}
					continue;
				}
				else if(File.Exists(compilingLock)) {
					// compile already in progress, check next device instead.
					continue;
				}
				else
				{
					// Tell any other rhino process that we've started compiling for this
					// device.
					File.WriteAllText(compilingLock, DateTime.Now.ToLongTimeString());
					// Start a new process to compile the OpenCL kernels for the current
					// device.
					// The process will use RhinoCyclesOpenClCompiler to essentially start
					// the simplest possible Cycles session. This in turn will build the
					// OpenCL kernels.
					using(Process currentCompilerProcess = new Process()) {
					currentCompilerProcess.StartInfo.FileName = Path.Combine(PluginPath, "RhinoCyclesOpenClCompiler");
					currentCompilerProcess.StartInfo.WorkingDirectory = DataUserPath;
					currentCompilerProcess.StartInfo.CreateNoWindow = true;
						using (NamedPipeServerStream pipeServer =
									new NamedPipeServerStream("rhino.opencl.compiler", PipeDirection.InOut))
						{
							currentCompilerProcess.StartInfo.UseShellExecute = true;
							currentCompilerProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
							currentCompilerProcess.Start();

							// wait for client to connect
							pipeServer.WaitForConnection();


							try
							{
								using (StreamWriter sw = new StreamWriter(pipeServer))
								{
									sw.AutoFlush = true;
									// Send a 'sync message' and wait for client to receive it.
									sw.WriteLine("SYNC");
									pipeServer.WaitForPipeDrain();
									// write compile lock filename
									sw.WriteLine(compilingLock);
									pipeServer.WaitForPipeDrain();
									// write compiled filename
									sw.WriteLine(compiledFileName);
									pipeServer.WaitForPipeDrain();
									// write kernel path
									sw.WriteLine(RcCore.It.KernelPath);
									pipeServer.WaitForPipeDrain();
									// write data path
									sw.WriteLine(RcCore.It.DataUserPath);
									pipeServer.WaitForPipeDrain();
									// now write device ID
									sw.WriteLine($"{renderDevice.Id}");
									pipeServer.WaitForPipeDrain();
									// and device NiceName
									sw.WriteLine($"{renderDevice.NiceName}");
									pipeServer.WaitForPipeDrain();
								}
							}
							catch (IOException)
							{
								continue;
							}

							bool still_compiling = true;
							do
							{
								Thread.Sleep(500);
								still_compiling = Directory.EnumerateFiles(RcCore.It.DataUserPath, "*.compile").Any();
							}
							while (!stopCheckingForOpenClCompileFinished && still_compiling);
						} /* end of using named pipe server stream */
					} /* end of using process */
				} /* end of else -> we need to compile code block */
			}
		} /* end of StartCompileOpenCl() */
	}
}
