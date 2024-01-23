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

namespace RhinoCyclesCore
{
	/// <summary>
	/// Helper object to determine what shaders need update, what shaders need to be created
	/// </summary>
	public class CyclesObjectShader
	{
		/// <summary>
		/// Construct new helper object to track shaders for object <code>id</code>
		/// </summary>
		/// <param name="id"></param>
		public CyclesObjectShader(uint id)
		{
			Id = id;
		}

		/// <summary>
		/// Get the object Id
		/// </summary>
		public uint Id { get; }

		/// <summary>
		/// Equality on Id
		/// </summary>
		/// <param name="obj"></param>
		/// <returns>True if object Ids are the same</returns>
		public override bool Equals(object obj)
		{
			var o = obj as CyclesObjectShader;

			return o != null && Id.Equals(o.Id);
		}

		/// <summary>
		/// Get hash code - based on Id
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			return Id.GetHashCode();
		}

		/// <summary>
		/// Get/set the shader hash for object in scene, if it already has one.
		/// </summary>
		public uint OldShaderHash { get; set; }

		/// <summary>
		/// Get/set the new shader hash for object in scene.
		/// </summary>
		public uint NewShaderHash { get; set; }

		/// <summary>
		/// True if old and new shaders differ
		/// </summary>
		public bool Changed => OldShaderHash != NewShaderHash;
	}
}
