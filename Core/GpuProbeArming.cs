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
using System.Globalization;
using System.IO;
using System.Text;

namespace RhinoCyclesCore.Core
{
	/// <summary>How an armed probe run is made to fail.</summary>
	public enum GpuProbeArmedMode
	{
		/// <summary>Not armed. The probe does exactly what it does for a user.</summary>
		None,
		/// <summary>Die before reporting, the way a driver that takes the process with it does.</summary>
		Crash,
		/// <summary>Never come back, so the host's timeout is what ends the run.</summary>
		Hang,
	}

	/// <summary>Which of a start's probe runs an arming applies to.</summary>
	public enum GpuProbeArmedScope
	{
		/// <summary>Every run: the combined scan and each narrowing run after it.</summary>
		EveryScan,
		/// <summary>The combined scan only, so every single backend comes back alive.</summary>
		CombinedScanOnly,
		/// <summary>Only the runs that include one named backend.</summary>
		OneBackend,
	}

	/// <summary>
	/// RH-98895: the crash guard's failure half cannot be reached with working hardware. A probe
	/// that dies, a probe that hangs, the loop that narrows a crash down to one backend, the
	/// fallback when no single backend reproduces it - all of it needs a driver that really kills
	/// the device scan. This arms those failures on purpose instead.
	///
	/// The arming lives in a file in the plug-in settings directory because what it has to reach
	/// is a child process: the host hands the path to RhinoCyclesKernelCompiler and the child
	/// reads the same file back, so the two can never disagree about what was armed.
	///
	/// Nothing here may touch <see cref="RhinoCyclesCore.Utilities"/>. That type's static RcPlugIn
	/// field looks the plug-in up, which cannot work inside the probe child - hence the GPU backend
	/// bits below are spelled out again rather than taken from Utilities' own table.
	/// </summary>
	public readonly struct GpuProbeArming
	{
		private const string _ModeKey = "mode=";
		private const string _ScopeKey = "scope=";
		private const string _CrashValue = "crash";
		private const string _HangValue = "hang";
		private const string _EveryValue = "every";
		private const string _CombinedValue = "combined";

		/// <summary>Nothing armed. What every missing or unrecognised file reads as.</summary>
		public static readonly GpuProbeArming NotArmed = default;

		public GpuProbeArming(GpuProbeArmedMode mode, GpuProbeArmedScope scope, DeviceTypeMask backend = 0)
		{
			Mode = mode;
			Scope = scope;
			Backend = backend;
		}

		public GpuProbeArmedMode Mode { get; }

		public GpuProbeArmedScope Scope { get; }

		/// <summary>The backend <see cref="GpuProbeArmedScope.OneBackend"/> names; zero otherwise.</summary>
		public DeviceTypeMask Backend { get; }

		public bool IsArmed => Mode != GpuProbeArmedMode.None;

		/// <summary>
		/// True when the run that is about to scan <paramref name="probing"/> is one of the runs
		/// this arming was meant for.
		/// </summary>
		public bool Fires(DeviceTypeMask probing)
		{
			if (!IsArmed) return false;
			switch (Scope)
			{
				case GpuProbeArmedScope.EveryScan:
					return true;
				case GpuProbeArmedScope.CombinedScanOnly:
					// The narrowing runs each ask for CPU plus one backend, so the combined scan is
					// the only run with more than one GPU bit in it.
					return _GpuBackendCount(probing) > 1;
				case GpuProbeArmedScope.OneBackend:
					return Backend != 0 && (probing & Backend) != 0;
				default:
					return false;
			}
		}

		/// <summary>
		/// Read an arming file. Missing, unreadable or not understood all read as
		/// <see cref="NotArmed"/>: a test hook must never be the reason a start goes wrong.
		/// </summary>
		public static GpuProbeArming Read(string file)
		{
			try
			{
				if (string.IsNullOrEmpty(file) || !File.Exists(file)) return NotArmed;
				return Parse(File.ReadAllText(file));
			}
			catch (Exception) { return NotArmed; }
		}

		/// <summary>Read the key=value shape the other records in the settings folder use.</summary>
		public static GpuProbeArming Parse(string content)
		{
			if (string.IsNullOrEmpty(content)) return NotArmed;

			var mode = GpuProbeArmedMode.None;
			var scope = GpuProbeArmedScope.EveryScan;
			DeviceTypeMask backend = 0;

			foreach (var raw in content.Split('\n'))
			{
				var line = raw.Trim();
				if (line.StartsWith(_ModeKey, StringComparison.Ordinal))
				{
					var value = line.Substring(_ModeKey.Length).Trim();
					if (string.Equals(value, _CrashValue, StringComparison.OrdinalIgnoreCase))
					{
						mode = GpuProbeArmedMode.Crash;
					}
					else if (string.Equals(value, _HangValue, StringComparison.OrdinalIgnoreCase))
					{
						mode = GpuProbeArmedMode.Hang;
					}
					else return NotArmed;
				}
				else if (line.StartsWith(_ScopeKey, StringComparison.Ordinal))
				{
					var value = line.Substring(_ScopeKey.Length).Trim();
					if (string.Equals(value, _EveryValue, StringComparison.OrdinalIgnoreCase))
					{
						scope = GpuProbeArmedScope.EveryScan;
					}
					else if (string.Equals(value, _CombinedValue, StringComparison.OrdinalIgnoreCase))
					{
						scope = GpuProbeArmedScope.CombinedScanOnly;
					}
					else if (uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsed)
						&& parsed != 0)
					{
						scope = GpuProbeArmedScope.OneBackend;
						backend = (DeviceTypeMask)parsed;
					}
					else return NotArmed;
				}
			}

			// A file with no mode in it is not an arming, whatever else it says.
			if (mode == GpuProbeArmedMode.None) return NotArmed;
			return new GpuProbeArming(mode, scope, backend);
		}

