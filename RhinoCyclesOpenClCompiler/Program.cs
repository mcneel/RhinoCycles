// Enable next line for debug assistance
// #define DEBUGCOMPILER
using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using ccl;

namespace RhinoCyclesOpenClCompiler {

	class Program {

		static int Main(string[] args) {

			return 0;
#if NOLONGER
			bool debugging = args.Contains("d");

			if (debugging)
			{
				Console.WriteLine("Attach debugger if you want, then press enter to continue...");
				Console.ReadLine();
			}
			using (NamedPipeClientStream pipeClient =
					new NamedPipeClientStream(".", "rhino.opencl.compiler", PipeDirection.In))
			{
				pipeClient.Connect();

				using (StreamReader sr = new StreamReader(pipeClient))
				{
					string temp;

					// Wait for 'sync message' from the server.
					do
					{
						temp = sr.ReadLine();
					}
					while (!temp.StartsWith("SYNC"));

					var compilingLock = sr.ReadLine();
					var compiledFileName = sr.ReadLine();
					var kernelpath = sr.ReadLine();
					var datauserpath = sr.ReadLine();
					var id = int.Parse(sr.ReadLine());
					var nicename = sr.ReadLine();

					CSycles.path_init(kernelpath, datauserpath);
					CSycles.initialise(DeviceTypeMask.All);

					if(id < Device.Devices.Count()) {
						Device device = Device.GetDevice(id);
						if (device.NiceName.Equals(nicename))
						{
							if (debugging)
							{
								Console.WriteLine($"We have a device match {device}");
							}
							try
							{
								Client client = new Client();
								SessionParameters sessionParameters = new SessionParameters(client, device)
								{
									Experimental = false,
									Samples = 2,
									TileSize = 32,
									Threads = 0,
									ShadingSystem = ShadingSystem.SVM,
									SkipLinearToSrgbConversion = true,
									DisplayBufferLinear = true,
									Background = false,
									ProgressiveRefine = true,
									Progressive = true,
									PixelSize = 1,
								};
								SceneParameters sceneParameters = new SceneParameters(client, ShadingSystem.SVM, BvhType.Static, false, BvhLayout.Default, false);
								Session session = new Session(sessionParameters);
								Scene scene = new Scene(client, sceneParameters, session)
								{
									Integrator =
								{
									MaxBounce = 1,
									TransparentMaxBounce = 1,
									MaxDiffuseBounce = 1,
									MaxGlossyBounce = 1,
									MaxTransmissionBounce = 1,
									MaxVolumeBounce = 1,
									NoCaustics = true,
									DiffuseSamples = 1,
									GlossySamples = 1,
									TransmissionSamples = 1,
									AoBounces = 1,
									MeshLightSamples = 1,
									SubsurfaceSamples = 1,
									VolumeSamples = 1,
									AaSamples = 1,
									FilterGlossy = 1,
									IntegratorMethod = IntegratorMethod.Path,
									SampleAllLightsDirect = true,
									SampleAllLightsIndirect = true,
									SampleClampDirect = 1,
									SampleClampIndirect =1,
									LightSamplingThreshold =  1,
									SamplingPattern = SamplingPattern.Sobol,
									Seed = 1337,
									NoShadows = false,
								}
								};
								scene.Film.SetFilter(FilterType.Gaussian, 1.5f);
								scene.Film.Exposure = 1.0f;
								scene.Film.Update();

								session.AddPass(PassType.Combined);
								session.Scene = scene;

								session.PrepareRun();
								session.Reset(10, 10, 2, 0, 0, 10, 10);
								// just do one, it'll compile and then we're ready.
								session.Sample();
								session.Cancel("done");
								session.EndRun();
								session.Destroy();
							} catch (Exception) {
								if (debugging)
								{
									Console.WriteLine("Error happened, press enter");
									Console.ReadLine();
								}

								if(File.Exists(compilingLock)) {
									File.Delete(compilingLock);
								}
								return -1;
							}
							if (debugging)
							{
								Console.WriteLine("Fin.");
								Console.ReadLine();
							}
							File.Move(compilingLock, compiledFileName);
							return 0;
						}
					}
				}
			}
			return -1;
#endif
		} /* end of Main */
	}
}
