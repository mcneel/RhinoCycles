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
using System.Collections.Generic;
using System.Linq;
using CclShader = ccl.Shader;

namespace RhinoCycles.Database
{
	/// <summary>
	/// Read-only interface to shader database
	/// </summary>
	public interface IReadShaderDatabase
	{
		bool HasChanges();
		List<CyclesObjectShader> ObjectShaderChanges { get; }
		List<Tuple<object, CclShader>> AllShaders { get; }

		bool HasShader(uint shaderId);
		CclShader GetShaderFromHash(uint shaderId);
		uint GetHashFromShader(CclShader shader);
	}

	public interface IWriteShaderDatabase
	{
		
	}

	public interface IReadWriteShaderDatabase : IReadShaderDatabase, IWriteShaderDatabase
	{ }

	public class ShaderDatabase : IReadWriteShaderDatabase
	{
		/// <summary>
		/// RhinoCycles shaders and Cycles shaders relations
		/// </summary>
		private readonly List<Tuple<object, CclShader>> m_all_shaders = new List<Tuple<object, CclShader>>();

		/// <summary>
		/// record material changes for objects
		/// </summary>
		private readonly List<CyclesObjectShader> m_cq_objects_shader_changes = new List<CyclesObjectShader>(); 
		/// <summary>
		/// record shader changes to push to cycles
		/// </summary>
		private readonly List<CyclesShader> m_cq_shaders = new List<CyclesShader>();
		/// <summary>
		/// record RenderMaterial CRC and Shader relationship. Key is RenderHash, Value is Shader.
		/// </summary>
		private readonly Dictionary<uint, CclShader> m_rh_ccl_shaders = new Dictionary<uint, CclShader>(); 
		/// <summary>
		/// record shader in scene relationship. Key is RenderMaterial.RenderHash, Value is shader id in scene.
		/// </summary>
		private readonly Dictionary<uint, uint> m_rh_ccl_scene_shader_ids = new Dictionary<uint, uint>();

		/// <summary>
		/// Return true if any shader or object shader changes were recorded by the ChangeQueue mechanism.
		/// </summary>
		/// <returns>True when changes where recorded, false otherwise.</returns>
		public bool HasChanges()
		{
			return m_cq_shaders.Any() || m_cq_objects_shader_changes.Any();
		}

		/// <summary>
		/// Get a list of object shader changes.
		/// </summary>
		public List<CyclesObjectShader> ObjectShaderChanges
		{
			get
			{
				return m_cq_objects_shader_changes;
			}
		}

		/// <summary>
		/// Get a list of shader changes.
		/// </summary>
		public List<CyclesShader> ShaderChanges
		{
			get
			{
				return m_cq_shaders;
			}
		} 

		/// <summary>
		/// Get a list of all shaders.
		/// </summary>
		public List<Tuple<object, CclShader>> AllShaders
		{
			get
			{
				return m_all_shaders;
			}
		}

		/// <summary>
		/// Record the CclShader for given id.
		/// </summary>
		/// <param name="id">RenderHash of the Rhino material</param>
		/// <param name="shader">ccl.Shader</param>
		public void RecordRhCclShaderRelation(uint id, CclShader shader)
		{
				m_rh_ccl_shaders.Add(id, shader);
		}

		/// <summary>
		/// Record the Cycles shader id in scene for RenderHash
		/// </summary>
		/// <param name="shaderId">Rhino material RenderHash</param>
		/// <param name="shaderSceneId">Cycles shader scene id</param>
		public void RecordCclShaderSceneId(uint shaderId, uint shaderSceneId)
		{
				m_rh_ccl_scene_shader_ids.Add(shaderId, shaderSceneId);
		}

		/// <summary>
		/// Get Cycles shader scene id for Rhino material RenderHash.
		/// @todo check this is correct naming and dictionary to query from
		/// </summary>
		/// <param name="id"></param>
		/// <returns>Cycles shader scene id</returns>
		public uint GetShaderIdForMatId(uint id)
		{
			return m_rh_ccl_scene_shader_ids[id];
		}

		/// <summary>
		/// Add a CyclesLight and its shader
		/// </summary>
		/// <param name="l"></param>
		/// <param name="shader"></param>
		public void Add(CyclesLight l, CclShader shader)
		{
			m_all_shaders.Add(new Tuple<object, CclShader>(l, shader));
		}

		/// <summary>
		/// Add a CyclesShader and its shader
		/// </summary>
		/// <param name="s"></param>
		/// <param name="shader"></param>
		public void Add(CyclesShader s, CclShader shader)
		{
			m_all_shaders.Add(new Tuple<object, CclShader>(s, shader));
		}

		public void AddObjectMaterialChange(CyclesObjectShader o)
		{
			if(!m_cq_objects_shader_changes.Contains(o)) m_cq_objects_shader_changes.Add(o);
		}

		/// <summary>
		/// Clear queue of object shader changes
		/// </summary>
		public void ClearObjectShaderChanges()
		{
			m_cq_objects_shader_changes.Clear();
		}

		/// <summary>
		/// Clear queue of shader changes.
		/// </summary>
		public void ClearShaders()
		{
			m_cq_shaders.Clear();
		}

		/// <summary>
		/// Check if a shader for a certain RenderHash already exists.
		/// </summary>
		/// <param name="shaderId"></param>
		/// <returns></returns>
		public bool HasShader(uint shaderId)
		{
			return m_rh_ccl_shaders.ContainsKey(shaderId);
		}

		/// <summary>
		/// Get Shader for hash, or null if not found
		/// </summary>
		/// <param name="shaderId">Render hash</param>
		/// <returns>Shader if found, null otherwise</returns>
		public CclShader GetShaderFromHash(uint shaderId)
		{
			return HasShader(shaderId) ? m_rh_ccl_shaders[shaderId] : null;
		}

		/// <summary>
		/// Get RenderHash for a <c>Shader</c>
		/// </summary>
		/// <param name="shader">Shader to search for</param>
		/// <returns>RenderHash for <c>shader</c></returns>
		public uint GetHashFromShader(CclShader shader)
		{
			var hash = uint.MaxValue;
			foreach (var hash_shader in m_rh_ccl_shaders)
			{
				if (hash_shader.Value.Id == shader.Id) hash = hash_shader.Key;
			}

			return hash;
		}

		/// <summary>
		/// Add a CyclesShader to the list of shaders that will have to be committed to Cycles.
		/// </summary>
		/// <param name="shader"></param>
		public void AddShader(CyclesShader shader)
		{
			if (!m_rh_ccl_shaders.ContainsKey(shader.Id) && !m_cq_shaders.Contains(shader))
			{
				m_cq_shaders.Add(shader);
				//m_all_shaders.Add(shader);
			}
		}


	}
}
