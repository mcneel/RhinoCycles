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
using RhinoCyclesCore;

namespace RhinoCycles.Commands
{

	[System.Runtime.InteropServices.Guid("6e775e71-1006-463b-83e2-627a137f347d")]
	[CommandStyle(Style.Hidden)]
	public class TestToggleIsClippingObject : Command
	{
		static TestToggleIsClippingObject _instance;
		public TestToggleIsClippingObject()
		{
			if(_instance==null) _instance = this;
		}

		///<summary>The only instance of the SetBumpStrength command.</summary>
		public static TestToggleIsClippingObject Instance => _instance;

		public override string EnglishName => "TestToggleIsClippingObject";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var getObject = new GetObject();
			getObject.SetCommandPrompt("Pick objects to toggle IsClippingObject status for");
			var getRc = getObject.Get();
			if (getObject.CommandResult() != Result.Success) return getObject.CommandResult();
			if (getRc == GetResult.Object)
			{
				foreach (var o in getObject.Objects())
				{
					var roa = o.Object().Attributes;
					RhinoCyclesData ud = null;
					if (roa!=null && roa.HasUserData)
					{
						ud = roa.UserData.Find(typeof(RhinoCyclesData)) as RhinoCyclesData;
					}
					if (ud == null) ud = new RhinoCyclesData();
					else ud.ToggleIsClippingObject();

					RhinoApp.WriteLine($"The clipping object status for {o.Object().Name} is now {ud.IsClippingObject}");
					roa.UserData.Add(ud);
				}
				return Result.Success;
			}

			return Result.Nothing;
		}
	}
}
