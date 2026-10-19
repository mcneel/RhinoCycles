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
	/// <summary>What became of a probe run.</summary>
	public enum GpuProbeOutcome
	{
		/// <summary>The probe could not be started at all. Says nothing about the GPUs.</summary>
		NotRun,
		/// <summary>The scan ran to the end and reported.</summary>
		Survived,
		/// <summary>The probe died before it could report.</summary>
		Died,
		/// <summary>The probe was still running when the timeout ran out.</summary>
		TimedOut,
	}

	/// <summary>The outcome of one probe run.</summary>
	public readonly struct GpuProbeResult
	{
		public GpuProbeResult(GpuProbeOutcome outcome, DeviceTypeMask probed,
			DeviceTypeMask failedGpus, string detail)
		{
			Outcome = outcome;
			Probed = probed;
			FailedGpus = failedGpus;
			Detail = detail ?? string.Empty;
		}

		public GpuProbeOutcome Outcome { get; }

		/// <summary>The backends this run was asked to look at.</summary>
		public DeviceTypeMask Probed { get; }

		/// <summary>
		/// The backends that failed but were caught, as Cycles reported them. Only meaningful
		/// when <see cref="Outcome"/> is <see cref="GpuProbeOutcome.Survived"/>; the host does
		/// not act on these, because its own scan will see and record them again.
		/// </summary>
		public DeviceTypeMask FailedGpus { get; }

		/// <summary>What the probe said, for the log and the support report.</summary>
		public string Detail { get; }

		/// <summary>True when this run is evidence that the scan kills the process.</summary>
		public bool IsCrash => Outcome == GpuProbeOutcome.Died || Outcome == GpuProbeOutcome.TimedOut;
	}

	/// <summary>
	/// RH-98895: run the Cycles device scan in a throwaway child process before the host runs it
	/// for real.
	///
	/// Cycles already catches the ordinary driver failure - each backend is asked separately and
	/// on Windows a structured-exception translator turns a driver access violation into an
	/// exception (see Device::available_devices). What no catch can see is a death that takes the
	/// process with it: a stack overflow, a driver calling abort, a crash on a thread the driver
	/// made itself, or a hang. Today that kills Rhino during startup, every start, with nothing
	/// written down - and the user is left on the forum being told about Safe Mode.
	///
	/// So: ask a process we can afford to lose first. The scan already runs out of process in
	/// RhinoCyclesKernelCompiler, which calls CSycles.initialise itself, so this reuses that
	/// executable with a --probe argument and the launching, environment and output plumbing in
	/// <see cref="RcCore.SetupChildProcessStartInfo"/>.
	///
	/// A probe costs a second driver load, so <see cref="Utilities.CrashedGpuBackends"/> caches
	/// the verdict against the GPU/driver/Rhino signature: once per environment, not once per
	/// start.
	/// </summary>
	public static class GpuProbe
	{
		/// <summary>First argument that puts RhinoCyclesKernelCompiler into probe mode.</summary>
		public const string ProbeArgument = "--probe";

		/// <summary>
		/// The probe's last line of stdout, followed by the failed-GPU mask as eight hex digits.
		/// Its presence is the whole result: it is written after the scan returns, so a probe
		/// that printed it got to the end.
		/// </summary>
		public const string ResultPrefix = "rhinocycles-probe-failed-gpus=";

		/// <summary>
		/// How long a scan may take before we call it hung. Deliberately generous - a cold CUDA
		/// or OptiX init on a slow machine is seconds, and calling a healthy machine hung would
		/// cost it its GPUs. Override with RHINOCYCLES_GPU_PROBE_TIMEOUT (seconds) while testing.
		/// </summary>
		public static TimeSpan Timeout
		{
			get
			{
				var seconds = 120;
				try
				{
					var fromEnvironment = Environment.GetEnvironmentVariable("RHINOCYCLES_GPU_PROBE_TIMEOUT");
					if (!string.IsNullOrEmpty(fromEnvironment) &&
						int.TryParse(fromEnvironment, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) &&
						parsed > 0)
					{
						seconds = parsed;
					}
				}
				catch (Exception) { }
				return TimeSpan.FromSeconds(seconds);
			}
		}

		/// <summary>
		/// Scan <paramref name="mask"/> in a child process and report what became of it.
		/// Never throws: a probe that cannot be run is <see cref="GpuProbeOutcome.NotRun"/>,
		/// which leaves the caller exactly where it was.
		/// </summary>
		/// <param name="armingFile">
		/// RH-98895 test hook: the arming file to hand the child, or null for an ordinary run.
		/// The child reads the arming out of this same file, so host and child cannot disagree
		/// about what was armed.
		/// </param>
		public static GpuProbeResult Run(DeviceTypeMask mask, string armingFile = null)
		{
			var arguments = string.Format(CultureInfo.InvariantCulture, "{0} \"{1}\" \"{2}\" {3:x8}",
				ProbeArgument, RcCore.It.KernelPath, RcCore.It.DataUserPath, (uint)mask);
			if (!string.IsNullOrEmpty(armingFile))
			{
				arguments += string.Format(CultureInfo.InvariantCulture, " \"{0}\"", armingFile);
			}

			var stdout = new StringBuilder();
			var stderr = new StringBuilder();

			try
			{
				var startInfo = RcCore.It.SetupChildProcessStartInfo(arguments);
				using (var process = new Process { StartInfo = startInfo })
				{
					process.OutputDataReceived += (sender, e) =>
					{
						if (e.Data == null) return;
						lock (stdout) { stdout.AppendLine(e.Data); }
					};
					process.ErrorDataReceived += (sender, e) =>
					{
						if (e.Data == null) return;
						lock (stderr) { stderr.AppendLine(e.Data); }
					};

					if (!process.Start())
					{
						return new GpuProbeResult(GpuProbeOutcome.NotRun, mask, 0, "the probe would not start");
					}
					process.BeginOutputReadLine();
					process.BeginErrorReadLine();

					if (!process.WaitForExit((int)Timeout.TotalMilliseconds))
					{
						try { process.Kill(); } catch (Exception) { }
						return new GpuProbeResult(GpuProbeOutcome.TimedOut, mask, 0,
							string.Format(CultureInfo.InvariantCulture,
								"the scan was still running after {0} seconds", (int)Timeout.TotalSeconds));
					}
					// Second wait with no timeout: this is what flushes the async readers, so the
					// result line is not still in flight when we go looking for it below.
					process.WaitForExit();

					string output;
					lock (stdout) { output = stdout.ToString(); }
					string errors;
					lock (stderr) { errors = stderr.ToString(); }

					// The result line is the verdict, not the exit code. A broken driver can take
					// the process down on its way out, after the scan returned and we already
					// know what we came for.
					if (_TryReadResult(output, out DeviceTypeMask failed))
					{
						return new GpuProbeResult(GpuProbeOutcome.Survived, mask, failed, errors.Trim());
					}

					var detail = string.Format(CultureInfo.InvariantCulture,
						"the scan did not report back (exit code {0})", process.ExitCode);
					if (errors.Trim().Length > 0) detail += "; " + errors.Trim();
					return new GpuProbeResult(GpuProbeOutcome.Died, mask, 0, detail);
				}
			}
			catch (Exception ex)
			{
				// Could not launch, could not read it back - either way this is not a verdict
				// on the GPUs, and startup carries on as if the probe did not exist.
				return new GpuProbeResult(GpuProbeOutcome.NotRun, mask, 0,
					ex.GetType().Name + ": " + ex.Message);
			}
		}

		private static bool _TryReadResult(string output, out DeviceTypeMask failed)
		{
			failed = 0;
			if (string.IsNullOrEmpty(output)) return false;
			foreach (var line in output.Split('\n'))
			{
				var trimmed = line.Trim();
				if (!trimmed.StartsWith(ResultPrefix, StringComparison.Ordinal)) continue;
				var value = trimmed.Substring(ResultPrefix.Length).Trim();
				if (!uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsed))
				{
					continue;
				}
				failed = (DeviceTypeMask)parsed;
				return true;
			}
			return false;
		}
	}
}
