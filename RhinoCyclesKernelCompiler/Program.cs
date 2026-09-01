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

// Enable next line for debug assistance
// #define DEBUGCOMPILER
using System;
using ccl;
using RhinoCyclesCore;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Collections.Generic;
#if ON_RUNTIME_WIN
#if DEBUG
using System.Management;
#endif
#endif
using System.Reflection;
using System.Text;

namespace RhinoCyclesKernelCompiler
{
	class Program
	{

		static bool parentProcessStillRunning()
		{
			bool stillRunning = true;
#if ON_RUNTIME_WIN
#if DEBUG
#pragma warning disable CA1416
			stillRunning = false;
			int pid = Process.GetCurrentProcess().Id;
			string query = $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {pid}";
			ManagementObjectSearcher searcher = new("root\\CIMV2", query);
			List<string> processNames = new List<string>();
			try
			{
				foreach (ManagementObject obj in searcher.Get())
				{
					uint parentPid = (uint)obj["ParentProcessId"];
					Process parentProcess = Process.GetProcessById((int)parentPid);
					processNames.Add(parentProcess.ProcessName);
					stillRunning = true;
					break;
				}
			}
			catch (Exception)
			{
				stillRunning = false;
			}
#endif
#endif

			return stillRunning;
		}


		static void HandleDevice(DeviceAndPath deviceWithHash)
		{
			Device device = deviceWithHash.Device;

			if (device.IsCpu) return;

			// Tag every line with GPU and backend so the host can group the log per GPU.
			string tag = $"[gpu {device.Id}|{DisplayName(device)}|{BackendName(device.Type)}]";
			void Log(string message) => Console.WriteLine($"{tag} {message}");

			string gpuCompileFile = deviceWithHash.Path;
			if (File.Exists(gpuCompileFile))
			{
				Log("Already compiled, nothing to do.");
				return;
			}

			string compilingSignal = $"{gpuCompileFile}.compiling";
			if (File.Exists(compilingSignal))
			{
				Log("Already compiling in another process.");
				return;
			}

			var fs = File.Create(compilingSignal);
			fs.Close();
			fs.Dispose();

			Log("Compile started.");

			string laststatus = "";
			Session session = null;
			Stopwatch sw = Stopwatch.StartNew();
			bool exceptionHappened = false;
			try
			{
				Client client = new Client();
				SessionParameters sessionParameters = new SessionParameters(device)
				{
					Experimental = false,
					Samples = 1,
					TileSize = 1,
					Threads = 0,
					ShadingSystem = ShadingSystem.SVM,
					Background = false,
					PixelSize = 1,
				};
				session = new Session(sessionParameters);

				session.Reset(1, 1, 1, 0, 0, 1, 1, 1);
				session.Start();
				while (true)
				{
					if(sw.ElapsedMilliseconds > (15 * 60 * 1000)) {
						exceptionHappened = true;
						throw new Exception("15 minute limit reached");
					}
					if(!parentProcessStillRunning()) {
						exceptionHappened = true;
						throw new Exception("Debug Rhino process no longer running");
					}
					string status = CSycles.progress_get_status(session.Id);
					string substatus = CSycles.progress_get_substatus(session.Id);
					int sample = CSycles.progress_get_sample(session.Id);
					status = $"{status}: {substatus}".Trim().TrimEnd(':');
					string lowstatus = status.ToLowerInvariant();
					bool finished = lowstatus.Contains("finished") || lowstatus.Contains("rendering done");
					if (lowstatus.Contains("error"))
					{
						exceptionHappened = true;
						throw new Exception(status);
					}
					if (sample >= 2 || finished)
					{
						break;
					}
					if (!status.Equals(laststatus))
					{
						Log(status);
						laststatus = status;
					}
					Thread.Sleep(100);
				}
				// just do one, it'll compile and then we're ready.
			}
			catch (Exception e)
			{
				exceptionHappened = true;
				Log($"Failed after {FormatElapsed(sw.Elapsed)}: {e.Message}");
				if (File.Exists(compilingSignal))
				{
					File.Delete(compilingSignal);
				}
				throw new Exception($"{DisplayName(device)} ({BackendName(device.Type)}): {e.Message}", e);
			}
			finally
			{
				if (session != null && !exceptionHappened)
				{
					session.Cancel("done");
					session.Dispose();
					File.Move(compilingSignal, gpuCompileFile, true);
					Log($"Compile completed in {FormatElapsed(sw.Elapsed)}.");
				}
				sw.Stop();
			}

		}

		static string FormatElapsed(TimeSpan elapsed) => elapsed.ToString(@"hh\:mm\:ss");

		static string Vendor(DeviceType type)
		{
			switch (type)
			{
				case DeviceType.Cuda:
				case DeviceType.Optix: return "NVIDIA";
				case DeviceType.Hip: return "AMD";
				case DeviceType.OneApi: return "Intel";
				case DeviceType.Metal: return "Apple";
				default: return "";
			}
		}

		static string BackendName(DeviceType type)
		{
			switch (type)
			{
				case DeviceType.Cuda: return "CUDA";
				case DeviceType.Optix: return "OptiX";
				case DeviceType.Hip: return "HIP";
				case DeviceType.OneApi: return "oneAPI";
				case DeviceType.Metal: return "Metal";
				default: return type.ToString();
			}
		}

