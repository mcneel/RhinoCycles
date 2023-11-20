// Enable next line for debug assistance
// #define DEBUGCOMPILER
using System;
using ccl;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

namespace RhinoCyclesOpenClCompiler {

	class Program {

		static void HandleDevice(Device device)
		{
			var id = $"{device.Id}: {device.NiceName}";
			var laststatus = "";
			Stopwatch sw = Stopwatch.StartNew();
			if (device.IsCpu) return;
			Session session = null;
			try
			{
				Client client = new Client();
				SessionParameters sessionParameters = new SessionParameters(device)
				{
					Experimental = false,
					Samples = 2,
					TileSize = 32,
					Threads = 0,
					ShadingSystem = ShadingSystem.SVM,
					Background = false,
					PixelSize = 1,
				};
				session = new Session(sessionParameters);

				session.AddPass(PassType.Combined);
				session.Reset(10, 10, 2, 0, 0, 10, 10);
				Console.WriteLine($"Started for {id}");
				session.Start();
				while (true)
				{
					string status = CSycles.progress_get_status(session.Id);
					string substatus = CSycles.progress_get_substatus(session.Id);
					status = $"{id} | {status}: {substatus}".ToLowerInvariant();
					int sample = CSycles.progress_get_sample(session.Id);
					bool finished = status.Contains("finished") || status.Contains("rendering done");
					if (status.Contains("error"))
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
				Console.WriteLine($"Failed for {id}\n\t{e.ToString()}");

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
			}
		}

		static int Main(string[] args)
		{
#if DEBUGCOMPILER
			System.Diagnostics.Debugger.Launch();
#endif
			if(args.Length!=2) {
				Console.WriteLine("Need kernel and user data paths of RhinoCycles");
				return -1;
			}

			var kernelpath = args[0];
			var datauserpath = args[1];

			Console.WriteLine(kernelpath);
			Console.WriteLine(datauserpath);
			try
			{
				CSycles.path_init(kernelpath, datauserpath);
				CSycles.initialise(DeviceTypeMask.All);
				Parallel.ForEach(Device.Devices, HandleDevice);
			} catch (Exception ex) {
				Console.WriteLine(ex.ToString());
				Console.WriteLine(ex.StackTrace);
			}
			return -1;
		} /* end of Main */
	}
}
