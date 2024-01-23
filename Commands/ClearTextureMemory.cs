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

namespace RhinoCycles.Commands
{
	[System.Runtime.InteropServices.Guid("b1a17785-71a3-4194-aaf7-fccac37ef716")]
	[CommandStyle(Style.Hidden)]
	public class ClearTextureMemory : Command
	{
		static ClearTextureMemory _instance;
		public ClearTextureMemory()
		{
			if(_instance==null) _instance = this;
		}

		///<summary>The only instance of the ClearTextureMemory command.</summary>
		public static ClearTextureMemory Instance => _instance;

		public override string EnglishName => "RhinoCycles_ClearTextureMemory";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			// BitmapConverter moved into RenderEngine as instance. When render engine
			// gets disposed the BitmapConverter and its dictionaries get removed. No
			// longer necessary to do that here.
			//RhinoCyclesCore.Converters.BitmapConverter.ClearTextureMemory();
			return Result.Success;
		}
	}
}
