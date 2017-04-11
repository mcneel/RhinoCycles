/**
Copyright 2014-2017 Robert McNeel and Associates

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

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RhinoCyclesCore.Core
{
	public sealed class RcCore
	{
		#region helper functions to get relative path between two paths
		private const int FileAttributeDirectory = 0x10;
		public static string GetRelativePath(string fromPath, string toPath)
		{

			var path = new StringBuilder();
			if (PathRelativePathTo(path,
				fromPath, FileAttributeDirectory,
				toPath, FileAttributeDirectory) == 0)
			{
				throw new ArgumentException("Paths must have a common prefix");
			}
			return path.ToString();
		}

		[DllImport("shlwapi.dll", SetLastError = true)]
		private static extern int PathRelativePathTo(StringBuilder pszPath,
				string pszFrom, int dwAttrFrom, string pszTo, int dwAttrTo);
		#endregion


		public void TriggerInitialisationCompleted(object sender)
		{
			InitialisationCompleted?.Invoke(sender, EventArgs.Empty);
		}

		/// <summary>
		/// Event signalling that CCSycles initialisation has been completed.
		/// </summary>
		public event EventHandler InitialisationCompleted;
		/// <summary>
		/// Flag to keep track of CSycles initialisation
		/// </summary>
		public bool Initialised { get; set; }

		public bool AppInitialised { get; set; }

		/// <summary>
		/// Get the path used to look up .cubins (absolute)
		/// </summary>
		public string KernelPath { get; set; }

		/// <summary>
		/// Get the path where runtime created data like compiled kernels and BVH caches are stored.
		/// </summary>
		public string DataUserPath { get; set; }

		/// <summary>
		/// Get the path used to look up .cubins (relative)
		/// </summary>
		public string KernelPathRelative { get; set; }

		public string PluginPath { get; set; }

		public string AppPath { get; set; }

		public EngineSettings EngineSettings => _engineSettings;

		private readonly EngineSettings _engineSettings;
		private RcCore() {
			AppInitialised = false;
			if(_engineSettings == null)
				_engineSettings = new EngineSettings();
		}

		public static RcCore It { get; } = new RcCore();
	}
}
