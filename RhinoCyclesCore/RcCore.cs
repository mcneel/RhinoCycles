/**
Copyright 2014-2016 Robert McNeel and Associates

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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RhinoCyclesCore
{
	public sealed class RcCore
	{
		#region helper functions to get relative path between two paths
		private const int FILE_ATTRIBUTE_DIRECTORY = 0x10;
		public static string GetRelativePath(string fromPath, string toPath)
		{

			var path = new StringBuilder();
			if (PathRelativePathTo(path,
				fromPath, FILE_ATTRIBUTE_DIRECTORY,
				toPath, FILE_ATTRIBUTE_DIRECTORY) == 0)
			{
				throw new ArgumentException("Paths must have a common prefix");
			}
			return path.ToString();
		}

		[DllImport("shlwapi.dll", SetLastError = true)]
		private static extern int PathRelativePathTo(StringBuilder pszPath,
				string pszFrom, int dwAttrFrom, string pszTo, int dwAttrTo);
		#endregion

		/// <summary>
		/// Flag to keep track of CSycles initialisation
		/// </summary>
		public bool Initialised { get; set; }

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

		public EngineSettings EngineSettings { get; set; }

		private static readonly RcCore instance = new RcCore();

		private RcCore() {
			EngineSettings = new EngineSettings();
		}

		public static RcCore It
		{
			get
			{
				return instance;
			}
		}
	}
}
