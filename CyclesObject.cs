/**
Copyright 2014-2017 Robert McNeel and Associates

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

namespace RhinoCyclesCore
{
	/// <summary>
	/// Simple wrapper object to hold mesh related data to be pushed to Cycles.
	/// </summary>
	public class CyclesObject
	{
		public CyclesObject()
		{
			Visible = true;
			CastShadow = true;
		}

		public ccl.Object cob { get; set; }

		/// <summary>
		/// Id of InstanceAncestry
		/// </summary>
		public uint obid { get; set; }

		/// <summary>
		/// Guid of the mesh this object references
		/// </summary>
		public Tuple<Guid, int> meshid { get; set; }

		/// <summary>
		/// The transformation matrix for this object.
		/// </summary>
		public Transform Transform { get; set; }

		/// <summary>
		/// Material CRC (RenderHash)
		/// </summary>
		public uint matid { get; set; }

		/// <summary>
		/// Visibility toggle.
		/// </summary>
		public bool Visible { get; set; }

		public bool CastShadow { get; set; }

		/// <summary>
		/// Shadow-only toggle.
		/// </summary>
		public bool IsShadowCatcher { get; set; }

		/// <summary>
		/// Like CastShadow, but to be used for objects that have an
		/// emissive material. Such an object is considered a
		/// mesh ligh. Set to true to ensure this light doesn't cast
		/// shadows.
		/// </summary>
		public bool CastNoShadow { get; set; }

		/// <summary>
		/// Object is clipping object if set to true
		/// </summary>
		public bool Cutout { get; set; }

		/// <summary>
		/// Object ignores any clipping object if set to true
		/// </summary>
		public bool IgnoreCutout { get; set; }
	}
}
