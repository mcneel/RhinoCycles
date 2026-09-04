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
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace RhinoCycles.Commands
{
	[Guid("9e91d7ea-7990-471f-a944-ad9ececcc88b")]
	[CommandStyle(Style.Hidden)]
	public class ListDevices : Command
	{
		static ListDevices _instance;
		public ListDevices()
		{
			_instance = this;
		}

		///<summary>The only instance of the ListDevices command.</summary>
		public static ListDevices Instance => _instance;

		public override string EnglishName => "RhinoCycles_ListDevices";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			(PlugIn as Plugin)?.InitialiseCSycles();

			// Which ccycles.dll am I actually running? There are two ways to get
			// one - the prebuilt payload from big_libs, or a local +Cycles build -
			// and they are indistinguishable without asking. The Cycles version
			// says what the source was; the file date and path say which copy.
			RhinoApp.WriteLine($"Cycles {CSycles.version_string()}");
			var ccyclesPath = Path.Combine(
				Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
				"ccycles.dll");
			if (File.Exists(ccyclesPath))
			{
				RhinoApp.WriteLine($"	{ccyclesPath}");
				RhinoApp.WriteLine($"	built {File.GetLastWriteTime(ccyclesPath):yyyy-MM-dd HH:mm}");
			}

			ReportPayload(Path.GetDirectoryName(ccyclesPath));
			RhinoApp.WriteLine("----------");

			var numDevices = Device.Count;
			var endS = numDevices != 1 ? "s" : "";
			RhinoApp.WriteLine($"We have {numDevices} device{endS}");
			RhinoApp.WriteLine("----------");
			foreach (var dev in Device.Devices)
			{
				RhinoApp.WriteLine($"	Device {dev.Id}: {dev.Name} > {dev.Description} > {dev.Num} | {dev.DisplayDevice} | {dev.Type} | GPU: {dev.IsGpu}");
			}
			RhinoApp.WriteLine("----------");
			return Result.Success;
		}

		/// <summary>
		/// Report what the deployed Cycles payload says about itself.
		/// </summary>
		/// <remarks>
		/// publish_payload.ps1 writes ccycles_payload.json beside ccycles.dll and the
		/// build copies it into the plug-in output, so the answer to "what am I running"
		/// travels with the binaries. Without this it was only answerable by finding the
		/// file on disk, which nobody does while wondering why a render looks wrong.
		///
		/// Most useful when it disagrees with expectations: a payload whose kernel source
		/// hash differs from the tree was built from different kernel code, and a payload
		/// built from a dirty tree cannot be reproduced from the commit it names.
		///
		/// Read with regular expressions rather than a JSON parser on purpose. This
		/// assembly targets net48, where System.Text.Json is not in the box, and a NuGet
		/// dependency for one diagnostic command is a poor trade. The file is ours, small
		/// and predictable; a parse failure here degrades to a missing line, never to a
		/// failed command.
		/// </remarks>
		private static void ReportPayload(string directory)
		{
			if (string.IsNullOrEmpty(directory)) return;

			var manifestPath = Path.Combine(directory, "ccycles_payload.json");
			if (!File.Exists(manifestPath))
			{
				// Not an error. A payload published before manifests existed has none, and
				// neither does one assembled by hand.
				RhinoApp.WriteLine("	payload   no manifest - cannot say what this payload contains");
				return;
			}

			string json;
			try
			{
				json = File.ReadAllText(manifestPath);
			}
			catch (IOException)
			{
				return;
			}

			string Scalar(string name)
			{
				var m = Regex.Match(json, "\"" + name + "\"\\s*:\\s*\"([^\"]*)\"");
				return m.Success ? m.Groups[1].Value : null;
			}

			int Count(string name)
			{
				var block = Regex.Match(json, "\"" + name + "\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
				return block.Success ? Regex.Matches(block.Groups[1].Value, "\"[^\"]+\"").Count : 0;
			}

			var configuration = Scalar("configuration") ?? "unknown";
			var built = Scalar("builtUtc");
			if (built != null && built.Length >= 16) built = built.Substring(0, 16).Replace("T", " ") + " UTC";

			RhinoApp.WriteLine($"	payload   {configuration}{(built != null ? ", built " + built : "")}");

			var commit = Scalar("commit");
			if (commit != null)
			{
				var branch = Scalar("branch");
				var dirty = Regex.Match(json, "\"dirty\"\\s*:\\s*true").Success;
				RhinoApp.WriteLine($"	source    {commit}{(branch != null ? " on " + branch : "")}{(dirty ? " (dirty tree - not reproducible from this commit)" : "")}");
			}

			RhinoApp.WriteLine($"	kernels   {Count("hip")} HIP, {Count("cuda")} CUDA, {Count("optix")} OptiX");

			var hash = Scalar("kernelSourceHash");
			if (hash != null && hash.Length >= 16)
			{
				RhinoApp.WriteLine($"	sources   {hash.Substring(0, 16)}");
			}
		}
	}
}
