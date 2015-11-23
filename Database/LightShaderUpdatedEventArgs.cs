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
using CclShader = ccl.Shader;

namespace RhinoCycles.Database
{
	public class LightShaderUpdatedEventArgs : EventArgs
	{
		/// <summary>
		/// Intermediate RhinoCycles Light shader
		/// </summary>
		public CyclesLight RcLightShader { get; private set; }
		/// <summary>
		/// Cycles shader
		/// </summary>
		public CclShader CclShader { get; private set; }

		public LightShaderUpdatedEventArgs(CyclesLight rcLightShader, CclShader cclShader)
		{
			RcLightShader = rcLightShader;
			CclShader = cclShader;
		}
	}
}
