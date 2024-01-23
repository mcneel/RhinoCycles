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

using System;
using System.Collections.Generic;
using System.Linq;
using CclShader = ccl.Shader;

namespace RhinoCyclesCore.Database
{
	/// <summary>
	/// Read-only interface to shader database
	/// </summary>
	public interface IReadShaderDatabase
	{
		bool HasChanges();
		List<CyclesObjectShader> ObjectShaderChanges { get; }

		// TODO: XXXX revisit AllShaders need
		// List<Tuple<object, CclShader>> AllShaders { get; }

		bool HasShader(uint shaderId);
		CclShader GetShaderFromHash(uint shaderId);
		uint GetHashFromShader(CclShader shader);
	}

	public interface IWriteShaderDatabase
	{

	}

	public interface IReadWriteShaderDatabase : IReadShaderDatabase, IWriteShaderDatabase
	{ }

	public class ShaderDatabase : IReadWriteShaderDatabase, IDisposable
	{
		/// <summary>
		/// RhinoCycles shaders and Cycles shaders relations
		/// </summary>
		private readonly List<Tuple<object, CclShader>> _allShaders = new List<Tuple<object, CclShader>>();

		/// <summary>
		/// record material changes for objects
		/// </summary>
		private readonly List<CyclesObjectShader> _cqObjectsShaderChanges = new List<CyclesObjectShader>();
		/// <summary>
		/// record shader changes to push to cycles
		/// </summary>
		private readonly List<CyclesShader> _cqShaders = new List<CyclesShader>();
		/// <summary>
		/// record RenderMaterial CRC and Shader relationship. Key is RenderHash, Value is Shader.
		/// </summary>
		private readonly Dictionary<uint, CclShader> _rhCclShaders = new Dictionary<uint, CclShader>();
		private bool disposedValue;

		/// <summary>
		/// Return true if any shader or object shader changes were recorded by the ChangeQueue mechanism.
		/// </summary>
		/// <returns>True when changes where recorded, false otherwise.</returns>
		public bool HasChanges()
		{
			return _cqShaders.Any() || _cqObjectsShaderChanges.Any();
		}

		/// <summary>
		/// Get a list of object shader changes.
		/// </summary>
		public List<CyclesObjectShader> ObjectShaderChanges => _cqObjectsShaderChanges;

		/// <summary>
		/// Get a list of shader changes.
		/// </summary>
		public List<CyclesShader> ShaderChanges => _cqShaders;

		/// <summary>
		/// Get a list of all shaders.
		/// </summary>
		public List<Tuple<object, CclShader>> AllShaders => _allShaders;

		/// <summary>
		/// Record the CclShader for given id.
		/// </summary>
		/// <param name="id">RenderHash of the Rhino material</param>
		/// <param name="shader">ccl.Shader</param>
		public void RecordRhCclShaderRelation(uint id, CclShader shader)
		{
				_rhCclShaders.Add(id, shader);
		}

		/// <summary>
		/// Add a CyclesLight and its shader
		/// </summary>
		/// <param name="l"></param>
		/// <param name="shader"></param>
		public void Add(CyclesLight l, CclShader shader)
		{
			_allShaders.Add(new Tuple<object, CclShader>(l, shader));
		}

		/// <summary>
		/// Add a CyclesShader and its shader
		/// </summary>
		/// <param name="s"></param>
		/// <param name="shader"></param>
		public void Add(CyclesShader s, CclShader shader)
		{
			_allShaders.Add(new Tuple<object, CclShader>(s, shader));
		}

		public void AddObjectMaterialChange(CyclesObjectShader o)
		{
			if(!_cqObjectsShaderChanges.Contains(o)) _cqObjectsShaderChanges.Add(o);
		}

		/// <summary>
		/// Clear queue of object shader changes
		/// </summary>
		public void ClearObjectShaderChanges()
		{
			_cqObjectsShaderChanges.Clear();
		}

		/// <summary>
		/// Clear queue of shader changes.
		/// </summary>
		public void ClearShaders()
		{
			_cqShaders.Clear();
		}

		/// <summary>
		/// Check if a shader for a certain RenderHash already exists.
		/// </summary>
		/// <param name="shaderId"></param>
		/// <returns></returns>
		public bool HasShader(uint shaderId)
		{
			return _rhCclShaders.ContainsKey(shaderId);
		}

		/// <summary>
		/// Get Shader for hash, or null if not found
		/// </summary>
		/// <param name="shaderId">Render hash</param>
		/// <returns>Shader if found, null otherwise</returns>
		public CclShader GetShaderFromHash(uint shaderId)
		{
			return HasShader(shaderId) ? _rhCclShaders[shaderId] : null;
		}

		/// <summary>
		/// Get RenderHash for a <c>Shader</c>
		/// </summary>
		/// <param name="shader">Shader to search for</param>
		/// <returns>RenderHash for <c>shader</c></returns>
		public uint GetHashFromShader(CclShader shader)
		{
			var hash = uint.MaxValue;
			foreach (var hashShader in _rhCclShaders)
			{
				if (hashShader.Value.Id == shader.Id) hash = hashShader.Key;
			}

			return hash;
		}

		/// <summary>
		/// Add a CyclesShader to the list of shaders that will have to be committed to Cycles.
		/// </summary>
		/// <param name="shader"></param>
		public void AddShader(CyclesShader shader)
		{
			if (!_rhCclShaders.ContainsKey(shader.Id) && !_cqShaders.Contains(shader))
			{
				_cqShaders.Add(shader);
				//_allShaders.Add(shader);
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					/*
					foreach (var allshader in _allShaders)
					{
						allshader.Item2?.Dispose();
					}
					_allShaders.Clear();
					*/
					_cqObjectsShaderChanges.Clear(); // CyclesObjectShader
					foreach (var shader in _cqShaders)
					{
						shader.Dispose();
					}
					_cqShaders.Clear(); // CyclesShader
					foreach (var cclshader in _rhCclShaders)
					{
						cclshader.Value?.Dispose();
					}
					_rhCclShaders.Clear(); // uint, CclShader
				}

				// TODO: free unmanaged resources (unmanaged objects) and override finalizer
				// TODO: set large fields to null
				disposedValue = true;
			}
		}

		// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~ShaderDatabase()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
