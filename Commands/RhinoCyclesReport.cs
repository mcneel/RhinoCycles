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
using Rhino;
using Rhino.Commands;
using Rhino.Input.Custom;
using Rhino.Runtime;
using Rhino.UI;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace RhinoCycles.Commands
{
	/// <summary>
	/// Collects in one zip everything support otherwise asks for one command at a time.
	/// </summary>
	[Guid("6F5B3A21-9C4E-4E0B-8B0A-1D2C3E4F5A6B")]
	// Not hidden, unlike the other RhinoCycles commands: customers are asked to run this one,
	// so it has to autocomplete on the command line.
	[CommandStyle(Style.DoNotRepeat)]
	public class RhinoCyclesReport : Command
	{
		// One place, so the command name, the zip name and the README can never drift apart.
		const string CommandName = "RhinoCyclesReport";

		const int SM_REMOTESESSION = 0x1000;

		// No BOM: a BOM makes report.json unreadable to strict JSON parsers.
		static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

		[DllImport("user32.dll")]
		static extern int GetSystemMetrics(int nIndex);

		static RhinoCyclesReport _instance;
		public RhinoCyclesReport()
		{
			if (_instance == null) _instance = this;
		}

		public static RhinoCyclesReport Instance => _instance;

		public override string LocalName => LOC.COMMANDNAME("RhinoCyclesReport");

		public override string EnglishName => CommandName;

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			(PlugIn as Plugin)?.InitialiseCSycles();

			bool wasDetailed = RcCore.It.AllSettings.VerboseLogging;
			var detailedLogging = new OptionToggle(wasDetailed, LOC.STR("No"), LOC.STR("Yes"));
			var openFolder = new OptionToggle(true, LOC.STR("No"), LOC.STR("Yes"));

			var go = new GetOption();
			go.SetCommandPrompt(LOC.STR("Press Enter to write the RhinoCycles report"));
			go.AddOptionToggle("DetailedLogging", ref detailedLogging);
			go.AddOptionToggle("OpenFolder", ref openFolder);
			go.AcceptNothing(true);

			while (true)
			{
				var res = go.Get();
				if (res == Rhino.Input.GetResult.Nothing) break;
				if (res == Rhino.Input.GetResult.Option) continue;
				return Result.Cancel;
			}

			// Changing the setting is a request for a *better* report later, not for one now: the
			// existing logs predate the change, so a report written here would be the shallow one
			// we were trying to avoid. Apply the change and stop.
			if (detailedLogging.CurrentValue != wasDetailed)
			{
				RcCore.It.AllSettings.VerboseLogging = detailedLogging.CurrentValue;

				if (detailedLogging.CurrentValue)
				{
					RhinoApp.WriteLine("----------");
					RhinoApp.WriteLine(LOC.STR("Detailed logging is now on. No report was written yet - the logs so far do not contain the detail."));
					RhinoApp.WriteLine(LOC.STR("1. Restart Rhino."));
					RhinoApp.WriteLine(LOC.STR("2. Reproduce the problem."));
					RhinoApp.WriteLine(string.Format(LOC.STR("3. Run {0} again to write the report."), EnglishName));
					RhinoApp.WriteLine("----------");
				}
				else
				{
					RhinoApp.WriteLine(LOC.STR("Detailed logging is off again. No report was written."));
				}
				return Result.Success;
			}

			string zipPath;
			try
			{
				zipPath = WriteReport();
			}
			catch (Exception ex)
			{
				RhinoApp.WriteLine(string.Format(LOC.STR("Could not write the report: {0}"), ex.Message));
				return Result.Failure;
			}

			RhinoApp.WriteLine("----------");
			RhinoApp.WriteLine(string.Format(LOC.STR("RhinoCycles report written to {0}"), zipPath));
			RhinoApp.WriteLine(LOC.STR("Please attach this file to your support request."));
			if (!RcCore.It.AllSettings.VerboseLogging)
			{
				RhinoApp.WriteLine(string.Format(
					LOC.STR("For a crash or a failed render, run {0} with DetailedLogging=Yes for a fuller report."),
					EnglishName));
			}
			RhinoApp.WriteLine("----------");

			if (openFolder.CurrentValue && mode == RunMode.Interactive)
			{
				RevealInFileBrowser(zipPath);
			}

			return Result.Success;
		}

		static string WriteReport()
		{
			string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
			string staging = Path.Combine(Path.GetTempPath(), CommandName + "-" + stamp);
			Directory.CreateDirectory(staging);

			try
			{
				// Copy logs first: the checks and summary depend on what the sessions recorded.
				LogStats logs = CopyLogs(Path.Combine(staging, "logs"));
				List<CheckResult> checks = Checks(logs.Unclean, logs.ShutdownKnown);

				// The bulky dumps live beside report.txt so report.txt stays readable.
				WriteSideFile(staging, "device-capabilities.txt", () => Device.Capabilities);
				WriteSideFile(staging, "kernel-cache.txt", KernelCacheListing);
				WriteSideFile(staging, "README.txt", () => ReadmeText(stamp));
				WriteSideFile(staging, "report.json", () => BuildJson(stamp, logs, checks));

				File.WriteAllText(Path.Combine(staging, "report.txt"),
					BuildReport(stamp, logs, checks), Utf8NoBom);

				string zipPath = UniquePath(Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
					CommandName + "-" + stamp + ".zip"));

				ZipFile.CreateFromDirectory(staging, zipPath, CompressionLevel.Optimal, false);
				return zipPath;
			}
			finally
			{
				try { Directory.Delete(staging, true); } catch (Exception) { }
			}
		}

		static void WriteSideFile(string staging, string name, Func<string> content)
		{
			string text;
			try { text = content(); }
			catch (Exception ex) { text = "<failed: " + ex.Message + ">"; }
			try { File.WriteAllText(Path.Combine(staging, name), text ?? "", Utf8NoBom); }
			catch (Exception) { }
		}

		static string UniquePath(string path)
		{
			string dir = Path.GetDirectoryName(path);
			string name = Path.GetFileNameWithoutExtension(path);
			string ext = Path.GetExtension(path);
			string candidate = path;
			for (int i = 1; File.Exists(candidate); i++)
			{
				candidate = Path.Combine(dir, string.Format("{0}-{1}{2}", name, i, ext));
			}
			return candidate;
		}

		class SessionLog
		{
			public string Name;
			public DateTime When;
			public long Bytes;
			public int Lines;
			public int Lookups;
			public bool SawShutdownStart;
			public bool SawShutdownEnd;
		}

		class LogStats
		{
			public int Total;
			public int Busy;
			/// <summary>Sessions that started shutting down but never finished.</summary>
			public int Unclean;
			/// <summary>Sessions where shutdown was logged at all - zero unless verbose was on.</summary>
			public int ShutdownKnown;
		}

		/// <summary>
		/// One log per Rhino session, and on machines that start Rhino often most of them are
		/// startup-only noise. Copy newest-first and write a manifest that marks the ones worth
		/// opening. The startup baseline is measured from the shortest log collected rather than
		/// hard-coded, because it moves with the Rhino version and the verbose setting.
		/// </summary>
		static LogStats CopyLogs(string destination)
		{
			var stats = new LogStats();

			List<FileInfo> logs;
			try
			{
				logs = new DirectoryInfo(RcCore.It.DataUserPath)
					.GetFiles("RhinoCycles*.log")
					.OrderByDescending(f => f.LastWriteTime)
					.ToList();
			}
			catch (Exception) { return stats; }

			Directory.CreateDirectory(destination);

			var collected = new List<SessionLog>();
			foreach (var log in logs)
			{
				string copy = Path.Combine(destination, log.Name);
				try
				{
					using (var src = new FileStream(log.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
					using (var dst = File.Create(copy))
					{
						src.CopyTo(dst);
					}
				}
				catch (Exception) { continue; }

				var entry = new SessionLog { Name = log.Name, When = log.LastWriteTime };
				try
				{
					foreach (string line in File.ReadLines(copy))
					{
						entry.Lines++;
						if (line.IndexOf("Finding render device", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							entry.Lookups++;
						}
						// Both are verbose-only, so a session with neither tells us nothing.
						if (line.IndexOf("Shutdown entry", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							entry.SawShutdownStart = true;
						}
						else if (line.IndexOf("Shutdown exit", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							entry.SawShutdownEnd = true;
						}
					}
				}
				catch (Exception) { }

				// The live log is still buffered, so its on-disk size can read 0 - measure the copy.
				entry.Bytes = log.Length;
				try { entry.Bytes = new FileInfo(copy).Length; } catch (Exception) { }

				collected.Add(entry);
			}

			stats.Total = collected.Count;
			// A session counts only if shutdown was logged at all; with verbose off nothing is.
			stats.ShutdownKnown = collected.Count(e => e.SawShutdownStart || e.SawShutdownEnd);
			stats.Unclean = collected.Count(e => e.SawShutdownStart && !e.SawShutdownEnd);

			// Median, not minimum: a single truncated log (a crash caught mid-write) would drag a
			// minimum-based baseline to almost nothing and mark every session busy.
			int baseline = 0;
			if (collected.Count > 0)
			{
				var lengths = collected.Select(e => e.Lines).OrderBy(n => n).ToList();
				baseline = lengths[lengths.Count / 2];
			}
			// Only meaningful once there is more than one session to compare against.
			int threshold = collected.Count > 1 ? (int)(baseline * 1.2) : int.MaxValue;

			var manifest = new StringBuilder();
			manifest.AppendLine("RhinoCycles session logs, newest first.");
			manifest.AppendLine(string.Format(CultureInfo.InvariantCulture,
				"The typical session here is {0} lines - that is roughly what starting Rhino and doing nothing costs.",
				baseline));
			manifest.AppendLine(threshold == int.MaxValue
				? "Only one session was collected, so nothing is marked."
				: string.Format(CultureInfo.InvariantCulture,
					"Sessions over {0} lines are marked [busy]; open those first, the rest are noise.", threshold));
			manifest.AppendLine();

			foreach (var e in collected)
			{
				bool isBusy = e.Lines > threshold;
				if (isBusy) stats.Busy++;
				string shutdown = !(e.SawShutdownStart || e.SawShutdownEnd) ? ""
					: e.SawShutdownEnd ? "  clean-exit" : "  NO CLEAN EXIT";
				manifest.AppendLine(string.Format(CultureInfo.InvariantCulture,
					"{0:yyyy-MM-dd HH:mm:ss}  {1,5} lines  {2,9:N0} bytes  {3,3} lookups  {4,-9}  {5}{6}",
					e.When, e.Lines, e.Bytes, e.Lookups, isBusy ? "[busy]" : "[startup]", e.Name, shutdown));
			}

			manifest.AppendLine();
			manifest.AppendLine(string.Format(CultureInfo.InvariantCulture,
				"{0} session log(s), {1} busy, {2} startup-only.", stats.Total, stats.Busy, stats.Total - stats.Busy));
			manifest.AppendLine(stats.ShutdownKnown > 0
				? string.Format(CultureInfo.InvariantCulture,
					"{0} session(s) recorded a shutdown, {1} of those never finished it.",
					stats.ShutdownKnown, stats.Unclean)
				: RcCore.It.AllSettings.VerboseLogging
					? "No session recorded a shutdown, so clean exits cannot be judged."
					: "No session recorded a shutdown, so clean exits cannot be judged - that needs verbose logging.");
			try { File.WriteAllText(Path.Combine(destination, "manifest.txt"), manifest.ToString(), Utf8NoBom); }
			catch (Exception) { }

			return stats;
		}

		/// <summary>True/false on Windows; null where we have no way to tell (macOS).</summary>
		static bool? IsRemoteSession()
		{
			if (!HostUtils.RunningOnWindows) return null;
			try { return GetSystemMetrics(SM_REMOTESESSION) != 0; }
			catch (Exception) { return null; }
		}

		/// <summary>
		/// Mirrors RcCore.SetupProcessStartInfo: an .exe on Windows, a .dll run through dotnet on
		/// macOS. Checking for the wrong one would fail on every Mac.
		/// </summary>
		static string KernelCompilerPath()
		{
			string dir;
			try { dir = Path.GetDirectoryName(Assembly.GetAssembly(typeof(RcCore)).Location); }
			catch (Exception) { dir = RcCore.It.PluginPath; }

			string path = Path.Combine(dir, "RhinoCyclesKernelCompiler");
			return HostUtils.RunningOnWindows ? path + ".exe" : path + ".dll";
		}

		/// <summary>Kept out of line so macOS never JITs a reference to System.Management.</summary>
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void WindowsVideoControllers(StringBuilder sb)
		{
			using (var searcher = new ManagementObjectSearcher(
				"SELECT Name, VideoProcessor, DriverVersion, DriverDate, Status FROM Win32_VideoController"))
			{
				foreach (ManagementObject mo in searcher.Get())
				{
					sb.AppendLine(string.Format("  {0} | {1} | driver {2} ({3}) | {4}",
						mo["Name"], mo["VideoProcessor"], mo["DriverVersion"], mo["DriverDate"], mo["Status"]));
					mo.Dispose();
				}
			}
		}

		/// <summary>Kept out of line so macOS never JITs a reference to System.Management.</summary>
		[MethodImpl(MethodImplOptions.NoInlining)]
		static void WindowsMachineInfo(StringBuilder sb)
		{
			using (var searcher = new ManagementObjectSearcher(
				"SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem"))
			{
				foreach (ManagementObject mo in searcher.Get())
				{
					sb.AppendLine("Manufacturer   " + mo["Manufacturer"]);
					sb.AppendLine("Model          " + mo["Model"]);
					ulong ram;
					if (mo["TotalPhysicalMemory"] != null &&
						ulong.TryParse(mo["TotalPhysicalMemory"].ToString(), out ram))
					{
						sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Physical RAM   {0:N0} MB",
							ram / (1024UL * 1024UL)));
					}
					mo.Dispose();
				}
			}
		}

		/// <summary>macOS equivalent of the WMI machine facts, via sysctl.</summary>
		static void MacMachineInfo(StringBuilder sb)
		{
			sb.AppendLine("Model          " + (Sysctl("hw.model") ?? "<unknown>"));

			string memsize = Sysctl("hw.memsize");
			ulong bytes;
			if (memsize != null && ulong.TryParse(memsize, out bytes))
			{
				sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Physical RAM   {0:N0} MB",
					bytes / (1024UL * 1024UL)));
			}
			sb.AppendLine("CPU            " + (Sysctl("machdep.cpu.brand_string") ?? "<unknown>"));
		}

		static string Sysctl(string name)
		{
			try
			{
				var psi = new ProcessStartInfo("/usr/sbin/sysctl", "-n " + name)
				{
					UseShellExecute = false,
					RedirectStandardOutput = true,
					CreateNoWindow = true,
				};
				using (var p = Process.Start(psi))
				{
					string value = p.StandardOutput.ReadToEnd().Trim();
					p.WaitForExit(5000);
					return string.IsNullOrEmpty(value) ? null : value;
				}
			}
			catch (Exception) { return null; }
		}

		static void RevealInFileBrowser(string path)
		{
			try
			{
				if (HostUtils.RunningOnWindows)
				{
					Process.Start("explorer.exe", string.Format("/select,\"{0}\"", path));
				}
				else if (HostUtils.RunningOnOSX)
				{
					// Same as everywhere else in Rhino (e.g. Rhino.UI OverviewEditor). Note that with
					// no Finder window already showing the folder this selects without raising a
					// window - see RH-98291 for a shared cross-platform reveal helper.
					Process.Start("/usr/bin/open", string.Format("-R \"{0}\"", path));
				}
			}
			catch (Exception) { }
		}

		static string KernelCacheListing()
		{
			var dir = new DirectoryInfo(RcCore.It.GpuCompilePath);
			if (!dir.Exists) return "(does not exist)";

			var sb = new StringBuilder();
			foreach (var f in dir.GetFiles("*", SearchOption.AllDirectories).OrderBy(f => f.FullName))
			{
				sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,12:N0}  {1:u}  {2}",
					f.Length, f.LastWriteTimeUtc, f.FullName.Substring(dir.FullName.Length).TrimStart('\\', '/')));
			}
			return sb.ToString();
		}

		/// <summary>Cache health: a full, read-only or missing cache is a silent compile failure.</summary>
		static void KernelCacheHealth(StringBuilder sb)
		{
			string path = RcCore.It.GpuCompilePath;
			bool exists = Directory.Exists(path);
			sb.AppendLine("Path        " + path);
			sb.AppendLine("Exists      " + exists);

			bool writable = false;
			if (exists)
			{
				string probe = Path.Combine(path, "supportreport.writeprobe");
				try
				{
					File.WriteAllText(probe, "");
					File.Delete(probe);
					writable = true;
				}
				catch (Exception) { }
			}
			sb.AppendLine("Writable    " + writable);

			try
			{
				var drive = new DriveInfo(Path.GetPathRoot(path));
				sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Free space  {0:N0} MB on {1}",
					drive.AvailableFreeSpace / (1024L * 1024L), drive.Name));
			}
			catch (Exception ex) { sb.AppendLine("Free space  <" + ex.GetType().Name + ">"); }

			if (exists)
			{
				var files = new DirectoryInfo(path).GetFiles("*", SearchOption.AllDirectories);
				sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Entries     {0}", files.Length));
				sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Pending     {0} .task file(s)",
					files.Count(f => f.Extension.Equals(".task", StringComparison.OrdinalIgnoreCase))));
			}

			string compiler = KernelCompilerPath();
			sb.AppendLine("Compiler    " + (File.Exists(compiler)
				? compiler + " (" + FileVersionInfo.GetVersionInfo(compiler).FileVersion + ")"
				: compiler + " MISSING"));
		}

		/// <summary>What the customer changed - far more useful for triage than the full list.</summary>
		static List<string> NonDefaultSettings()
		{
			var result = new List<string>();
			var settings = RcCore.It.AllSettings;
			var defaults = typeof(DefaultEngineSettings);

			foreach (var p in SettingProperties(settings))
			{
				var dp = defaults.GetProperty(p.Name, BindingFlags.Public | BindingFlags.Static);
				if (dp == null || !dp.CanRead) continue;

				string current, standard;
				try
				{
					current = Convert.ToString(p.GetValue(settings, null), CultureInfo.InvariantCulture);
					standard = Convert.ToString(dp.GetValue(null, null), CultureInfo.InvariantCulture);
				}
				catch (Exception) { continue; }

				if (!string.Equals(current, standard, StringComparison.Ordinal))
				{
					result.Add(string.Format("  {0} = {1}   (default {2})", p.Name, current, standard));
				}
			}
			return result;
		}

		// Reflect the concrete type: interface GetProperties() skips inherited interface members.
		static IEnumerable<PropertyInfo> SettingProperties(object settings)
		{
			return settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
				.OrderBy(p => p.Name);
		}

		enum CheckKind { Ok, Warn, Note }

		class CheckResult
		{
			public CheckKind Kind;
			/// <summary>Lower sorts first and wins the verdict line - roughly "how likely a root cause".</summary>
			public int Priority;
			/// <summary>Short phrase for the verdict line.</summary>
			public string Label;
			public string Text;
			public string Reference;

			public override string ToString()
			{
				string prefix = Kind == CheckKind.Ok ? "  OK    " : Kind == CheckKind.Warn ? "  WARN  " : "  NOTE  ";
				return prefix + Text + (string.IsNullOrEmpty(Reference) ? "" : "  (" + Reference + ")");
			}
		}

		/// <summary>
		/// Plain-language pass/warn lines. This is what the support team reads; everything below
		/// it in the report exists so we can dig deeper afterwards.
		/// </summary>
		static List<CheckResult> Checks(int uncleanShutdowns, int shutdownsKnown)
		{
			var checks = new List<CheckResult>();

			Action<bool, int, string, string, string, string> check =
				(ok, priority, label, warning, fine, reference) => checks.Add(new CheckResult
				{
					Kind = ok ? CheckKind.Ok : CheckKind.Warn,
					Priority = priority,
					Label = label,
					Text = ok ? fine : warning,
					Reference = ok ? null : reference,
				});

			Action<int, string, string> note = (priority, label, text) => checks.Add(new CheckResult
			{
				Kind = CheckKind.Note,
				Priority = priority,
				Label = label,
				Text = text,
			});

			// Skipped rather than guessed where we cannot detect it (macOS).
			bool? remote = IsRemoteSession();
			if (remote.HasValue)
			{
				check(!remote.Value, 10, "Rhino is running over Remote Desktop",
					"Rhino is running in a remote desktop session. GPUs are normally not available to Cycles here - ask the customer to test at the physical machine.",
					"Not a remote desktop session.", null);
			}

			check(!RhinoCyclesCore.Utilities.GpusDisabled, 20, "GPU use has been switched off",
				"GPU use has been switched off (RhinoCyclesDisableGpu was run). Run RhinoCyclesEnableGpu and restart Rhino.",
				"GPU use is enabled.", null);

			var gpus = new List<Device>();
			try { gpus = Device.Devices.Where(d => d.IsGpu).ToList(); } catch (Exception) { }

			check(gpus.Count > 0, 30, "Cycles found no GPU at all",
				"Cycles found no GPU devices at all - only the CPU is available.",
				string.Format(CultureInfo.InvariantCulture, "Cycles found {0} GPU device(s).", gpus.Count), null);

			var displayGpus = new List<GpuDeviceInfo>();
			try { displayGpus = DisplayDeviceInfo.GpuDeviceInfos(); } catch (Exception) { }

			// Only meaningful where we can enumerate the adapters independently of Cycles.
			// DisplayDeviceInfo does that on Windows only, so on macOS there is nothing to compare
			// against and running the check would print a pass we have not actually established.
			if (gpus.Count > 0 && displayGpus.Count > 0)
			{
				check(gpus.Count >= displayGpus.Count, 35, "Cycles is skipping a GPU",
					string.Format(CultureInfo.InvariantCulture,
						"The system reports {0} GPU(s) but Cycles only offers {1} - a card is being skipped (unsupported model, or a driver/toolkit problem).",
						displayGpus.Count, gpus.Count),
					string.Format(CultureInfo.InvariantCulture,
						"Cycles offers a device for every GPU the system reports ({0}).", displayGpus.Count), null);
			}

			// With no GPU to compile for, "not finished" is the expected state and not a fault -
			// reporting it would bury the real cause under a warning the customer cannot act on.
			if (gpus.Count > 0 && !RhinoCyclesCore.Utilities.GpusDisabled)
			{
				check(!RcCore.It.CompileProcessError, 40, "the GPU kernel compile failed",
					"The GPU kernel compile reported ERRORS - see the compile log section.",
					"GPU kernel compile reported no errors.", null);

				check(RcCore.It.CompileProcessFinished, 50, "the GPU kernel compile has not finished",
					"The GPU kernel compile has not finished. Rendering on GPU will fall back or fail until it does.",
					"GPU kernel compile finished.", null);
			}

			check(!RcCore.It.InitialisationFailed, 60, "RhinoCycles failed to initialise",
				"RhinoCycles failed to initialise - see the log sections.",
				"RhinoCycles initialised.", null);

			string cache = RcCore.It.GpuCompilePath;
			bool cacheOk = false;
			try
			{
				if (Directory.Exists(cache))
				{
					string probe = Path.Combine(cache, "supportreport.writeprobe");
					File.WriteAllText(probe, "");
					File.Delete(probe);
					cacheOk = true;
				}
			}
			catch (Exception) { }
			check(cacheOk, 70, "the kernel cache folder is not writable",
				"The GPU kernel cache folder is missing or not writable. Kernels cannot be cached, so GPU rendering may silently fail: " + cache,
				"GPU kernel cache folder is writable.", null);

			string compiler = KernelCompilerPath();
			check(File.Exists(compiler), 80, "the kernel compiler is missing",
				"The RhinoCycles kernel compiler is missing from the plug-in folder - the install is incomplete or antivirus removed it: " + compiler,
				"Kernel compiler executable is present.", null);

			// Known-configuration detectors. These are the shapes support has seen before.
			// Metal is excluded: an Intel Mac reports "Intel" here and works fine, so including it
			// would fire this warning on every Intel Mac.
			bool anyMetal = gpus.Any(d => d.Type == DeviceType.Metal);
			bool intelOnly = !anyMetal && gpus.Count > 0 && gpus.All(d =>
				d.Type == DeviceType.OneApi ||
				(d.Description ?? "").IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0);
			check(!intelOnly, 90, "an Intel integrated GPU is the only GPU",
				"The only GPU available to Cycles is Intel integrated graphics. Cycles support for Intel iGPUs is known-broken (oneAPI kernel init fails with -30); the customer should render on CPU for now.",
				"Not limited to Intel integrated graphics.", "RH-96815, RH-97665");

			bool hasIntegrated = displayGpus.Any(g =>
				(g.Name ?? "").IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0);
			bool hasDiscrete = displayGpus.Any(g =>
				(g.Name ?? "").IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0 ||
				(g.Name ?? "").IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0);
			if (hasIntegrated && hasDiscrete)
			{
				note(95, "hybrid graphics", HostUtils.RunningOnWindows
					? "This machine has both integrated and discrete graphics. Check that Windows is giving Rhino the discrete GPU (Graphics Settings > Rhino > High performance)."
					: "This machine has both integrated and discrete graphics. Check that Rhino is being given the discrete GPU rather than the integrated one.");
			}

			var vendors = gpus.Select(d => d.Type).Distinct()
				.Where(t => t != DeviceType.Cpu && t != DeviceType.Optix).ToList();
			if (vendors.Count > 1)
			{
				note(100, "mixed-vendor GPUs",
					"GPUs from more than one vendor are present (" + string.Join(", ", vendors.Select(v => v.ToString()).ToArray())
					+ "). This is an unusual setup and worth keeping in mind, though it is not wrong by itself.");
			}

			foreach (var gpu in displayGpus)
			{
				const ulong fourGb = 4UL * 1024UL * 1024UL * 1024UL;
				if (gpu.Memory > 0 && gpu.Memory < fourGb)
				{
					check(false, 110, "a GPU has very little memory",
						string.Format(CultureInfo.InvariantCulture,
							"{0} has only {1} of memory. Larger scenes will not fit and will fall back or fail.",
							gpu.Name, gpu.MemoryAsString),
						null, null);
				}
			}

			int computeMajor = LowestCudaComputeCapability();
			if (computeMajor > 0)
			{
				check(computeMajor >= 5, 115, "a CUDA GPU is too old",
					string.Format(CultureInfo.InvariantCulture,
						"A CUDA device reports compute capability {0}.x. The kernels need 5.0 or newer, so this card cannot be used.",
						computeMajor),
					string.Format(CultureInfo.InvariantCulture, "CUDA compute capability {0}.x is supported.", computeMajor), null);
			}

			// Only meaningful with verbose logging on - nothing marks a clean shutdown otherwise.
			if (shutdownsKnown > 0)
			{
				check(uncleanShutdowns == 0, 120, "recent sessions ended without a clean shutdown",
					string.Format(CultureInfo.InvariantCulture,
						"{0} of {1} logged sessions ended without a clean shutdown - that usually means Rhino crashed.",
						uncleanShutdowns, shutdownsKnown),
					string.Format(CultureInfo.InvariantCulture, "All {0} logged session(s) shut down cleanly.", shutdownsKnown), null);
			}
			else if (RcCore.It.AllSettings.VerboseLogging)
			{
				// Say why rather than blaming the verbose setting, which is plainly on.
				note(125, "no shutdown was recorded",
					"No session recorded a shutdown even though verbose logging is on. Either every collected session was killed outright, or they all predate the build that writes the shutdown lines - a crash cannot be told from a clean exit either way.");
			}

			try
			{
				long freeMb = new DriveInfo(Path.GetPathRoot(cache)).AvailableFreeSpace / (1024L * 1024L);
				check(freeMb > 2048, 130, "the kernel-cache drive is nearly full",
					string.Format(CultureInfo.InvariantCulture, "Only {0:N0} MB free on the kernel-cache drive - kernel compiles need room.", freeMb),
					string.Format(CultureInfo.InvariantCulture, "{0:N0} MB free on the kernel-cache drive.", freeMb), null);
			}
			catch (Exception) { }

			foreach (var gpu in displayGpus)
			{
				DateTime driverDate;
				if (!DateTime.TryParse(gpu.DriverDateAsString, CultureInfo.InvariantCulture,
						DateTimeStyles.None, out driverDate))
				{
					continue;
				}
				double months = (DateTime.Now - driverDate).TotalDays / 30.0;
				check(months < 18, 140, "a graphics driver is badly out of date",
					string.Format(CultureInfo.InvariantCulture,
						"The driver for {0} is about {1:N0} months old ({2}) - ask the customer to update it.",
						gpu.Name, months, gpu.DriverDateAsString),
					string.Format(CultureInfo.InvariantCulture, "Driver for {0} is recent ({1}).",
						gpu.Name, gpu.DriverDateAsString), null);
			}

			// Deliberately last: never a cause, only a reason the rest of the report is thin.
			check(RcCore.It.AllSettings.VerboseLogging, 900, "verbose logging was off",
				"Verbose logging was off, so the logs in this report are shallow. For a crash or a failing render, switch it on, restart Rhino, reproduce, and send a second report.",
				"Verbose logging is on - the logs in this report are detailed.", null);

			return checks;
		}

		/// <summary>Lowest CUDA compute capability major across devices, or 0 if none reported.</summary>
		static int LowestCudaComputeCapability()
		{
			try
			{
				int lowest = 0;
				foreach (string line in (Device.Capabilities ?? "").Split('\n'))
				{
					if (line.IndexOf("COMPUTE_CAPABILITY_MAJOR", StringComparison.OrdinalIgnoreCase) < 0) continue;
					var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
					int value;
					if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out value) && value > 0)
					{
						lowest = lowest == 0 ? value : Math.Min(lowest, value);
					}
				}
				return lowest;
			}
			catch (Exception) { return 0; }
		}

		[DllImport("/usr/lib/libSystem.dylib")]
		static extern uint _dyld_image_count();

		[DllImport("/usr/lib/libSystem.dylib")]
		static extern IntPtr _dyld_get_image_name(uint index);

		/// <summary>
		/// Paths of every library loaded into this process. Process.Modules returns only the main
		/// module on macOS - it enumerates nothing - so ask dyld directly there instead.
		/// </summary>
		static List<string> LoadedImagePaths()
		{
			var paths = new List<string>();

			if (HostUtils.RunningOnOSX)
			{
				try
				{
					uint count = _dyld_image_count();
					for (uint i = 0; i < count; i++)
					{
						string name = Marshal.PtrToStringAnsi(_dyld_get_image_name(i));
						if (!string.IsNullOrEmpty(name)) paths.Add(name);
					}
				}
				catch (Exception) { }
				return paths;
			}

			foreach (ProcessModule m in Process.GetCurrentProcess().Modules)
			{
				if (!string.IsNullOrEmpty(m.FileName)) paths.Add(m.FileName);
			}
			return paths;
		}

		/// <summary>
		/// Version of a loaded library. Mach-O images carry no Win32 version resource, so on macOS
		/// fall back to the enclosing bundle's Info.plist, which is where the real version lives.
		/// </summary>
		static string ImageVersion(string path)
		{
			try
			{
				string version = FileVersionInfo.GetVersionInfo(path).FileVersion;
				if (!string.IsNullOrEmpty(version)) return version;
			}
			catch (Exception) { }

			if (!HostUtils.RunningOnOSX) return "?";

			// .../Foo.framework/Versions/A/Foo    -> .../Versions/A/Resources/Info.plist
			// .../Foo.bundle/Contents/MacOS/Foo   -> .../Contents/Info.plist
			try
			{
				for (var dir = new DirectoryInfo(Path.GetDirectoryName(path)); dir != null; dir = dir.Parent)
				{
					foreach (string candidate in new[]
					{
						Path.Combine(dir.FullName, "Resources", "Info.plist"),
						Path.Combine(dir.FullName, "Info.plist"),
					})
					{
						if (!File.Exists(candidate)) continue;
						string plistVersion = PlistString(candidate, "CFBundleShortVersionString")
							?? PlistString(candidate, "CFBundleVersion");
						if (plistVersion != null) return plistVersion;
					}
					// Never walk out of the bundle - the parent's plist describes something else.
					if (dir.Name.EndsWith(".framework", StringComparison.OrdinalIgnoreCase) ||
						dir.Name.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase) ||
						dir.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
					{
						break;
					}
				}
			}
			catch (Exception) { }

			return "?";
		}

		/// <summary>One string value out of an Info.plist, without dragging in a plist parser.</summary>
		static string PlistString(string plistPath, string key)
		{
			try
			{
				string text = File.ReadAllText(plistPath);
				var match = System.Text.RegularExpressions.Regex.Match(text,
					"<key>" + System.Text.RegularExpressions.Regex.Escape(key)
						+ "</key>\\s*<string>([^<]*)</string>",
					System.Text.RegularExpressions.RegexOptions.IgnoreCase);
				if (match.Success && match.Groups[1].Value.Trim().Length > 0)
				{
					return match.Groups[1].Value.Trim();
				}
			}
			catch (Exception) { }
			return null;
		}

		/// <summary>
		/// Which graphics libraries Rhino actually loaded, and their versions. The loaded driver
		/// tells us more than the version WMI reports for the adapter.
		/// </summary>
		static void LoadedGraphicsModules(StringBuilder sb)
		{
			string[] wanted =
			{
				"nvcuda", "nvoptix", "optix", "nvapi", "nvml",
				"amdhip", "hiprtc", "amd_comgr", "atio", "amdxc",
				"OpenCL", "opengl32", "vulkan",
				"ze_loader", "sycl", "pi_level_zero", "igd", "igc",
				// macOS. AGX* is the Apple GPU driver; libccycles is ours.
				"Metal", "AGX", "MTLCompiler", "GPUCompiler",
				"libccycles", "libomp",
			};

			var rows = new List<string>();
			foreach (string path in LoadedImagePaths())
			{
				string name = Path.GetFileNameWithoutExtension(path);
				if (!wanted.Any(w => name.StartsWith(w, StringComparison.OrdinalIgnoreCase))) continue;

				rows.Add(string.Format("  {0,-32} {1,-20} {2}",
					Path.GetFileName(path), ImageVersion(path), path));
			}

			if (rows.Count == 0)
			{
				sb.AppendLine(HostUtils.RunningOnOSX
					? "  (no Metal, Apple GPU driver or Cycles library is loaded)"
					: "  (no CUDA/HIP/OptiX/oneAPI/OpenCL module is loaded)");
				return;
			}
			foreach (string row in rows.OrderBy(r => r)) sb.AppendLine(row);
		}

		/// <summary>RhinoApp.BuildDate is empty in developer builds; fall back to the plug-in file.</summary>
		static string BuildDateText(Assembly plugInAssembly)
		{
			DateTime buildDate = RhinoApp.BuildDate;
			if (buildDate.Year > 1) return buildDate.ToString("u");

			try
			{
				return File.GetLastWriteTimeUtc(plugInAssembly.Location).ToString("u")
					+ " (plug-in file date - Rhino reports no build date, so this is a developer build)";
			}
			catch (Exception)
			{
				return "unknown";
			}
		}

		static void MachineInfo(StringBuilder sb)
		{
			if (HostUtils.RunningOnWindows) WindowsMachineInfo(sb);
			else if (HostUtils.RunningOnOSX) MacMachineInfo(sb);

			sb.AppendLine("OS             " + RuntimeInformation.OSDescription);
			sb.AppendLine("Architecture   " + RuntimeInformation.OSArchitecture
				+ " (process " + RuntimeInformation.ProcessArchitecture + ")");

			bool? remote = IsRemoteSession();
			sb.AppendLine("Remote session " + (remote.HasValue
				? (remote.Value ? "YES" : "No")
				: "unknown on this platform"));

			try
			{
				var id = Rhino.Render.Utilities.DefaultRenderPlugInId;
				var info = Rhino.PlugIns.PlugIn.GetPlugInInfo(id);
				sb.AppendLine("Current render " + (info != null ? info.Name : id.ToString()));
			}
			catch (Exception ex) { sb.AppendLine("Current render <" + ex.GetType().Name + ">"); }
		}

		/// <summary>Customers do set these, and they change how Cycles picks devices.</summary>
		static void RelevantEnvironmentVariables(StringBuilder sb)
		{
			string[] prefixes = { "CYCLES", "CUDA", "HIP", "OPTIX", "OCIO", "NVIDIA", "AMD_", "RHINO" };
			var hits = Environment.GetEnvironmentVariables()
				.Cast<System.Collections.DictionaryEntry>()
				.Where(e => prefixes.Any(p => Convert.ToString(e.Key, CultureInfo.InvariantCulture)
					.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
				.OrderBy(e => Convert.ToString(e.Key, CultureInfo.InvariantCulture))
				.ToList();

			if (hits.Count == 0)
			{
				sb.AppendLine("  (none set)");
				return;
			}
			foreach (var e in hits) sb.AppendLine(string.Format("  {0} = {1}", e.Key, e.Value));
		}

		static string ReadmeText(string stamp)
		{
			var sb = new StringBuilder();
			sb.AppendLine("RhinoCycles support report");
			sb.AppendLine("Created " + stamp + " by the " + CommandName + " command in Rhino.");
			sb.AppendLine();
			sb.AppendLine("WHAT THIS IS");
			sb.AppendLine("  A snapshot of how Rhino's renderer is set up on this machine.");
			sb.AppendLine();
			sb.AppendLine("WHAT IS IN IT");
			sb.AppendLine("  report.txt               Start here. The CHECKS section at the top is the summary.");
			sb.AppendLine("  report.json              The same information for our tools to read.");
			sb.AppendLine("  device-capabilities.txt  What your graphics hardware reports it can do.");
			sb.AppendLine("  kernel-cache.txt         Which render kernels have been compiled and cached.");
			sb.AppendLine("  logs/                    One log per Rhino session. See logs/manifest.txt.");
			sb.AppendLine();
			sb.AppendLine("WHAT IS *NOT* IN IT");
			sb.AppendLine("  None of your models, geometry, materials or textures. No 3dm data at all.");
			sb.AppendLine();
			sb.AppendLine("WHAT IT DOES CONTAIN ABOUT YOU");
			sb.AppendLine("  Your user name, because it appears in folder paths, and Rhino's");
			sb.AppendLine("  command history from this session, which can include the names and paths of");
			sb.AppendLine("  files you had open. If any of that is confidential, say so when you send");
			sb.AppendLine("  this in and we will tell you what to remove - or open report.txt and delete");
			sb.AppendLine("  the 'Command history' section yourself before sending it.");
			return sb.ToString();
		}

		static string Json(string value)
		{
			if (value == null) return "null";
			var sb = new StringBuilder("\"");
			foreach (char c in value)
			{
				switch (c)
				{
					case '"': sb.Append("\\\""); break;
					case '\\': sb.Append("\\\\"); break;
					case '\n': sb.Append("\\n"); break;
					case '\r': sb.Append("\\r"); break;
					case '\t': sb.Append("\\t"); break;
					default:
						if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
						else sb.Append(c);
						break;
				}
			}
			return sb.Append('"').ToString();
		}

		static string Json(bool value) { return value ? "true" : "false"; }

		static string Json(int value) { return value.ToString(CultureInfo.InvariantCulture); }

		/// <summary>Machine-readable twin of report.txt, so we can aggregate across tickets later.</summary>
		static string BuildJson(string stamp, LogStats logs, List<CheckResult> checks)
		{
			var sb = new StringBuilder();
			sb.AppendLine("{");
			sb.AppendLine("  \"generated\": " + Json(stamp) + ",");
			sb.AppendLine("  \"rhino\": {");
			sb.AppendLine("    \"version\": " + Json(RhinoApp.Version.ToString()) + ",");
			sb.AppendLine("    \"installation\": " + Json(RhinoApp.InstallationType.ToString()) + ",");
			sb.AppendLine("    \"rhinoCycles\": " + Json(RhinoBuildConstants.VERSION_STRING) + ",");
			sb.AppendLine("    \"os\": " + Json(RuntimeInformation.OSDescription) + ",");
			sb.AppendLine("    \"architecture\": " + Json(RuntimeInformation.OSArchitecture.ToString()) + ",");
			sb.AppendLine("    \"processors\": " + Json(Environment.ProcessorCount) + ",");
			bool? remoteSession = IsRemoteSession();
			sb.AppendLine("    \"remoteSession\": " + (remoteSession.HasValue ? Json(remoteSession.Value) : "null"));
			sb.AppendLine("  },");

			sb.AppendLine("  \"state\": {");
			sb.AppendLine("    \"initialised\": " + Json(RcCore.It.Initialised) + ",");
			sb.AppendLine("    \"initialisationFailed\": " + Json(RcCore.It.InitialisationFailed) + ",");
			sb.AppendLine("    \"gpusDisabled\": " + Json(RhinoCyclesCore.Utilities.GpusDisabled) + ",");
			sb.AppendLine("    \"compileFinished\": " + Json(RcCore.It.CompileProcessFinished) + ",");
			sb.AppendLine("    \"compileError\": " + Json(RcCore.It.CompileProcessError) + ",");
			sb.AppendLine("    \"verboseLogging\": " + Json(RcCore.It.AllSettings.VerboseLogging));
			sb.AppendLine("  },");

			sb.AppendLine("  \"displayGpus\": [");
			try
			{
				var gpus = DisplayDeviceInfo.GpuDeviceInfos();
				for (int i = 0; i < gpus.Count; i++)
				{
					sb.AppendLine("    { \"name\": " + Json(gpus[i].Name)
						+ ", \"vendor\": " + Json(gpus[i].Vendor)
						+ ", \"memory\": " + Json(gpus[i].MemoryAsString)
						+ ", \"driverDate\": " + Json(gpus[i].DriverDateAsString)
						+ " }" + (i < gpus.Count - 1 ? "," : ""));
				}
			}
			catch (Exception) { }
			sb.AppendLine("  ],");

			sb.AppendLine("  \"cyclesDevices\": [");
			try
			{
				var devices = Device.Devices.ToList();
				for (int i = 0; i < devices.Count; i++)
				{
					sb.AppendLine("    { \"id\": " + Json((int)devices[i].Id)
						+ ", \"name\": " + Json(devices[i].Name)
						+ ", \"description\": " + Json(devices[i].Description)
						+ ", \"type\": " + Json(devices[i].Type.ToString())
						+ ", \"isGpu\": " + Json(devices[i].IsGpu)
						+ " }" + (i < devices.Count - 1 ? "," : ""));
				}
			}
			catch (Exception) { }
			sb.AppendLine("  ],");

			sb.AppendLine("  \"renderDevice\": " + Json(RcCore.It.AllSettings.RenderDevice.Name) + ",");

			sb.AppendLine("  \"checks\": [");
			for (int i = 0; i < checks.Count; i++)
			{
				sb.AppendLine("    { \"kind\": " + Json(checks[i].Kind.ToString().ToUpperInvariant())
					+ ", \"priority\": " + Json(checks[i].Priority)
					+ ", \"label\": " + Json(checks[i].Label)
					+ ", \"text\": " + Json(checks[i].Text)
					+ ", \"reference\": " + Json(checks[i].Reference)
					+ " }" + (i < checks.Count - 1 ? "," : ""));
			}
			sb.AppendLine("  ],");

			sb.AppendLine("  \"verdict\": " + Json(Verdict(checks)) + ",");

			sb.AppendLine("  \"nonDefaultSettings\": [");
			var changed = NonDefaultSettings();
			for (int i = 0; i < changed.Count; i++)
			{
				sb.AppendLine("    " + Json(changed[i].Trim()) + (i < changed.Count - 1 ? "," : ""));
			}
			sb.AppendLine("  ],");

			sb.AppendLine("  \"logs\": {");
			sb.AppendLine("    \"total\": " + Json(logs.Total) + ",");
			sb.AppendLine("    \"busy\": " + Json(logs.Busy) + ",");
			sb.AppendLine("    \"shutdownKnown\": " + Json(logs.ShutdownKnown) + ",");
			sb.AppendLine("    \"unclean\": " + Json(logs.Unclean));
			sb.AppendLine("  }");
			sb.AppendLine("}");
			return sb.ToString();
		}

		/// <summary>The single most likely cause: the worst warning by priority.</summary>
		static string Verdict(List<CheckResult> checks)
		{
			var worst = checks.Where(c => c.Kind == CheckKind.Warn)
				.OrderBy(c => c.Priority)
				.FirstOrDefault();
			return worst == null ? null : worst.Label;
		}

		static string BuildReport(string stamp, LogStats logs, List<CheckResult> checks)
		{
			var sb = new StringBuilder();

			sb.AppendLine("RhinoCycles support report");
			sb.AppendLine("Generated " + stamp);
			sb.AppendLine();
			sb.AppendLine("Read CHECKS first. Everything after it is detail for the developers.");
			sb.AppendLine();

			Section(sb, "CHECKS", () =>
			{
				string verdict = Verdict(checks);
				int warnings = checks.Count(c => c.Kind == CheckKind.Warn);

				sb.AppendLine(verdict == null
					? "MOST LIKELY:  nothing obviously wrong was detected."
					: "MOST LIKELY:  " + verdict + ".");
				sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
					"{0} warning(s), {1} note(s). Warnings first, then what passed.",
					warnings, checks.Count(c => c.Kind == CheckKind.Note)));
				sb.AppendLine();

				// Warnings and notes first, worst-first; the passing checks are reassurance only.
				foreach (var c in checks.Where(x => x.Kind != CheckKind.Ok).OrderBy(x => x.Priority))
				{
					sb.AppendLine(c.ToString());
				}
				sb.AppendLine();
				foreach (var c in checks.Where(x => x.Kind == CheckKind.Ok).OrderBy(x => x.Priority))
				{
					sb.AppendLine(c.ToString());
				}
			});

			Section(sb, "SUMMARY", () =>
			{
				bool? remote = IsRemoteSession();
				sb.AppendLine("Rhino            " + RhinoApp.Version + " " + RhinoApp.InstallationType);
				sb.AppendLine("Remote session   " + (!remote.HasValue
					? "unknown on this platform"
					: remote.Value
						? "YES - a remote desktop session can hide GPUs from Cycles"
						: "No"));

				var types = Device.Devices.Select(d => d.Type.ToString()).Distinct().ToArray();
				sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Cycles devices   {0} ({1})",
					Device.Count, string.Join(", ", types)));
				sb.AppendLine("Render device    " + RcCore.It.AllSettings.RenderDevice.Name);
				sb.AppendLine("GPUs disabled    " + RhinoCyclesCore.Utilities.GpusDisabled);
				sb.AppendLine("Kernel compile   " + (RcCore.It.CompileProcessFinished ? "finished" : "NOT finished")
					+ (RcCore.It.CompileProcessError ? ", WITH ERRORS" : ", no errors"));

				foreach (var gpu in DisplayDeviceInfo.GpuDeviceInfos())
				{
					sb.AppendLine(string.Format("Display GPU      {0} ({1}, driver date {2})",
						gpu.Name, gpu.MemoryAsString, gpu.DriverDateAsString));
				}

				sb.AppendLine("Verbose logging  " + (RcCore.It.AllSettings.VerboseLogging ? "On" : "Off"));
				sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Non-default      {0} setting(s) changed from default",
					NonDefaultSettings().Count));
				sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Session logs     {0} collected, {1} busy, {2} startup-only (see logs/manifest.txt)",
					logs.Total, logs.Busy, logs.Total - logs.Busy));
				sb.AppendLine(logs.ShutdownKnown > 0
					? string.Format(CultureInfo.InvariantCulture, "Clean exits      {0} of {1} recorded sessions did NOT finish shutting down",
						logs.Unclean, logs.ShutdownKnown)
					: RcCore.It.AllSettings.VerboseLogging
						? "Clean exits      unknown - no session recorded a shutdown"
						: "Clean exits      unknown - needs verbose logging");
				sb.AppendLine("Also in this zip README.txt, report.json, device-capabilities.txt, kernel-cache.txt");
			});

			Section(sb, "Versions", () =>
			{
				var rhcyclesAss = Assembly.GetExecutingAssembly();
				var csyclesAss = Assembly.GetAssembly(typeof(Client));
				var csyclesFvi = FileVersionInfo.GetVersionInfo(csyclesAss.Location);
				sb.AppendLine("Rhino          " + RhinoApp.Version + " (" + RhinoApp.InstallationType + ")");
				sb.AppendLine("Build date     " + BuildDateText(rhcyclesAss));
				sb.AppendLine("RhinoCycles    " + RhinoBuildConstants.VERSION_STRING + " @ " + rhcyclesAss.Location);
				sb.AppendLine("CCSycles       " + csyclesFvi.FileVersion + " @ " + csyclesAss.Location);
				sb.AppendLine("OS             " + RuntimeInformation.OSDescription
					+ " (" + RuntimeInformation.OSArchitecture + ")");
				sb.AppendLine("CLR            " + Environment.Version);
				sb.AppendLine("Processors     " + Environment.ProcessorCount);
			});

			// SystemInfo has no managed API and never returns when run via RunScript from inside
			// a command, so gather the display-device facts that matter for GPU cases directly.
			Section(sb, "Machine", () => MachineInfo(sb));

			Section(sb, "Loaded graphics modules", () => LoadedGraphicsModules(sb));

			Section(sb, "Cycles-related environment variables", () => RelevantEnvironmentVariables(sb));

			Section(sb, "Display devices", () =>
			{
				foreach (var gpu in DisplayDeviceInfo.GpuDeviceInfos())
				{
					sb.AppendLine(string.Format("  {0} | {1} | {2} | driver date {3}",
						gpu.Name, gpu.Vendor, gpu.MemoryAsString, gpu.DriverDateAsString));
				}
				sb.AppendLine();
				if (HostUtils.RunningOnWindows)
				{
					WindowsVideoControllers(sb);
				}
				else
				{
					// DisplayDeviceInfo is only fully implemented on Windows, so on macOS the list
					// above may be empty. The Cycles device list below is the reliable one there.
					sb.AppendLine("  (adapter driver details are only available on Windows)");
				}
			});

			Section(sb, "Cycles devices", () =>
			{
				sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} device(s) found by Cycles", Device.Count));
				foreach (var dev in Device.Devices)
				{
					sb.AppendLine(string.Format("  Device {0}: {1} > {2} > {3} | {4} | {5} | GPU: {6}",
						dev.Id, dev.Name, dev.Description, dev.Num, dev.DisplayDevice, dev.Type, dev.IsGpu));
				}
				sb.AppendLine();
				sb.AppendLine("Render device      " + RcCore.It.AllSettings.RenderDevice.Name);
				sb.AppendLine("Selected device    " + RcCore.It.AllSettings.SelectedDeviceStr);
				sb.AppendLine("Override allowed   " + RcCore.It.AllSettings.AllowSelectedDeviceOverride);
				sb.AppendLine();
				sb.AppendLine("Capabilities are in device-capabilities.txt");
			});

			Section(sb, "State", () =>
			{
				sb.AppendLine("Initialised            " + RcCore.It.Initialised);
				sb.AppendLine("Initialisation failed  " + RcCore.It.InitialisationFailed);
				sb.AppendLine("App initialised        " + RcCore.It.AppInitialised);
				sb.AppendLine("GPUs disabled          " + RhinoCyclesCore.Utilities.GpusDisabled);
				sb.AppendLine("Has GPUs               " + RhinoCyclesCore.Utilities.HasGpus);
				sb.AppendLine("Compile finished       " + RcCore.It.CompileProcessFinished);
				sb.AppendLine("Compile error          " + RcCore.It.CompileProcessError);
			});

			Section(sb, "Kernel cache health", () => KernelCacheHealth(sb));

			Section(sb, "GPU kernel compile log", () => sb.AppendLine(RcCore.It.GetFormattedCompileLog()));

			Section(sb, "Non-default settings", () =>
			{
				var changed = NonDefaultSettings();
				if (changed.Count == 0)
				{
					sb.AppendLine("  (everything at its default)");
					return;
				}
				foreach (string line in changed) sb.AppendLine(line);
			});

			Section(sb, "All settings", () =>
			{
				var settings = RcCore.It.AllSettings;
				foreach (var p in SettingProperties(settings))
				{
					string value;
					// Invariant so a comma-decimal locale does not make the numbers ambiguous to whoever reads this.
					try { value = Convert.ToString(p.GetValue(settings, null), CultureInfo.InvariantCulture); }
					catch (Exception ex) { value = "<" + ex.GetType().Name + ">"; }
					sb.AppendLine(string.Format("  {0} = {1}", p.Name, value));
				}
			});

			Section(sb, "Paths", () =>
			{
				sb.AppendLine("Kernel path      " + RcCore.It.KernelPath);
				sb.AppendLine("Kernel relative  " + RcCore.It.KernelPathRelative);
				sb.AppendLine("Plug-in path     " + RcCore.It.PluginPath);
				sb.AppendLine("App path         " + RcCore.It.AppPath);
				sb.AppendLine("User data path   " + RcCore.It.DataUserPath);
				sb.AppendLine("GPU compile path " + RcCore.It.GpuCompilePath);
			});

			Section(sb, "Loaded plug-ins", () =>
			{
				foreach (var kv in Rhino.PlugIns.PlugIn.GetInstalledPlugIns(true).OrderBy(kv => kv.Value))
				{
					var info = Rhino.PlugIns.PlugIn.GetPlugInInfo(kv.Key);
					if (info == null || !info.IsLoaded) continue;
					sb.AppendLine(string.Format("  {0} {1} ({2})", info.Name, info.Version, info.FileName));
				}
			});

			Section(sb, "RhinoCycles log (in memory)", () => sb.AppendLine(RcCore.It.GetLog()));

			Section(sb, "Command history", () => sb.AppendLine(RhinoApp.CommandHistoryWindowText));

			return sb.ToString();
		}

		static void Section(StringBuilder sb, string title, Action body)
		{
			sb.AppendLine("========== " + title + " ==========");
			try { body(); }
			catch (Exception ex) { sb.AppendLine("<failed: " + ex.Message + ">"); }
			sb.AppendLine();
		}
	}
}
