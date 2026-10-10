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

			// The path and date tell the big_libs payload from a local +Cycles build.
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
		/// Report ccycles_payload.json, which publish_payload.ps1 writes beside ccycles.dll.
		/// </summary>
		/// <remarks>
		/// Regular expressions, not a JSON parser: net48 has no System.Text.Json, and a parse
		/// failure only costs a line.
		/// </remarks>
		private static void ReportPayload(string directory)
		{
			if (string.IsNullOrEmpty(directory)) return;

			var manifestPath = Path.Combine(directory, "ccycles_payload.json");
			if (!File.Exists(manifestPath))
			{
				// Not an error: older and hand-assembled payloads have none.
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
