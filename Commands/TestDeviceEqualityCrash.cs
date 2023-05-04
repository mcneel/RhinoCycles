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

using System.Runtime.InteropServices;
using ccl;
using Rhino;
using Rhino.Commands;

namespace RhinoCycles.Commands
{
	[Guid("701e9844-10c5-4891-ade0-a18bceea550d")]
	[CommandStyle(Style.Hidden)]
	public class TestDeviceEqualityCrash : Command
	{
		static TestDeviceEqualityCrash _instance;
		public TestDeviceEqualityCrash()
		{
			_instance = this;
		}

		///<summary>The only instance of the TestDeviceEqualityCrash command.</summary>
		public static TestDeviceEqualityCrash Instance => _instance;

		public override string EnglishName => "TestDeviceEqualityCrash";

		protected override Result RunCommand(RhinoDoc doc, RunMode mode)
		{
			Device a = null;
			try
			{
				if (a == null) RhinoApp.WriteLine("Device null equals null. RH-43880 is fixed.");
			} catch (System.NullReferenceException nre)
			{
				RhinoApp.WriteLine($"Device::operator==() not fixed properly. RH-43880 not fixed. Report to nathan@mcneel.com: {nre}");
			}
			return Result.Success;
		}
	}
}
