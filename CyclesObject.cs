/**
Copyright 2014-2021 Robert McNeel and Associates

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
using System.Collections.Generic;
using ccl;

namespace RhinoCyclesCore
{
	/// <summary>
	/// Simple wrapper object to hold mesh related data to be pushed to Cycles.
	/// </summary>
	public class CyclesObject : IDisposable
	{
		private bool disposedValue;

		public CyclesObject()
		{
			Visible = true;
			CastShadow = true;
			OcsFrame = Transform.Identity();
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
		/// The OcsFrame for this object.
		/// </summary>
		public ccl.Transform OcsFrame { get; set; } = ccl.Transform.Identity();
		/// <summary>
		/// Material CRC (RenderHash)
		/// </summary>
		public uint matid { get; set; }

		/// <summary>
		/// Visibility toggle.
		/// </summary>
		public bool Visible { get; set; }

		public uint Shader { get; set; }

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

		public List<CyclesDecal> Decals { get; set; } = null;

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					if(Decals!=null)
					{
						foreach(CyclesDecal cyclesDecal in Decals)
						{
							cyclesDecal.Dispose();
						}
					}
				}

				disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
