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

			string gpuCompileFile = deviceWithHash.Path;
			if (File.Exists(gpuCompileFile))
			{
				Console.WriteLine($"{device.NiceName} already completed");
				return;
			}

			string compilingSignal = $"{gpuCompileFile}.compiling";
			if (File.Exists(compilingSignal))
			{
				Console.WriteLine($"{device.NiceName} already compiling");
				return;
			}

			var fs = File.Create(compilingSignal);
			fs.Close();
			fs.Dispose();

			string id = $"{device.Id}: {device.NiceName}";

			Console.WriteLine($"Start compiling {id}\n");

			string sha = device.NiceNameSha;
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

				//session.AddPass(PassType.Combined);
				session.Reset(1, 1, 1, 0, 0, 1, 1, 1);
				session.Start();
				while (true)
				{
					if(sw.ElapsedMilliseconds > (15 * 60 * 1000)) {
						exceptionHappened = true;
						throw new Exception("30 minute limit reached");
					}
					if(!parentProcessStillRunning()) {
						exceptionHappened = true;
						throw new Exception("Debug Rhino process no longer running");
					}
					string status = CSycles.progress_get_status(session.Id);
					string substatus = CSycles.progress_get_substatus(session.Id);
					int sample = CSycles.progress_get_sample(session.Id);
					status = $"{id} ({sample}) | {status}: {substatus}";
					string lowstatus = status.ToLowerInvariant();
					bool finished = lowstatus.Contains("finished") || lowstatus.Contains("rendering done");
					if (lowstatus.Contains("error"))
					{
						Console.WriteLine(status);
						exceptionHappened = true;
						throw new Exception($"Error in session ({id}) -> {status}.");
					}
					if (sample >= 2 || finished)
					{
						break;
					}
					if (!status.Equals(laststatus))
					{
						Console.WriteLine(status);
						laststatus = status;
					}
					Thread.Sleep(100);
				}
				// just do one, it'll compile and then we're ready.
			}
			catch (Exception e)
			{
				exceptionHappened = true;
				Console.WriteLine($"Failed for {id}\n\t{e}");
				if (File.Exists(compilingSignal))
				{
					File.Delete(compilingSignal);
				}
				throw new Exception($"Exception while compiling for {id}", e);

			}
			finally
			{
				if (session != null && !exceptionHappened)
				{
					session.Cancel("done");
					session.Dispose();
					File.Move(compilingSignal, gpuCompileFile, true);
				}
				sw.Stop();
				Console.WriteLine($"Completed {id}");
				Console.WriteLine($"   time: {sw.Elapsed}");
			}

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

			CSycles.path_init(kernelPath, dataUserPath);
			CSycles.initialise(DeviceTypeMask.All);
			SetupTables();

			var gpuTasks = ReadGpuTaskData(compileTaskFile, gpuDataPath);

			try
			{
				Parallel.ForEach(gpuTasks, HandleDevice);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
				Console.WriteLine(ex.StackTrace);
				Console.WriteLine(ex.InnerException.ToString());
				Console.WriteLine(ex.InnerException.StackTrace);
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