		// Vendor + device name, e.g. "NVIDIA GeForce RTX 4090". The backend is reported
		// separately, so drop the "(Optix)" NiceName adds and prepend the vendor when the
		// name doesn't already start with it.
		static string DisplayName(Device device)
		{
			string name = device.NiceName.Replace(" (Optix)", "").Trim();
			string vendor = Vendor(device.Type);
			if (vendor.Length == 0 || name.StartsWith(vendor, StringComparison.OrdinalIgnoreCase)) return name;
			return $"{vendor} {name}";
		}

		static List<DeviceAndPath> ReadGpuTaskData(string gpuTaskFile, string gpuDataPath)
		{
			List<DeviceAndPath> gpuTasks = new List<DeviceAndPath>();

			var gpuTaskData = File.ReadAllLines(gpuTaskFile);
			var separator = " || ";
			foreach (var gpuTask in gpuTaskData)
			{
				var parts = gpuTask.Split(separator);
				int devid = int.Parse(parts[0]);
				string path = parts[1];
				var dev = Device.GetDevice(devid);

				if (gpuTasks.FindIndex(gpt => gpt.Path.Equals(path)) > -1) continue;

				gpuTasks.Add(new(dev, path));
			}

			return gpuTasks;
		}
		static private ProcessStartInfo SetupProcessStartInfo(string kernelPath, string compileTaskFile)
		{
			var assembly = Assembly.GetExecutingAssembly();
			string assemblyDirectory = Path.GetDirectoryName(path: assembly.Location);
			string programToRun = Path.Combine(assemblyDirectory, "RhinoCyclesKernelCompiler");
			var argumentsToProgramToRun = $"\"{kernelPath}\" \"{compileTaskFile}\"";

#if ON_RUNTIME_WIN
			programToRun += ".exe";
#endif

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

#if ON_RUNTIME_WIN
			startInfo.CreateNoWindow = true;
#endif

			return startInfo;
		}

		static int Main(string[] args)
		{
			if (args.Length < 2)
			{
				Console.WriteLine("Need kernel and user data paths of RhinoCycles");
				return -1;
			}
			bool hacky = false;
#if ON_RUNTIME_WIN
#if DEBUGCOMPILER
			System.Diagnostics.Debugger.Launch();
#endif
#if DEBUG
			hacky = args.Length > 2;
#endif
#endif
			int result = 0;
			if (hacky)
			{
				ProcessStartInfo startInfo = SetupProcessStartInfo(args[0], args[1]);
				Process cp = Process.Start(startInfo);
				while(!cp.HasExited)
				{
					if(!parentProcessStillRunning())
					{
						cp.Kill();
					}
					Thread.Sleep(20);
				}
				Console.Write(cp.StandardOutput.ReadToEnd());
				Console.Error.Write(cp.StandardError.ReadToEnd());

			}
			else
			{
				result = RunCompile(args);
			}

			return result;

		} /* end of Main */

		static float[] DummyTable = { 1.0f };

		private static void SetupTables()
		{
			unsafe {
				fixed (float* ptr = DummyTable) {
					CSycles.set_rhino_aaltonen_noise_table((IntPtr)ptr, (uint)DummyTable.Length);
					CSycles.set_rhino_impulse_noise_table((IntPtr)ptr, (uint)DummyTable.Length);
					CSycles.set_rhino_perlin_noise_table((IntPtr)ptr, (uint)DummyTable.Length);
					CSycles.set_rhino_vc_noise_table((IntPtr)ptr, (uint)DummyTable.Length);
				}

			}
		}

		private static int RunCompile(string[] args)
		{
			int result = 0;

			string kernelPath = args[0];
			string compileTaskFile = args[1];
			string gpuDataPath = new DirectoryInfo(Path.GetDirectoryName(compileTaskFile)).FullName;
			string dataUserPath = new DirectoryInfo(Path.GetDirectoryName(compileTaskFile)).Parent.FullName;

			Console.WriteLine($"Kernel path: {kernelPath}");
			Console.WriteLine($"Data path  : {dataUserPath}");
			CSycles.path_init(kernelPath, dataUserPath);
			CSycles.initialise(DeviceTypeMask.All);

			DeviceTypeMask failed = CSycles.failed_gpus_mask();
			foreach (DeviceType t in Enum.GetValues(typeof(DeviceType)))
			{
				var bit = (DeviceTypeMask)(1u << (int)t);
				if ((failed & bit) == 0) continue;
				Console.Error.WriteLine($"{Vendor(t)} {BackendName(t)} failed to initialise: {CSycles.gpu_init_error(t)}".TrimStart());
			}

			SetupTables();
			Console.WriteLine("Cycles initialized.");

			var gpuTasks = ReadGpuTaskData(compileTaskFile, gpuDataPath);

			try
			{
				Parallel.ForEach(gpuTasks, HandleDevice);
			}
			catch (Exception ex)
			{
				// stderr, so the host log shows these grouped at the end
				Console.Error.WriteLine(ex.ToString());
				result = -13;
			}
			finally
			{
				try
				{
					foreach (var gpuTask in gpuTasks)
					{
						var compilingFile = $"{gpuTask.Path}.compiling";
						if (File.Exists(compilingFile))
						{
							File.Delete(compilingFile);
						}
					}
					File.Delete(compileTaskFile);
				}
				finally { }
			}

			return result;
		}
	}
}
