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
using ccl;
using Light = Rhino.Render.ChangeQueue.Light;

namespace RhinoCyclesCore
{
	/// <summary>
	/// Intermediate class used for converting rhino light sources 
	/// to Cycles light sources.
	/// </summary>
	public class CyclesLight
	{
		public Light.Event Event { get; set; }
		public LightType Type { get; set; }
		/// <summary>
		/// Location of light in world
		/// </summary>
		public float4 Co { get; set; }
		/// <summary>
		/// Direction of light (ignored for point light)
		/// </summary>
		public float4 Dir { get; set; }
		/// <summary>
		/// Size of soft-shadow. Higher values give softer shadows, lower values
		/// sharper shadows.
		/// 
		/// Note that lower values will contribute to fireflies.
		/// </summary>
		public float Size { get; set; }

		public float SizeU { get; set; }
		public float SizeV { get; set; }

		public float4 AxisU { get; set; }
		public float4 AxisV { get; set; }

		public float SpotAngle { get; set; }
		public float SpotSmooth { get; set; }

		/// <summary>
		/// Color of the light
		/// </summary>
		public float4 DiffuseColor { get; set; }
		/// <summary>
		/// Intensity of the light. This is generally
		/// between 0.0f and 1.0f, but can be higher
		/// </summary>
		public float Strength { get; set; }

		/// <summary>
		/// Set to true if light source should cast shadows
		/// </summary>
		public bool CastShadow { get; set; }

		/// <summary>
		/// Set to true if multiple importance sampling is to
		/// be used for this light
		/// </summary>
		public bool UseMis { get; set; }

		/// <summary>
		/// Light ID set to the RhinoObject Id it represents
		/// </summary>
		public Guid Id { get; set; }

		public float Gamma { get; set; }
	}
}
