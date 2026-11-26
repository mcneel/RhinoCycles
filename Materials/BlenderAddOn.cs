/**
Copyright 2026 Robert McNeel and Associates

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
using Rhino.Runtime;
using Rhino.UI;
using RhinoCyclesCore.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RhinoCyclesCore.Materials
{
	/// <summary>
	/// The Blender add-on that copies materials to and from Rhino as Node-Based materials. Rhino
	/// ships it as a Blender extension next to RhinoCycles and installs it into the Blender
	/// versions it finds, with Blender's own command line.
	/// </summary>
	public static class BlenderAddOn
	{
		/// <summary>
		/// The extension's id, as in its blender_manifest.toml.
		/// </summary>
		public const string ExtensionId = "rhino_materials";

		/// <summary>
		/// The command that installs it, by its English name.
		/// </summary>
		public const string CommandName = "InstallBlenderAddOn";

		/* The add-on version last offered, so a newer one is offered again. */
		private const string OfferedSetting = "blender-add-on-offered-version";

		/* Blender programs the user chose, which Find includes from then on. */
		private const string ChosenSetting = "blender-programs-chosen";

		/* Extensions, and the command line that installs them, came with Blender 4.2. */
		private static readonly Version MinimumBlender = new Version(4, 2);

		/// <summary>
		/// A Blender installation.
		/// </summary>
		public sealed class Installation
		{
			public Version Version { get; internal set; }

			/// <summary>
			/// The program, which runs without its window to install extensions.
			/// </summary>
			public string Executable { get; internal set; }

			/// <summary>
			/// Where this Blender keeps the extensions its user installs.
			/// </summary>
			public string ExtensionsFolder { get; internal set; }

			public string Label => string.Format(CultureInfo.InvariantCulture, "Blender {0}.{1}", Version.Major, Version.Minor);

			public bool HasAddOn => File.Exists(Path.Combine(ExtensionsFolder, ExtensionId, "blender_manifest.toml"));

			/// <summary>
			/// The version of the add-on this Blender has, or null if it has none.
			/// </summary>
			public Version AddOnVersion => ManifestVersion(Path.Combine(ExtensionsFolder, ExtensionId));

			/// <summary>
			/// Whether this Blender has the add-on at this version or a newer one.
			/// </summary>
			public bool HasAddOnVersion(Version version) => AddOnVersion is Version v && v >= version;
		}

		/// <summary>
		/// The extension as Rhino ships it.
		/// </summary>
		public static string Folder => Path.Combine(RcCore.It.PluginPath ?? string.Empty, "RhinoCycles", "blender", ExtensionId);

		public static bool IsShipped => File.Exists(Path.Combine(Folder, "blender_manifest.toml"));

		/// <summary>
		/// The version of the add-on Rhino ships, or null if it ships none.
		/// </summary>
		public static Version ShippedVersion => ManifestVersion(Folder);

		private static readonly Regex g_manifestVersion = new Regex("^\\s*version\\s*=\\s*\"([^\"]*)\"", RegexOptions.Multiline);

		/* The version in an extension's blender_manifest.toml, or null. */
		private static Version ManifestVersion(string folder)
		{
			try
			{
				var match = g_manifestVersion.Match(File.ReadAllText(Path.Combine(folder, "blender_manifest.toml")));
				return match.Success && Version.TryParse(match.Groups[1].Value, out var version) ? version : null;
			}
			catch (Exception)
			{
				return null;
			}
		}

		/// <summary>
		/// Blender 4.2 and later where they are usually installed, the ones Windows lists as installed
		/// apps or Spotlight knows on a Mac, the one .blend files open with, and the ones the user chose.
		/// One per extensions folder, as copies of a version share it.
		/// </summary>
		public static List<Installation> Find()
		{
			var programs = (HostUtils.RunningOnWindows ? WindowsPrograms() : MacPrograms()).Concat(ChosenPrograms());
			var found = new List<Installation>();
			foreach (var program in programs.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				var blender = FromProgram(program);
				if (blender != null && found.All(b => !string.Equals(b.Executable, blender.Executable, StringComparison.OrdinalIgnoreCase) &&
				                                      !string.Equals(b.ExtensionsFolder, blender.ExtensionsFolder, StringComparison.OrdinalIgnoreCase)))
					found.Add(blender);
			}
			return found.OrderBy(b => b.Version).ToList();
		}

		private static string[] ChosenPrograms()
		{
			return Settings?.GetStringList(ChosenSetting, new string[0]) ?? new string[0];
		}

		/* Keep a Blender the user chose for Find, dropping chosen ones that are gone. */
		private static void Remember(Installation blender)
		{
			var settings = Settings;
			if (settings == null)
				return;

			var chosen = ChosenPrograms()
				.Where(p => File.Exists(p) && !string.Equals(p, blender.Executable, StringComparison.OrdinalIgnoreCase))
				.ToList();
			chosen.Add(blender.Executable);
			settings.SetStringList(ChosenSetting, chosen.ToArray());
		}

		/// <summary>
		/// The Blender a program belongs to: blender.exe, Blender.app or the program in it. Null if
		/// it is not Blender 4.2 or later.
		/// </summary>
		public static Installation FromProgram(string program)
		{
			program = program?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			if (string.IsNullOrEmpty(program))
				return null;

			if (program.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(program))
				program = Path.Combine(program, "Contents", "MacOS", "Blender");
			if (!File.Exists(program))
				return null;

			var version = ProgramVersion(program);
			if (version == null || version < MinimumBlender)
				return null;

			return new Installation
			{
				Version = version,
				Executable = program,
				ExtensionsFolder = Path.Combine(UserResources(program, version), "extensions", "user_default"),
			};
		}

		private static IEnumerable<string> WindowsPrograms()
		{
			var roots = new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 }
				.Select(Environment.GetFolderPath).Where(r => !string.IsNullOrEmpty(r)).Distinct();
			foreach (var root in roots)
			{
				var foundation = Path.Combine(root, "Blender Foundation");
				if (Directory.Exists(foundation))
					foreach (var folder in Directory.GetDirectories(foundation))
						yield return Path.Combine(folder, "blender.exe");
				yield return Path.Combine(root, "Steam", "steamapps", "common", "Blender", "blender.exe");
			}

			foreach (var folder in InstalledAppFolders())
				yield return Path.Combine(folder, "blender.exe");

			// The Blender .blend files open with, which can be a copy unpacked anywhere.
			string command = null;
			try
			{
				using (var key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"blendfile\shell\open\command"))
					command = key?.GetValue(null) as string;
			}
			catch (Exception)
			{
			}
			var match = Regex.Match(command ?? string.Empty, "^\\s*\"([^\"]+)\"|^\\s*(\\S+)");
			var launcher = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
			if (!string.IsNullOrEmpty(launcher))
				yield return Path.Combine(Path.GetDirectoryName(launcher) ?? string.Empty, "blender.exe");
		}

		/// <summary>
		/// The folders of the Blenders in Windows' list of installed apps, wherever they were installed,
		/// published by the Blender Foundation.
		/// </summary>
		private static List<string> InstalledAppFolders()
		{
			var folders = new List<string>();
			var lists = new[]
			{
				(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64),
				(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry32),
				(Microsoft.Win32.RegistryHive.CurrentUser, Microsoft.Win32.RegistryView.Default),
			};
			foreach (var (hive, view) in lists)
			{
				try
				{
					using (var root = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, view))
					using (var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
					{
						if (uninstall == null)
							continue;
						foreach (var name in uninstall.GetSubKeyNames())
						{
							using (var app = uninstall.OpenSubKey(name))
							{
								var display = app?.GetValue("DisplayName") as string;
								var publisher = app?.GetValue("Publisher") as string;
								var location = (app?.GetValue("InstallLocation") as string)?.Trim().Trim('"');
								if (!string.IsNullOrEmpty(location) && display != null && display.IndexOf("Blender", StringComparison.OrdinalIgnoreCase) >= 0 &&
								    publisher != null && publisher.IndexOf("Blender Foundation", StringComparison.OrdinalIgnoreCase) >= 0)
									folders.Add(location);
							}
						}
					}
				}
				catch (Exception)
				{
				}
			}
			return folders;
		}

		private static IEnumerable<string> MacPrograms()
		{
			var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			foreach (var folder in new[] { "/Applications", Path.Combine(home, "Applications") })
			{
				if (!Directory.Exists(folder))
					continue;
				foreach (var app in Directory.GetDirectories(folder, "Blender*.app"))
					yield return Path.Combine(app, "Contents", "MacOS", "Blender");
			}
			yield return Path.Combine(home, "Library", "Application Support", "Steam", "steamapps", "common", "Blender", "Blender.app", "Contents", "MacOS", "Blender");

			foreach (var app in SpotlightApps("org.blenderfoundation.blender"))
				yield return Path.Combine(app, "Contents", "MacOS", "Blender");
		}

		/// <summary>
		/// The apps with this bundle id that Spotlight knows of, wherever they are; none if it cannot say.
		/// </summary>
		private static List<string> SpotlightApps(string bundleId)
		{
			var start = new ProcessStartInfo("/usr/bin/mdfind", string.Format(CultureInfo.InvariantCulture, "\"kMDItemCFBundleIdentifier == '{0}'\"", bundleId))
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			try
			{
				using (var process = Process.Start(start))
				{
					var output = process.StandardOutput.ReadToEndAsync();
					_ = process.StandardError.ReadToEndAsync();
					if (!process.WaitForExit(5000))
					{
						try { process.Kill(); } catch (Exception) { }
						return new List<string>();
					}

					return output.Result.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(l => l.Trim())
						.Where(l => l.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && !l.Contains("/.Trash/"))
						.ToList();
				}
			}
			catch (Exception)
			{
				return new List<string>();
			}
		}

		/// <summary>
		/// The version of a Blender program: the folder of its scripts and data, named for it, or
		/// else the version in the program file.
		/// </summary>
		private static Version ProgramVersion(string program)
		{
			var folder = Path.GetDirectoryName(program) ?? string.Empty;
			var dataParent = program.EndsWith(Path.Combine("Contents", "MacOS", "Blender"), StringComparison.Ordinal)
				? Path.Combine(folder, "..", "Resources")
				: folder;
			if (Directory.Exists(dataParent))
			{
				var named = Directory.GetDirectories(dataParent)
					.Select(Path.GetFileName)
					.Where(n => Regex.IsMatch(n, "^\\d+\\.\\d+$"))
					.Select(n => Version.TryParse(n, out var v) ? v : null)
					.Where(v => v != null)
					.OrderByDescending(v => v)
					.FirstOrDefault();
				if (named != null)
					return named;
			}

			try
			{
				var text = FileVersionInfo.GetVersionInfo(program).ProductVersion ?? string.Empty;
				var match = Regex.Match(text, "^(\\d+)\\.(\\d+)");
				if (match.Success)
					return new Version(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
			}
			catch (Exception)
			{
			}
			return null;
		}

		/// <summary>
		/// Where a Blender keeps its user's settings and extensions, as Blender finds it.
		/// </summary>
		private static string UserResources(string program, Version version)
		{
			var overridden = Environment.GetEnvironmentVariable("BLENDER_USER_RESOURCES");
			if (!string.IsNullOrEmpty(overridden))
				return overridden;

			var portable = Path.Combine(Path.GetDirectoryName(program) ?? string.Empty, "portable");
			if (HostUtils.RunningOnWindows && Directory.Exists(portable))
				return portable;

			var versionFolder = string.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
			return HostUtils.RunningOnWindows
				? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Blender Foundation", "Blender", versionFolder)
				: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Blender", versionFolder);
		}

		private static PersistentSettings Settings => Rhino.PlugIns.PlugIn.Find(new Guid("9BC28E9E-7A6C-4B8F-A0C6-3D05E02D1B97"))?.Settings;

		/// <summary>
		/// Offer the add-on when the Materials panel opens and Blender is installed: once, to the
		/// Blenders without it, then again only when Rhino ships a newer version, to the Blenders
		/// with an older one. Not asked again for a version, whichever the answer.
		/// </summary>
		public static void OfferOnce()
		{
			var settings = Settings;
			var shipped = ShippedVersion;
			if (settings == null || shipped == null)
				return;

			var offered = Version.TryParse(settings.GetString(OfferedSetting, string.Empty), out var last) ? last : null;
			if (offered != null && offered >= shipped)
				return;

			var found = Find();
			if (found.Count == 0)
				return;

			settings.SetString(OfferedSetting, shipped.ToString());

			string question;
			List<Installation> blenders;
			if (offered == null)
			{
				blenders = found.Where(b => !b.HasAddOnVersion(shipped)).ToList();
				question = string.Format(LOC.STR("Rhino has an add-on for Blender that copies Blender materials to Rhino as Node-Based materials, and back. Install it into {0}?{1}{1}You can also install it later with the {2} command, or from the Nodes section of a Node-Based material."),
					Labels(blenders), Environment.NewLine, LOC.COMMANDNAME("InstallBlenderAddOn"));
			}
			else
			{
				blenders = found.Where(b => b.HasAddOn && !b.HasAddOnVersion(shipped)).ToList();
				question = string.Format(LOC.STR("This Rhino has a newer version of the Blender add-on that copies materials to and from Rhino. Update it in {0}?"), Labels(blenders));
			}
			if (blenders.Count == 0)
				return;

			if (Dialogs.ShowMessage(question, Title, ShowMessageButton.YesNo, ShowMessageIcon.Question) == ShowMessageResult.Yes)
				Install(blenders);
		}

		/// <summary>
		/// Install or update the add-on in the Blenders found, asking first; or in a Blender program
		/// the user chooses, if none is found or they would rather.
		/// </summary>
		public static void InstallInteractively()
		{
			if (RefusedWhileInstalling())
				return;

			if (!IsShipped)
			{
				Dialogs.ShowMessage(LOC.STR("The Blender add-on is not part of this Rhino installation."), Title);
				return;
			}

			var found = Find();
			if (found.Count > 0)
			{
				var question = string.Format(LOC.STR("Install or update the Rhino Materials add-on in {0}?{1}{1}Choose No to pick another Blender program instead."), Labels(found), Environment.NewLine);
				var answer = Dialogs.ShowMessage(question, Title, ShowMessageButton.YesNoCancel, ShowMessageIcon.Question);
				if (answer == ShowMessageResult.Yes)
				{
					Install(found);
					return;
				}
				if (answer != ShowMessageResult.No)
					return;
			}

			var chosen = ChooseProgram(found.Count == 0);
			if (chosen != null)
				Install(new[] { chosen });
		}

		private static Installation ChooseProgram(bool noneFound)
		{
			if (noneFound)
				Dialogs.ShowMessage(LOC.STR("No Blender 4.2 or later was found where Blender is usually installed. Choose the Blender program to install the add-on into."), Title);

			var dialog = new Rhino.UI.OpenFileDialog { Title = LOC.STR("Choose the Blender program") };
			if (HostUtils.RunningOnWindows)
				dialog.Filter = LOC.STR("Blender") + " (blender.exe)|blender.exe";
			else
				dialog.InitialDirectory = "/Applications";
			if (!dialog.ShowOpenDialog())
				return null;

			var blender = FromProgram(dialog.FileName);
			if (blender == null)
				Dialogs.ShowMessage(string.Format(LOC.STR("{0} is not Blender 4.2 or later."), dialog.FileName), Title);
			else
				Remember(blender);
			return blender;
		}

		/// <summary>
		/// Install the add-on into Blenders, in the background, then say how it went.
		/// </summary>
		public static void Install(IEnumerable<Installation> blenders)
		{
			if (RefusedWhileInstalling())
				return;

			var list = blenders.ToList();
			if (!ClosedForFirstInstall(list))
				return;

			string package;
			try
			{
				package = Package();
			}
			catch (Exception ex)
			{
				Dialogs.ShowMessage(string.Format(LOC.STR("Could not prepare the Blender add-on: {0}"), ex.Message), Title);
				return;
			}

			RhinoApp.WriteLine(string.Format(LOC.STR("Installing the Blender add-on into {0}..."), Labels(list)));
			g_installing = true;
			Task.Run(() => list.Select(b => (Blender: b, Error: InstallInto(b, package))).ToList()).ContinueWith(task =>
			{
				try { File.Delete(package); } catch (Exception) { }
				var results = task.Status == TaskStatus.RanToCompletion ? task.Result : list.Select(b => (Blender: b, Error: task.Exception?.InnerException?.Message)).ToList();
				RhinoApp.InvokeOnUiThread(new Action(() =>
				{
					g_installing = false;
					Report(results);
				}));
			});
		}

		/* An install running in the background, whose hidden Blender would pass for an open one.
		 * Only the UI thread reads and sets it. */
		private static bool g_installing;

		private static bool RefusedWhileInstalling()
		{
			// Read once: the install can finish while the message is up.
			var installing = g_installing;
			if (installing)
				Dialogs.ShowMessage(LOC.STR("The Blender add-on is being installed already."), Title);
			return installing;
		}

		/// <summary>
		/// Have the Blenders that get the add-on for the first time closed first: one that is open
		/// saves its preferences, without the add-on, over the install's when it closes with changed
		/// preferences, which switches the add-on off. False if the user cancels.
		/// </summary>
		private static bool ClosedForFirstInstall(List<Installation> blenders)
		{
			var open = blenders.Where(b => !b.HasAddOn && IsOpen(b)).ToList();
			while (open.Count > 0)
			{
				var message = string.Format(LOC.STR("Close {0}, then choose Retry. Installed while Blender is open, the add-on can be switched off again when Blender closes."), Labels(open));
				if (Dialogs.ShowMessage(message, Title, ShowMessageButton.RetryCancel, ShowMessageIcon.Warning) != ShowMessageResult.Retry)
					return false;
				open = open.Where(IsOpen).ToList();
			}
			return true;
		}

		private static bool IsOpen(Installation blender)
		{
			var program = Path.GetFullPath(blender.Executable);
			var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(program));
			try
			{
				return processes.Any(p =>
				{
					try
					{
						var running = p.MainModule?.FileName;
						return running != null && string.Equals(Path.GetFullPath(running), program, StringComparison.OrdinalIgnoreCase);
					}
					catch (Exception)
					{
						return false;
					}
				});
			}
			finally
			{
				foreach (var p in processes)
					p.Dispose();
			}
		}

		private static void Report(List<(Installation Blender, string Error)> results)
		{
			var lines = new List<string>();
			var installed = results.Where(r => r.Error == null).Select(r => r.Blender).ToList();
			if (installed.Count > 0)
				lines.Add(string.Format(LOC.STR("Installed the Rhino Materials add-on into {0}. In Blender, Copy as Rhino Material and Paste Rhino Material are in the material menu, the down arrow beside the material slots, and in the Shader Editor's right-click menu. Restart Blender if it is open."), Labels(installed)));
			foreach (var (blender, error) in results.Where(r => r.Error != null))
				lines.Add(string.Format(LOC.STR("Could not install it into {0}: {1}"), blender.Label, error));

			var message = string.Join(Environment.NewLine + Environment.NewLine, lines);
			RhinoApp.WriteLine(message);
			Dialogs.ShowMessage(message, Title, ShowMessageButton.OK, installed.Count == results.Count ? ShowMessageIcon.Information : ShowMessageIcon.Warning);
		}

		/// <summary>
		/// The extension as a zip in the temporary folder, its manifest at the top, as Blender installs it.
		/// </summary>
		private static string Package()
		{
			var zip = Path.Combine(Path.GetTempPath(), string.Format(CultureInfo.InvariantCulture, "{0}-{1}.zip", ExtensionId, Guid.NewGuid().ToString("N")));
			try
			{
				using (var stream = File.Create(zip))
				using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
				{
					foreach (var file in Directory.GetFiles(Folder, "*", SearchOption.AllDirectories))
					{
						var relative = file.Substring(Folder.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
						if (relative.Split('/').Contains("__pycache__"))
							continue;
						using (var source = File.OpenRead(file))
						using (var target = archive.CreateEntry(relative).Open())
							source.CopyTo(target);
					}
				}
			}
			catch
			{
				// Not a half-written zip left in the temporary folder.
				try { File.Delete(zip); } catch (Exception) { }
				throw;
			}
			return zip;
		}

		/// <summary>
		/// Install the package into one Blender and switch it on; null if that worked, else why not.
		/// </summary>
		private static string InstallInto(Installation blender, string package)
		{
			var start = new ProcessStartInfo(blender.Executable)
			{
				Arguments = string.Format(CultureInfo.InvariantCulture, "-c extension install-file -r user_default -e \"{0}\"", package),
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};
			try
			{
				using (var process = Process.Start(start))
				{
					var output = process.StandardOutput.ReadToEndAsync();
					var errors = process.StandardError.ReadToEndAsync();
					if (!process.WaitForExit(120000))
					{
						try { process.Kill(); } catch (Exception) { }
						return LOC.STR("Blender did not finish within two minutes.");
					}

					if (process.ExitCode == 0 && blender.HasAddOn)
						return null;

					var said = (errors.Result + Environment.NewLine + output.Result)
						.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
					return said.Count > 0 ? said.Last() : string.Format(LOC.STR("Blender stopped with code {0}."), process.ExitCode);
				}
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
		}

		private static string Title => LOC.STR("Blender Add-on");

		private static string Labels(IEnumerable<Installation> blenders)
		{
			return string.Join(", ", blenders.Select(b => b.Label));
		}
	}
}
