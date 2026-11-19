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
using Rhino.Commands;
using Rhino.UI;
using RhinoCyclesCore.Materials;
using System.Runtime.InteropServices;

namespace RhinoCycles.Commands
{
	/// <summary>
	/// Install or update the Blender add-on that copies materials to and from Rhino.
	/// </summary>
	[Guid("6E128F15-7803-43D3-9F4F-897EAF90B103")]
	public class InstallBlenderAddOn : Command
	{
		public override string LocalName => LOC.COMMANDNAME(BlenderAddOn.CommandName);

		public override string EnglishName => BlenderAddOn.CommandName;

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			BlenderAddOn.InstallInteractively();
			return Result.Success;
		}
	}
}
