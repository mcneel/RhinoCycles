/**
Copyright 2014-2015 Robert McNeel and Associates

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
using RhinoCyclesCore.Core;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using Rhino.DocObjects.Custom;

namespace RhinoCycles.Commands
{
	[System.Runtime.InteropServices.Guid ("E18E7D7B-D7C2-491A-B791-7259F0C5F210")]
	public class RcTestUserData : UserDictionary
	{
		public RcTestUserData()
		{
			var r = new System.Random();
			Number = r.Next(10, 500);
			Enabled = Enabled;
		}

		protected override void OnDuplicate(UserData source)
		{
			if(source is RcTestUserData s)
			{
				Enabled = s.Enabled;
				Number = s.Number;
			}
		}


		public bool EnabledDefault => true;
		public bool Enabled
		{
			get { return Dictionary.GetBool("Enabled", EnabledDefault); }
			set { Dictionary.Set("Enabled", value); }
		}
		public int Number
		{
			get { return Dictionary.GetInteger("Number", 0); }
			set { Dictionary.Set("Number", value); }
		}
	}
	[System.Runtime.InteropServices.Guid("fbce78a4-0ae9-41e4-aade-ff4b9ccb1b64")]
	[CommandStyle(Style.Hidden)]
	public class TestAddUserData : Command
	{
		static TestAddUserData _instance;
		public TestAddUserData()
		{
			if(_instance==null) _instance = this;
		}

		///<summary>The only instance of the SetBumpStrength command.</summary>
		public static TestAddUserData Instance => _instance;

		public override string EnglishName => "TestAddUserData";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			var getObject = new GetObject();
			getObject.SetCommandPrompt($"Pick Object To Improve");
			var getRc = getObject.Get();
			if (getObject.CommandResult() != Result.Success) return getObject.CommandResult();
			if (getRc == GetResult.Object)
			{
				//vpi.UserData.Add(nvud);
				foreach (var o in getObject.Objects())
				{
					var nud = new RcTestUserData();
					o.Object().Attributes.UserData.Add(nud);
				}
				return Result.Success;
			}

			return Result.Nothing;
		}
	}
}
