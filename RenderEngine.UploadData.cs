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
using System.Linq;
using ccl;
using Object = ccl.Object;

namespace RhinoCycles
{
	partial class RenderEngine
	{
		/// <summary>
		/// Main entry point for uploading data to Cycles.
		/// </summary>
		private void UploadData()
		{
			// linear workflow changes
			Database.UploadLinearWorkflowChanges();

			// gamma changes
			Database.UploadGammaChanges();

			// environment changes
			Database.UploadEnvironmentChanges();

			// transforms on objects, no geometry changes
			UploadDynamicObjectTransforms();

			// viewport changes
			Database.UploadCameraChanges();

			// new shaders we've got
			UploadShaderChanges();

			// light changes
			Database.UploadLightChanges();

			// mesh changes (new ones, updated ones)
			UploadMeshChanges();

			// shader changes on objects (replacement)
			UploadObjectShaderChanges();

			// object changes (new ones, deleted ones)
			UploadObjectChanges();

			// done, now clear out our change queue stuff so we're ready for the next time around :)
			ClearChanges();
		}

		private void ClearChanges()
		{
			Database.ClearChanges();
			ClearShaders();
			ClearObjectsChanges();
			ClearMeshes();
			ClearDynamicObjectTransforms();
			ClearObjectShaderChanges();
		}


		/// <summary>
		/// Change shaders on objects and their meshes
		/// </summary>
		private void UploadObjectShaderChanges()
		{
			foreach (var obshad in m_cq_objects_shader_changes)
			{

				var cob = FindObjectRelation(obshad.Id);

				//if(mesh!=null) mesh.ReplaceShader(new_shader);

				if(cob!=null)
				{
					// get shaders
					var new_shader = GetShaderFromHash(obshad.NewShaderHash);
					var old_shader = GetShaderFromHash(obshad.OldShaderHash);
					if (new_shader != null)
					{
						if (cob.Mesh != null) cob.Mesh.ReplaceShader(new_shader);
						new_shader.Tag();
					}
					if (old_shader != null)
					{
						old_shader.Tag();
					}
					cob.TagUpdate();
				}
				ReplaceShaderRelation(obshad.OldShaderHash, obshad.NewShaderHash, obshad.Id);
			}
		}
		/// <summary>
		/// Upload changes to shaders
		/// </summary>
		private void UploadShaderChanges()
		{
			// map shaders. key is RenderHash
			foreach (var shader in m_cq_shaders)
			{
				if (CancelRender) return;

				// create a cycles shader
				var sh = CreateMaterialShader(shader);
				m_rh_ccl_shaders.Add(shader.Id, sh);
				Database.m_all_shaders.Add(new Tuple<object, Shader>(shader, sh));
				// add the new shader to scene
				var scshid = Client.Scene.AddShader(sh);
				m_rh_ccl_scene_shader_ids.Add(shader.Id, scshid);

				sh.Tag();
			}
		}

		/// <summary>
		/// Handle dynamic object transforms
		/// </summary>
		private void UploadDynamicObjectTransforms()
		{
			foreach (var cot in m_cq_object_transform)
			{
				var cob = FindObjectRelation(cot.Id);
				if (cob == null) continue;

				cob.Transform = cot.Transform;
				cob.Mesh.TagRebuild();
				cob.TagUpdate();
			}
		}

