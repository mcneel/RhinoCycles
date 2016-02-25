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

namespace RhinoCyclesCore
{
	/// <summary>
	/// Intermediat class to record dynamic object transforms
	/// </summary>
	public class CyclesObjectTransform
	{
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="id">Id of object</param>
		/// <param name="t">Transform</param>
		public CyclesObjectTransform(uint id, Transform t)
		{
			Id = id;
			Transform = t;
		}

		/// <summary>
		/// Get the object ID
		/// </summary>
		public uint Id { get; private set; }

		/// <summary>
		/// Get the transform
		/// </summary>
		public Transform Transform { get; set; }

		/// <summary>
		/// Hash code for this instance
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			return Id.GetHashCode();
		}

		/// <summary>
		/// Two instances are considered equal when their Ids match.
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
		{
			var cot = obj as CyclesObjectTransform;

			return cot != null && Id.Equals(cot.Id);
		}

		/// <summary>
		/// Textual representation of instances
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return String.Format("ccl.CyclesObjectTransform: {0}, {1}", Id, Transform);
		}
	}
}
