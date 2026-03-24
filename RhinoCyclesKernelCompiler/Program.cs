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
using System.Runtime.InteropServices;
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
		static void MaybeFakeKernelCrash(string stage)
		{
			string configuredStage = Environment.GetEnvironmentVariable("RHINO_FAKE_KERNEL_CRASH_STAGE");
			if (!string.Equals(configuredStage, stage, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			Console.Error.WriteLine($"kernel-compiler: intentionally simulating crash at stage={stage}");
			throw new System.Runtime.InteropServices.SEHException($"Intentional test failure at stage={stage}");
		}
		static string CreateTraceId(Device device)
		{
			return $"{Process.GetCurrentProcess().Id}:{Environment.CurrentManagedThreadId}:{device.Id}:{Stopwatch.GetTimestamp()}";
		}
		static string FormatHandle(IntPtr handle)
		{
			return handle == IntPtr.Zero ? "0x0" : "0x" + handle.ToInt64().ToString("X");
		}
		static void LogTrace(string traceId, string message)
		{
			Console.WriteLine($"[kernel-compiler:{traceId}] {message}");
		}
		static void LogException(string traceId, Exception exception)
		{
			int depth = 0;
			for (Exception current = exception; current != null; current = current.InnerException)
			{
				LogTrace(traceId, $"Exception[{depth}] {current.GetType().FullName}: {current.Message}");
				if (!string.IsNullOrWhiteSpace(current.StackTrace))
				{
					LogTrace(traceId, $"Stack[{depth}] {current.StackTrace}");
				}
				depth++;
			}
		}
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
			string traceId = CreateTraceId(device);
			Console.WriteLine($"Start compiling {id}");
			LogTrace(traceId, $"HandleDevice enter device='{device.NiceName}' compileFile='{gpuCompileFile}' signalFile='{compilingSignal}'");
			string sha = device.NiceNameSha;
			string laststatus = "";
			Session session = null;
			Stopwatch sw = Stopwatch.StartNew();
			bool exceptionHappened = false;
			try
			{
				Client client = new Client();
				LogTrace(traceId, "Creating SessionParameters");
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
				LogTrace(traceId, $"SessionParameters created id={FormatHandle(sessionParameters.Id)}");
				LogTrace(traceId, "Creating Session");
				MaybeFakeKernelCrash("managed_before_session_create");
				session = new Session(sessionParameters);
				LogTrace(traceId, $"Session created id={FormatHandle(session.Id)}");
				//session.AddPass(PassType.Combined);
				session.Reset(1, 1, 1, 0, 0, 1, 1, 1);
				LogTrace(traceId, "Starting session");
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
			catch (SEHException e)
			{
				exceptionHappened = true;
				LogTrace(traceId, $"SEHException while compiling {id}");
				LogException(traceId, e);
				if (File.Exists(compilingSignal))
				{
					File.Delete(compilingSignal);
				}
				throw new Exception($"Exception while compiling for {id}", e);
			}
			catch (Exception e)
			{
				exceptionHappened = true;
				Console.WriteLine($"Failed for {id}");
				Console.WriteLine($"\t{e}");
				LogException(traceId, e);
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
					LogTrace(traceId, $"Cancelling and disposing session id={FormatHandle(session.Id)}");
					session.Cancel("done");
					session.Dispose();
					FinalizeCompileMarker(traceId, compilingSignal, gpuCompileFile);
				}
				sw.Stop();
				if (exceptionHappened)
				{
					Console.WriteLine($"Worker finished with failure {id}");
				}
				else
				{
					Console.WriteLine($"Completed {id}");
				}
				Console.WriteLine($"   time: {sw.Elapsed}");
			}
		}
		static void FinalizeCompileMarker(string traceId, string compilingSignal, string gpuCompileFile)
		{
			try
			{
				if (File.Exists(compilingSignal))
				{
					File.Move(compilingSignal, gpuCompileFile, true);
					return;
				}
			}
			catch (FileNotFoundException)
			{
				LogTrace(traceId, $"Compile marker disappeared during finalize outputFile='{gpuCompileFile}' signalFile='{compilingSignal}'");
			}
			if (File.Exists(gpuCompileFile))
			{
				LogTrace(traceId, $"Compile marker already finalized outputFile='{gpuCompileFile}'");
				return;
			}
			LogTrace(traceId, $"Compile marker missing before finalize; recreating outputFile='{gpuCompileFile}' signalFile='{compilingSignal}'");
			using (FileStream stream = File.Open(gpuCompileFile, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read)) { }
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
				Device dev = null;
				if (parts.Length >= 4 && Enum.TryParse(parts[2], ignoreCase: true, out DeviceType taskDeviceType))
				{
					string taskDeviceName = parts[3];
					foreach (var candidate in Device.Devices)
					{
						if (candidate.Type == taskDeviceType && string.Equals(candidate.Name, taskDeviceName, StringComparison.Ordinal))
						{
							dev = candidate;
							break;
						}
					}
				}
				if (dev == null)
				{
					dev = Device.GetDevice(devid);
				}
				if (gpuTasks.FindIndex(gpt => gpt.Path.Equals(path)) > -1) continue;
				gpuTasks.Add(new(dev, path));
			}
			return gpuTasks;
		}
		static string GetRequestedDeviceTypeArgument(string[] args)
		{
			for (int idx = 2; idx < args.Length; idx++)
			{
				if (!string.Equals(args[idx], "inception", StringComparison.OrdinalIgnoreCase))
				{
					return args[idx];
				}
			}

			return null;
		}

		static DeviceTypeMask GetInitialiseMask(string requestedDeviceTypeArgument)
		{
			if (!Enum.TryParse(requestedDeviceTypeArgument, ignoreCase: true, out DeviceType deviceType))
			{
				return DeviceTypeMask.All;
			}

			switch (deviceType)
			{
				case DeviceType.Cuda:
					return DeviceTypeMask.CUDA;
				case DeviceType.Optix:
					return DeviceTypeMask.OPTIX | DeviceTypeMask.CUDA;
				case DeviceType.Hip:
					return DeviceTypeMask.HIP;
				case DeviceType.Metal:
					return DeviceTypeMask.METAL;
				case DeviceType.OneApi:
					return DeviceTypeMask.ONEAPI;
				default:
					return DeviceTypeMask.All;
			}
		}

		static string FormatInitialiseMask(DeviceTypeMask initialiseMask)
		{
			if (initialiseMask == (DeviceTypeMask.OPTIX | DeviceTypeMask.CUDA))
			{
				return "OPTIX|CUDA";
			}
			if (initialiseMask == DeviceTypeMask.CUDA)
			{
				return "CUDA";
			}
			if (initialiseMask == DeviceTypeMask.HIP)
			{
				return "HIP";
			}
			if (initialiseMask == DeviceTypeMask.METAL)
			{
				return "METAL";
			}
			if (initialiseMask == DeviceTypeMask.ONEAPI)
			{
				return "ONEAPI";
			}
			if (initialiseMask == DeviceTypeMask.All)
			{
				return "All";
			}

			return initialiseMask.ToString();
		}

		static private ProcessStartInfo SetupProcessStartInfo(string kernelPath, string compileTaskFile, string requestedDeviceTypeArgument = null)
		{
			var assembly = Assembly.GetExecutingAssembly();
			string assemblyDirectory = Path.GetDirectoryName(path: assembly.Location);
			string programToRun = Path.Combine(assemblyDirectory, "RhinoCyclesKernelCompiler");
			var argumentsToProgramToRun = $"\"{kernelPath}\" \"{compileTaskFile}\"";
			if (!string.IsNullOrWhiteSpace(requestedDeviceTypeArgument))
			{
				argumentsToProgramToRun += $" {requestedDeviceTypeArgument}";
			}
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
			hacky = Array.Exists(args, arg => string.Equals(arg, "inception", StringComparison.OrdinalIgnoreCase));
#endif
#endif
			int result = 0;
			if (hacky)
			{
				ProcessStartInfo startInfo = SetupProcessStartInfo(args[0], args[1], GetRequestedDeviceTypeArgument(args));
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
			string requestedDeviceTypeArgument = GetRequestedDeviceTypeArgument(args);
			string gpuDataPath = new DirectoryInfo(Path.GetDirectoryName(compileTaskFile)).FullName;
			string dataUserPath = new DirectoryInfo(Path.GetDirectoryName(compileTaskFile)).Parent.FullName;
			Console.WriteLine("Initializing Cycles");
			Console.WriteLine($"\tKernel path: {kernelPath}");
			Console.WriteLine($"\tData path: {dataUserPath}");
			CSycles.path_init(kernelPath, dataUserPath);
			DeviceTypeMask initialiseMask = GetInitialiseMask(requestedDeviceTypeArgument);
			LogTrace("main", $"Initialising Cycles with mask={FormatInitialiseMask(initialiseMask)}");
			CSycles.initialise(initialiseMask);
			Console.WriteLine("Setup tables for Cycles");
			SetupTables();
			Console.WriteLine("Cycles initialized");
			var gpuTasks = ReadGpuTaskData(compileTaskFile, gpuDataPath);
			LogTrace("main", $"Launching kernel compilation for {gpuTasks.Count} GPU task(s)");
			try
			{
				Parallel.ForEach(gpuTasks, HandleDevice);
			}
			catch (AggregateException ex)
			{
				LogTrace("main", "AggregateException while compiling GPU tasks");
				foreach (var inner in ex.Flatten().InnerExceptions)
				{
					LogException("main", inner);
				}
				result = -13;
			}
			catch (Exception ex)
			{
				LogTrace("main", "Unhandled exception while compiling GPU tasks");
				LogException("main", ex);
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