		/// <summary>
		/// Upload mesh changes
		/// </summary>
		private void UploadMeshChanges()
		{
			// handle mesh deletes first
			foreach (var mesh_delete in m_cq_meshes_to_delete)
			{
				var cobs = GetCyclesObjectsForGuid(mesh_delete);

				foreach (var cob in cobs)
				{
					// remove mesh data
					cob.Mesh.ClearData();
					cob.Mesh.TagRebuild();
					// hide object containing the mesh
					cob.Visibility = PathRay.Hidden;
					cob.TagUpdate();
				}
			}

			var curmesh = 0;
			var totalmeshes = m_cq_mesh_changes.Count;
			foreach (var mesh_change in m_cq_mesh_changes)
			{
				var cycles_mesh = mesh_change.Value;
				var mid = mesh_change.Key;

				var me = FindMeshRelation(mid);

				// newme true if we have to upload new mesh data
				var newme = me == null;

				if (CancelRender) return;

				// lets find the shader for this, or use 0 if none found.
				uint shid;
				var matid = FindRenderHashForMeshId(cycles_mesh.MeshId);
				try
				{
					shid = m_rh_ccl_scene_shader_ids[matid];
				}
				catch (Exception)
				{
					shid = 0;
				}

				var shader = Client.Scene.ShaderFromSceneId(shid);

				// creat a new mesh to upload mesh data to
				if (newme)
				{
					me = new Mesh(Client, shader);
				}
				else
				{
					// or just reuse existing mesh container.
					me.ClearData();
				}

				// update status bar of render window.
				var stat = String.Format("Upload mesh {0}/{1} [v: {2}, t: {3} using shader {4}]", curmesh, totalmeshes,
					cycles_mesh.verts.Length, cycles_mesh.faces.Length, shid);

				// set progress, but without rendering percentage (hence the -1.0f)
				SetProgress(RenderWindow, stat, -1.0f);

				// upload, if we get false back we were signalled to stop rendering by user
				if (!UploadMeshData(me, cycles_mesh)) return;

				// if we re-uploaded mesh data, we need to make sure the shader
				// information doesn't get lost.
				if (!newme) me.ReplaceShader(shader);

				// don't forget to record this new mesh
				if(newme) RecordObjectMeshRelation(cycles_mesh.MeshId, me);
				//RecordShaderRelation(shader, cycles_mesh.MeshId);

				curmesh++;
			}
		}

		/// <summary>
		/// Upload object changes
		/// </summary>
		private void UploadObjectChanges()
		{
			// first delete objects
			foreach (var ob in m_cq_deleted_objects)
			{
				if (ob.cob != null)
				{
					var cob = ob.cob;
					// deleting we do (for now?) by marking object as hidden.
					// we *don't* clear mesh data here, since that very mesh
					// may be used elsewhere.
					cob.Visibility = PathRay.Hidden;
					cob.TagUpdate();
				}
			}

			// now combine objects and meshes, creating new objects when necessary
			foreach (var ob in m_cq_new_updated_objects)
			{
				// mesh for this object id
				var mesh = FindMeshRelation(ob.meshid);

				// hmm, no mesh. Oh well, lets get on with the next
				if (mesh == null) continue;

				// see if we already have an object here.
				// update it, otherwise create new one
				var cob = FindObjectRelation(ob.obid);

				var newcob = cob == null;

				// new object, so lets create it and record necessary stuff about it
				if (newcob)
				{
					cob = new Object(Client);
					RecordObjectRelation(ob.obid, cob);
					RecordObjectIdMeshIdRelation(ob.obid, ob.meshid);
				}

				// set mesh reference and other stuff
				cob.Mesh = mesh;
				cob.Transform = ob.Transform;
				cob.Visibility = ob.Visible ? PathRay.AllVisibility : PathRay.Hidden;
				cob.TagUpdate();
			}
		}


		/// <summary>
		/// Upload mesh data, return false if cancel render is signalled.
		/// </summary>
		/// <param name="me">mesh to upload to</param>
		/// <param name="cyclesMesh">data to upload from</param>
		/// <returns>true if uploaded without cancellation, false otherwise</returns>
		private bool UploadMeshData(Mesh me, CyclesMesh cyclesMesh)
		{
			// set raw vertex data
			me.SetVerts(ref cyclesMesh.verts);
			if (CancelRender) return false;
			// set the triangles
			me.SetVertTris(ref cyclesMesh.faces, cyclesMesh.vertex_normals != null);
			if (CancelRender) return false;
			// set vertex normals
			if (cyclesMesh.vertex_normals != null)
			{
				me.SetVertNormals(ref cyclesMesh.vertex_normals);
			}
			if (CancelRender) return false;
			// set uvs
			if (cyclesMesh.uvs != null)
			{
				me.SetUvs(ref cyclesMesh.uvs);
			}
			// and finally tag for rebuilding
			me.TagRebuild();
			return true;
		}


	}
}
