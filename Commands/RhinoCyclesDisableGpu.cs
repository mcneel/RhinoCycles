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

using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.UI;
using RhinoCyclesCore;
using RhinoCyclesCore.Core;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RhinoCycles.Commands
{
	[Guid("C802AF8A-7FD8-4281-93A4-B434961E2388")]
	[CommandStyle(Style.Hidden)]
	public class RhinoCyclesDisableGpu : Command
	{
		static RhinoCyclesDisableGpu _instance;
		public RhinoCyclesDisableGpu()
		{
			if(_instance==null) _instance = this;
		}
		public override string LocalName => Localization.LocalizeString("RhinoCyclesDisableGpu", 61);

		public override string EnglishName => "RhinoCyclesDisableGpu";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			// Backend=All is what this command has always done. Naming a single backend records it
			// as failed instead, which is otherwise only reachable with hardware that really fails -
			// that is how the switched off warning and the Retry button get tested. RH-98701.
			var choices = new List<string> { Localization.LocalizeString("All", 111) };
			choices.AddRange(Utilities.GpuBackendNames);
			var picked = 0;

			var go = new GetOption();
			go.SetCommandPrompt(Localization.LocalizeString("Press Enter to switch off GPU use", 112));
			var list = go.AddOptionList("Backend", choices, picked);
			// RH-98895: hidden, so it is not on the command line and the name has to be typed in
			// full. Nobody reaches the test hook by reading the options.
			var probeTest = go.AddOption("ProbeTest", null, true);
			go.AcceptNothing(true);
			while (true)
			{
				var res = go.Get();
				if (res == GetResult.Nothing) break;
				if (res == GetResult.Option)
				{
					if (go.OptionIndex() == list) picked = go.Option().CurrentListOptionIndex;
					else if (go.OptionIndex() == probeTest) return _ArmProbeTest();
					continue;
				}
				return Result.Cancel;
			}

			if (picked == 0)
			{
				Utilities.DisableGpus();
				RhinoApp.WriteLine(Localization.LocalizeString("GPUs for RhinoCycles have now been disabled. Restart Rhino for the change to take effect.", 62));
				return Result.Success;
			}

			var name = choices[picked];
			if (!Utilities.TryFindGpuBackend(name, out var backend)) return Result.Failure;
			// Say in the record that this was not a real failure, so a report from a tester's
			// machine cannot be mistaken for a driver problem.
			Utilities.DisableGpu(backend,
				"switched off with RhinoCyclesDisableGpu, not a real failure");
			RhinoApp.WriteLine(string.Format(
				Localization.LocalizeString("{0} has been switched off as if it had failed to start. Restart Rhino for the change to take effect.", 113),
				name));
			return Result.Success;
		}

		/// <summary>
		/// RH-98895: arm the next start's device probe to fail on purpose. The guard's failure half
		/// - a probe that dies, a probe that hangs, the loop that narrows a crash down to a single
		/// backend, the fallback when no single backend reproduces it - otherwise needs a driver
		/// that really kills the device scan. The arming is spent by the next start, so a tester
		/// who forgets about it loses one start, not every start.
		/// </summary>
		private static Result _ArmProbeTest()
		{
			// Scan names the runs the arming applies to. The two whole-sequence choices come first;
			// naming a backend arms only the runs that include it, which is what the narrowing loop
			// has to be shown doing.
			var modes = new List<string> { LOC.STR("Crash"), LOC.STR("Hang") };
			var scans = new List<string> { LOC.STR("EveryScan"), LOC.STR("CombinedScanOnly") };
			scans.AddRange(Utilities.GpuBackendNames);
			var mode = 0;
			var scan = 0;

			var go = new GetOption();
			go.SetCommandPrompt(LOC.STR("Press Enter to arm the next GPU device probe to fail"));
			var modeList = go.AddOptionList("Mode", modes, mode);
			var scanList = go.AddOptionList("Scan", scans, scan);
			var disarm = go.AddOption("Disarm");
			go.AcceptNothing(true);
			while (true)
			{
				var res = go.Get();
				if (res == GetResult.Nothing) break;
				if (res == GetResult.Option)
				{
					if (go.OptionIndex() == modeList) mode = go.Option().CurrentListOptionIndex;
					else if (go.OptionIndex() == scanList) scan = go.Option().CurrentListOptionIndex;
					else if (go.OptionIndex() == disarm)
					{
						Utilities.DisarmGpuProbe();
						RhinoApp.WriteLine(LOC.STR("The GPU device probe is no longer armed."));
						return Result.Success;
					}
					continue;
				}
				return Result.Cancel;
			}

			var armedMode = mode == 1 ? GpuProbeArmedMode.Hang : GpuProbeArmedMode.Crash;
			var armedScope = GpuProbeArmedScope.EveryScan;
			ccl.DeviceTypeMask backend = 0;
			if (scan == 1)
			{
				armedScope = GpuProbeArmedScope.CombinedScanOnly;
			}
			else if (scan > 1)
			{
				if (!Utilities.TryFindGpuBackend(scans[scan], out backend)) return Result.Failure;
				armedScope = GpuProbeArmedScope.OneBackend;
			}

			Utilities.ArmGpuProbe(new GpuProbeArming(armedMode, armedScope, backend));

			RhinoApp.WriteLine(string.Format(
				LOC.STR("The GPU device probe is armed to {0} on {1}. Restart Rhino to run it; the arming is spent by that start."),
				modes[mode], scans[scan]));
			RhinoApp.WriteLine(_WhatToExpect(armedScope, scans[scan]));
			if (armedMode == GpuProbeArmedMode.Hang)
			{
				// A hung run is only as quick as the timeout, and there can be six of them.
				RhinoApp.WriteLine(string.Format(
					LOC.STR("Each armed run waits out the probe timeout, which is {0} seconds. Set RHINOCYCLES_GPU_PROBE_TIMEOUT to a few seconds before starting Rhino."),
					(int)GpuProbe.Timeout.TotalSeconds));
			}
			return Result.Success;
		}

		/// <summary>What the next start should end up recording, so a tester can tell right from wrong.</summary>
		private static string _WhatToExpect(GpuProbeArmedScope scope, string scanName)
		{
			switch (scope)
			{
				case GpuProbeArmedScope.CombinedScanOnly:
					return LOC.STR("Expect no single backend to reproduce it, so every GPU backend is skipped.");
				case GpuProbeArmedScope.OneBackend:
					return string.Format(
						LOC.STR("Expect {0} to be the only backend switched off; the others stay enabled."),
						scanName);
				default:
					return LOC.STR("Expect every GPU backend to be switched off.");
			}
		}
	}
}
