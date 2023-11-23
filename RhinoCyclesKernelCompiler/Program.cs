// Enable next line for debug assistance
//#define DEBUGCOMPILER
using System;
using ccl;
using RhinoCyclesCore;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace RhinoCyclesOpenClCompiler
{
	class Program
	{

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

			File.Create(compilingSignal);

			var id = $"{device.Id}: {device.NiceName}";

			Console.WriteLine($"Start compiling {id}");

			var sha = device.NiceNameSha;
			var laststatus = "";
			Session session = null;
			Stopwatch sw = Stopwatch.StartNew();
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

				session.AddPass(PassType.Combined);
				session.Reset(1, 1, 1, 0, 0, 1, 1);
				Console.WriteLine($"Started for {id}");
				session.Start();
				while (true)
				{
					string status = CSycles.progress_get_status(session.Id);
					string substatus = CSycles.progress_get_substatus(session.Id);
					int sample = CSycles.progress_get_sample(session.Id);
					status = $"{id} [{sha}] ({sample}) | {status}: {substatus}";
					string lowstatus = status.ToLowerInvariant();
					bool finished = lowstatus.Contains("finished") || lowstatus.Contains("rendering done");
					if (lowstatus.Contains("error"))
					{
						Console.WriteLine(status);
						throw new Exception($"Error in session -> {status}.");
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
				Console.WriteLine($"Failed for {id}\n\t{e}");

				/*if (File.Exists(compilingLock))
				{
					File.Delete(compilingLock);
				}
				return -1;*/
			}
			finally
			{
				Console.WriteLine($"Cleaning up for {id}");
				if (session != null)
				{
					session.Cancel("done");
					session.Dispose();
				}
				sw.Stop();
				Console.WriteLine($"Completed {id}");
				Console.WriteLine($"   time: {sw.Elapsed}");
				File.Move(compilingSignal, gpuCompileFile);
			}

		}

		static List<DeviceAndPath> ReadGpuTaskData(string gpuTaskFile, string gpuDataPath)
		{
			List<DeviceAndPath> gpuTasks = new List<DeviceAndPath>();

			var gpuTaskData = File.ReadAllLines(gpuTaskFile);
			foreach (var gpuTask in gpuTaskData)
			{
				Console.WriteLine($"### -> {gpuTask}");
				var parts = gpuTask.Split(" || ");
				Console.WriteLine($"@@@ -> {parts[0]} -- {parts[1]}");
				int devid = int.Parse(parts[0]);
				string path = parts[1];
				var dev = Device.GetDevice(devid);

				if (gpuTasks.FindIndex(gpt => gpt.Path.Equals(path)) > -1) continue;

				Console.WriteLine($"Adding device {dev.NiceName} with path {path}");

				gpuTasks.Add(new(dev, path));
			}

			return gpuTasks;
		}
		static int Main(string[] args)
		{
#if DEBUGCOMPILER
			System.Diagnostics.Debugger.Launch();
#endif
			foreach (var arg in args)
			{
				Console.WriteLine($"> {arg}");
			}
			if (args.Length != 2)
			{
				Console.WriteLine("Need kernel and user data paths of RhinoCycles");
				return -1;
			}

			var kernelPath = args[0];
			var compileTaskFile = args[1];
			var gpuDataPath = new DirectoryInfo(Path.GetDirectoryName(compileTaskFile)).FullName;
			var dataUserPath = new DirectoryInfo(Path.GetDirectoryName(compileTaskFile)).Parent.FullName;

			CSycles.path_init(kernelPath, dataUserPath);
			CSycles.initialise(DeviceTypeMask.All);

			var gpuTasks = ReadGpuTaskData(compileTaskFile, gpuDataPath);

			Console.WriteLine(kernelPath);
			Console.WriteLine(dataUserPath);
			Console.WriteLine(compileTaskFile);
			Console.WriteLine(gpuDataPath);
			try
			{
				Parallel.ForEach(gpuTasks, HandleDevice);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
				Console.WriteLine(ex.StackTrace);
				return -1;
			}

			return 0;
		} /* end of Main */
	}
}