		/// <summary>The file contents for this arming.</summary>
		public string ToRecord()
		{
			var sb = new StringBuilder();
			sb.AppendLine(_ModeKey + (Mode == GpuProbeArmedMode.Hang ? _HangValue : _CrashValue));
			sb.AppendLine(_ScopeKey + _ScopeValue());
			sb.AppendLine("armed=" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
			sb.AppendLine("by=RhinoCyclesDisableGpu");
			return sb.ToString();
		}

		/// <summary>One line naming the arming, for the log and the probe record.</summary>
		public override string ToString()
		{
			if (!IsArmed) return "not armed";
			return string.Format(CultureInfo.InvariantCulture, "{0}/{1}",
				Mode == GpuProbeArmedMode.Hang ? _HangValue : _CrashValue, _ScopeValue());
		}

		private string _ScopeValue()
		{
			switch (Scope)
			{
				case GpuProbeArmedScope.CombinedScanOnly:
					return _CombinedValue;
				case GpuProbeArmedScope.OneBackend:
					return ((uint)Backend).ToString("x8", CultureInfo.InvariantCulture);
				default:
					return _EveryValue;
			}
		}

		/// <summary>
		/// The GPU backend bits, spelled out here because this type has to stay usable inside the
		/// probe child, where Utilities and its table cannot be loaded.
		/// </summary>
		private static readonly DeviceTypeMask[] _GpuBackends = new[]
		{
			DeviceTypeMask.CUDA,
			DeviceTypeMask.OPTIX,
			DeviceTypeMask.HIP,
			DeviceTypeMask.METAL,
			DeviceTypeMask.ONEAPI,
		};

		private static int _GpuBackendCount(DeviceTypeMask mask)
		{
			var count = 0;
			foreach (var backend in _GpuBackends)
			{
				if ((mask & backend) != 0) count++;
			}
			return count;
		}
	}
}
