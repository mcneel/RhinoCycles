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
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace RhinoCyclesCore.Core
{
	public enum GpuProbeOutcome
	{
		/// <summary>The probe could not be started. Says nothing about the GPUs.</summary>
		NotRun,
		Survived,
		Died,
		TimedOut,
	}

	public readonly struct GpuProbeResult
	{
		public GpuProbeResult(GpuProbeOutcome outcome, string detail)
		{
			Outcome = outcome;
			Detail = detail ?? string.Empty;
		}

		public GpuProbeOutcome Outcome { get; }

		public string Detail { get; }

		/// <summary>True when the scan killed or hung the probe.</summary>
		public bool IsCrash => Outcome == GpuProbeOutcome.Died || Outcome == GpuProbeOutcome.TimedOut;
	}

	/// <summary>
	/// RH-98895: run the Cycles device scan in a RhinoCyclesKernelCompiler child first, so a driver
	/// that kills or hangs the scan takes down the child instead of Rhino.
	/// </summary>
	public static class GpuProbe
	{
		public const string ProbeArgument = "--probe";

		/// <summary>Written by the child after the scan returns; its presence is the verdict, not the exit code.</summary>
		public const string ResultPrefix = "rhinocycles-probe-failed-gpus=";

		/// <summary>
		/// Testing only: crash, hang, crash:CUDA (any backend name) or crash:combined (only when more
		/// than one GPU backend is probed). Inherited by the child, which fails instead of scanning.
		/// </summary>
		public const string TestVariable = "RHINOCYCLES_GPU_PROBE_TEST";

		private const DeviceTypeMask AllGpus =
			DeviceTypeMask.CUDA | DeviceTypeMask.OPTIX | DeviceTypeMask.HIP | DeviceTypeMask.METAL | DeviceTypeMask.ONEAPI;

		/// <summary>
		/// A healthy scan takes well under a second; this is what Raytraced may wait on a hanging
		/// driver (twice: combined, then the singles). RHINOCYCLES_GPU_PROBE_TIMEOUT overrides it (seconds).
		/// </summary>
		public static TimeSpan Timeout
		{
			get
			{
				var fromEnvironment = Environment.GetEnvironmentVariable("RHINOCYCLES_GPU_PROBE_TIMEOUT");
				if (int.TryParse(fromEnvironment, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) && seconds > 0)
				{
					return TimeSpan.FromSeconds(seconds);
				}
				return TimeSpan.FromSeconds(30);
			}
		}

		/// <summary>Scan <paramref name="mask"/> in a child process. Never throws.</summary>
		public static GpuProbeResult Run(DeviceTypeMask mask)
		{
			var arguments = string.Format(CultureInfo.InvariantCulture, "{0} \"{1}\" \"{2}\" {3:x8}",
				ProbeArgument, RcCore.It.KernelPath, RcCore.It.DataUserPath, (uint)mask);

			var stdout = new StringBuilder();
			var stderr = new StringBuilder();

			try
			{
				using (var process = new Process { StartInfo = RcCore.It.SetupChildProcessStartInfo(arguments) })
				{
					process.OutputDataReceived += (sender, e) => { if (e.Data != null) lock (stdout) { stdout.AppendLine(e.Data); } };
					process.ErrorDataReceived += (sender, e) => { if (e.Data != null) lock (stderr) { stderr.AppendLine(e.Data); } };

					if (!process.Start())
					{
						return new GpuProbeResult(GpuProbeOutcome.NotRun, "the probe would not start");
					}
					process.BeginOutputReadLine();
					process.BeginErrorReadLine();

					if (!process.WaitForExit((int)Timeout.TotalMilliseconds))
					{
						try { process.Kill(); } catch (Exception) { }
						return new GpuProbeResult(GpuProbeOutcome.TimedOut,
							string.Format(CultureInfo.InvariantCulture, "the scan was still running after {0} seconds", (int)Timeout.TotalSeconds));
					}
					// Without a timeout this also flushes the async readers.
					process.WaitForExit();

					string output, errors;
					lock (stdout) { output = stdout.ToString(); }
					lock (stderr) { errors = stderr.ToString().Trim(); }

					if (output.Contains(ResultPrefix))
					{
						return new GpuProbeResult(GpuProbeOutcome.Survived, errors);
					}

					var detail = string.Format(CultureInfo.InvariantCulture, "the scan did not report back (exit code {0})", process.ExitCode);
					if (errors.Length > 0) detail += "; " + errors;
					return new GpuProbeResult(GpuProbeOutcome.Died, detail);
				}
			}
			catch (Exception ex)
			{
				return new GpuProbeResult(GpuProbeOutcome.NotRun, ex.GetType().Name + ": " + ex.Message);
			}
		}

		/// <summary>For the child: whether <see cref="TestVariable"/> asks a probe of <paramref name="mask"/> to fail, and how.</summary>
		public static bool TestFires(DeviceTypeMask mask, out bool hang)
		{
			hang = false;
			var test = Environment.GetEnvironmentVariable(TestVariable);
			if (string.IsNullOrEmpty(test)) return false;

			var parts = test.Split(':');
			hang = string.Equals(parts[0], "hang", StringComparison.OrdinalIgnoreCase);
			if (parts.Length < 2) return true;

			if (string.Equals(parts[1], "combined", StringComparison.OrdinalIgnoreCase))
			{
				var gpus = (uint)(mask & AllGpus);
				return (gpus & (gpus - 1)) != 0;
			}
			return Enum.TryParse(parts[1], true, out DeviceTypeMask backend) && (mask & backend) != 0;
		}
	}
}
