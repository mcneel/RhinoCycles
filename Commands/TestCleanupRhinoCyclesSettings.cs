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
using System.Linq;

namespace RhinoCycles.Commands
{
	[System.Runtime.InteropServices.Guid("f38da37f-5f7f-4fee-b6c3-f6586507ca12")]
	[CommandStyle(Style.Hidden)]
	public class TestCleanupRhinoCyclesSettings : Command
	{
		static TestCleanupRhinoCyclesSettings _instance;
		public TestCleanupRhinoCyclesSettings()
		{
			if(_instance==null) _instance = this;
		}

		///<summary>The only instance of the TestCleanupRhinoCyclesSettings command.</summary>
		public static TestCleanupRhinoCyclesSettings Instance => _instance;

		public override string EnglishName => "TestCleanupRhinoCyclesSettings";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var ps = PlugIn.Settings.Keys;
			var lconly = (from s in ps where s.ToLowerInvariant().Equals(s) select s).ToList();
			foreach(var lc in lconly)
			{
				PlugIn.Settings.DeleteItem(lc);
			}
			PlugIn.SaveSettings();
			return Result.Nothing;
		}
	}
}
