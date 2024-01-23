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

using ccl.ShaderNodes.Sockets;

namespace RhinoCyclesCore.Materials
{
	public interface ICyclesMaterial
	{
		/// <summary>
		/// Get the material type for the implementation
		/// </summary>
		ShaderBody.CyclesMaterial MaterialType { get; }

		/// <summary>
		/// Bake parameters from Fields dictionary. Implement this if you
		/// need access to your own custom render material Fields dictionary
		/// after a flush.
		/// </summary>
		void BakeParameters(Converters.BitmapConverter bitmapConverter, uint docsrn);

		/// <summary>
		/// Get the XML representing the material. Note that if you need to access
		/// this after a Flush from the ChangeQueue you need to call BakeParameters()
		/// on your custom render material first.
		/// </summary>
		string MaterialXml { get; }

		/// <summary>
		/// Get the pre-made shader tree. Used by custom Cycles material implementations that don't use XML.
		/// </summary>
		/// <param name="sh">Shader to fill</param>
		/// <param name="finalize">Pass true if the shader should be finalized</param>
		/// <returns>true when ok.</returns>
		bool GetShader(ccl.Shader sh, bool finalize);

		/// <summary>
		/// Get the closure that should go into the Output if this is the only material.
		/// </summary>
		/// <param name="sh">CCL shader used.</param>
		/// <returns>Closure socket.</returns>
		ClosureSocket GetClosureSocket(ccl.Shader sh);

		/// <summary>
		/// Set the gamma to use when serializing to XML.
		/// </summary>
		float Gamma { get; set; }

		/// <summary>
		/// Get and set the Bitmap to be used internally when needed
		/// </summary>
		Converters.BitmapConverter BitmapConverter { get; set; }
	}
}
