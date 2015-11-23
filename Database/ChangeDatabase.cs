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
using System.Drawing;
using System.Linq;
using ccl;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using Rhino.Render.ChangeQueue;
using RhinoCycles.Database;
using sdd = System.Diagnostics.Debug;
using CqMaterial = Rhino.Render.ChangeQueue.Material;
using CqMesh = Rhino.Render.ChangeQueue.Mesh;
using CqGroundPlane = Rhino.Render.ChangeQueue.GroundPlane;
using CqLight = Rhino.Render.ChangeQueue.Light;
using CqSkylight = Rhino.Render.ChangeQueue.Skylight;
using CQLinearWorkflow = Rhino.Render.ChangeQueue.LinearWorkflow;
using CclLight = ccl.Light;
using CclMesh = ccl.Mesh;
using CclObject = ccl.Object;
using Light = Rhino.Geometry.Light;

namespace RhinoCycles.Database
{
	public class ChangeDatabase : ChangeQueue
	{
		/// <summary>
		/// Reference to the Cycles render engine C# level implementation.
		/// </summary>
		private readonly RenderEngine m_render_engine;

		private ViewInfo m_current_view_info;

		#region DATABASES

		/// <summary>
		/// Database responsible for keeping track of objects/meshes and their shaders.
		/// </summary>
		private readonly ObjectShaderDatabase m_object_shader_db;

		/// <summary>
		/// Database responsible for all material shaders
		/// </summary>
		private readonly ShaderDatabase m_shader_db = new ShaderDatabase();

		/// <summary>
		/// Database responsible for keeping track of objects and meshes and their relations between
		/// Rhino and Cycles.
		/// </summary>
		private readonly ObjectDatabase m_object_db = new ObjectDatabase();

		/// <summary>
		/// The database responsible for keeping track of light changes and the relations between Rhino
		/// and Cycles lights and their shaders.
		/// </summary>
		private readonly LightDatabase m_light_db = new LightDatabase();

		/// <summary>
		/// Database responsible for keeping track of background and environment changes and their
		/// relations between Rhino and Cycles.
		/// </summary>
		private readonly EnvironmentDatabase m_env_db = new EnvironmentDatabase();

		/// <summary>
		/// Database responsible for managing camera transforms from Rhino to Cycles.
		/// </summary>
		private readonly CameraDatabase m_camera_db = new CameraDatabase();

		#endregion

		private readonly ShaderConverter m_shader_converter;

		/// <summary>
		/// Constructor for our changequeue implementation
		/// </summary>
		/// <param name="pluginId">Id of the plugin instantiating the render change queue</param>
		/// <param name="engine">Reference to our render engine</param>
		/// <param name="doc">Document runtime serial number</param>
		/// <param name="view">Reference to the RhinoView for which this queue is created.</param>
		internal ChangeDatabase(Guid pluginId, RenderEngine engine, uint doc, RhinoView view) : base(pluginId, doc, view)
		{
			m_render_engine = engine;
			m_object_shader_db = new ObjectShaderDatabase(m_object_db);
			m_shader_converter = new ShaderConverter(engine.Settings);
		}


		/// <summary>
		/// Constructor for our changequeue implementation
		/// </summary>
		/// <param name="pluginId">Id of the plugin instantiating the render change queue</param>
		/// <param name="engine">Reference to our render engine</param>
		/// <param name="createPreviewEventArgs">preview event arguments</param>
		internal ChangeDatabase(Guid pluginId, RenderEngine engine, CreatePreviewEventArgs createPreviewEventArgs) : base(pluginId, createPreviewEventArgs)
		{
			m_render_engine = engine;
			m_object_shader_db = new ObjectShaderDatabase(m_object_db);
			m_shader_converter = new ShaderConverter(engine.Settings);
		}

		public void UploadLinearWorkflowChanges()
		{
			if (LinearWorkflowHasChanged)
			{
				if (LinearWorkflow.Active)
				{
					Gamma = LinearWorkflow.Gamma;
				}
				else
				{
					Gamma = 1.0f;
				}
			}
		}

		/// <summary>
		/// Change shaders on objects and their meshes
		/// </summary>
		public void UploadObjectShaderChanges()
		{
			foreach (var obshad in m_shader_db.ObjectShaderChanges)
			{

				var cob = m_object_db.FindObjectRelation(obshad.Id);
				if(cob!=null)
				{
					// get shaders
					var new_shader = m_shader_db.GetShaderFromHash(obshad.NewShaderHash);
					var old_shader = m_shader_db.GetShaderFromHash(obshad.OldShaderHash);
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
				m_object_shader_db.ReplaceShaderRelation(obshad.OldShaderHash, obshad.NewShaderHash, obshad.Id);
			}
		}

		public event EventHandler<LinearWorkflowUploadedEventArgs> LinearWorkflowUploaded; 

		public void UploadGammaChanges()
		{
			if (GammaHasChanged)
			{
				BitmapConverter.ApplyGammaToTextures(Gamma);

				if (m_env_db.CurrentBackgroundShader != null)
				{
					m_env_db.CurrentBackgroundShader.Reset();
					var handler = LinearWorkflowUploaded;
					if (handler != null)
					{
						handler(this, new LinearWorkflowUploadedEventArgs(new RhinoCycles.LinearWorkflow(LinearWorkflow), Gamma));
					}
				}

				foreach (var tup in m_shader_db.AllShaders)
				{
					var matsh = tup.Item1 as CyclesShader;
					if (matsh != null)
					{
						matsh.Gamma = Gamma;
						m_render_engine.RecreateMaterialShader(matsh, tup.Item2);
						tup.Item2.Tag();
					}

					var lgsh = tup.Item1 as CyclesLight;
					if (lgsh != null)
					{
						lgsh.Gamma = Gamma;
						m_render_engine.ReCreateSimpleEmissionShader(tup.Item2, lgsh);
						tup.Item2.Tag();
					}

				}

				m_render_engine.Session.Scene.Film.Update();
			}
		}

		/// <summary>
		/// Handle dynamic object transforms
		/// </summary>
		public void UploadDynamicObjectTransforms()
		{
			foreach (var cot in m_object_db.ObjectTransforms)
			{
				var cob = m_object_db.FindObjectRelation(cot.Id);
				if (cob == null) continue;

				cob.Transform = cot.Transform;
				cob.Mesh.TagRebuild();
				cob.TagUpdate();
			}
		}

		/// <summary>
		/// Upload mesh changes
		/// </summary>
		public void UploadMeshChanges()
		{
			// handle mesh deletes first
			foreach (var mesh_delete in m_object_db.MeshesToDelete)
			{
				var cobs = m_object_db.GetCyclesObjectsForGuid(mesh_delete);

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
			var totalmeshes = m_object_db.MeshChanges.Count;
			foreach (var mesh_change in m_object_db.MeshChanges)
			{
				var cycles_mesh = mesh_change.Value;
				var mid = mesh_change.Key;

				var me = m_object_db.FindMeshRelation(mid);

				// newme true if we have to upload new mesh data
				var newme = me == null;

				if (m_render_engine.CancelRender) return;

				// lets find the shader for this, or use 0 if none found.
				uint shid;
				var matid = m_object_shader_db.FindRenderHashForMeshId(cycles_mesh.MeshId);
				try
				{
					// @todo check this is correct naming and dictionary to query from
					shid = m_shader_db.GetShaderIdForMatId(matid);
				}
				catch (Exception)
				{
					shid = 0;
				}

				var shader = m_render_engine.Client.Scene.ShaderFromSceneId(shid);

				// creat a new mesh to upload mesh data to
				if (newme)
				{
					me = new CclMesh(m_render_engine.Client, shader);
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
				m_render_engine.SetProgress(m_render_engine.RenderWindow, stat, -1.0f);

				// upload, if we get false back we were signalled to stop rendering by user
				if (!UploadMeshData(me, cycles_mesh)) return;

				// if we re-uploaded mesh data, we need to make sure the shader
				// information doesn't get lost.
				if (!newme) me.ReplaceShader(shader);

				// don't forget to record this new mesh
				if(newme) m_object_db.RecordObjectMeshRelation(cycles_mesh.MeshId, me);
				//RecordShaderRelation(shader, cycles_mesh.MeshId);

				curmesh++;
			}
		}

		/// <summary>
		/// Upload mesh data, return false if cancel render is signalled.
		/// </summary>
		/// <param name="me">mesh to upload to</param>
		/// <param name="cyclesMesh">data to upload from</param>
		/// <returns>true if uploaded without cancellation, false otherwise</returns>
		private bool UploadMeshData(CclMesh me, CyclesMesh cyclesMesh)
		{
			// set raw vertex data
			me.SetVerts(ref cyclesMesh.verts);
			if (m_render_engine.CancelRender) return false;
			// set the triangles
			me.SetVertTris(ref cyclesMesh.faces, cyclesMesh.vertex_normals != null);
			if (m_render_engine.CancelRender) return false;
			// set vertex normals
			if (cyclesMesh.vertex_normals != null)
			{
				me.SetVertNormals(ref cyclesMesh.vertex_normals);
			}
			if (m_render_engine.CancelRender) return false;
			// set uvs
			if (cyclesMesh.uvs != null)
			{
				me.SetUvs(ref cyclesMesh.uvs);
			}
			// and finally tag for rebuilding
			me.TagRebuild();
			return true;
		}

		/// <summary>
		/// Reset changequeue lists and dictionaries. Generally this is done once all changes
		/// have been handled, and thus no longer needed.
		/// </summary>
		public void ResetChangeQueue()
		{
			ClearGamma();
			ClearLinearWorkflow();
			m_env_db.ResetBackgroundChangeQueue();
			m_camera_db.ResetViewChangeQueue();
			m_light_db.ResetLightChangeQueue();
			m_shader_db.ClearShaders();
			m_shader_db.ClearObjectShaderChanges();
			m_object_db.ResetObjectsChangeQueue();
			m_object_db.ResetMeshChangeQueue();
			m_object_db.ResetDynamicObjectTransformChangeQueue();
		}

		/// <summary>
		/// Tell if any changes have been recorded by the ChangeQueue mechanism since
		/// the last flush.
		/// </summary>
		/// <returns>True if changes where recorded, false otherwise.</returns>
		public bool HasChanges()
		{
			return
				m_camera_db.HasChanges() ||
				m_env_db.BackgroundHasChanged ||
				m_light_db.HasChanges() || 
				m_shader_db.HasChanges() ||
				m_object_db.HasChanges() ||
				LinearWorkflowHasChanged ||
				GammaHasChanged;
		}

		private float m_gamma = 1.0f;

		public bool GammaHasChanged { get; private set; }

		public float Gamma
		{
			set
			{
				GammaHasChanged = false;

				if (Math.Abs(m_gamma - value) > float.Epsilon)
				{
					m_gamma = value;
					GammaHasChanged = true;
				}
			}
			get
			{
				return m_gamma;
			}
		}


		private void ClearGamma()
		{
			GammaHasChanged = false;
		}

		protected override void ApplyGammaChanges(double dGamma)
		{
			Gamma= (float) dGamma;
		}

		private LinearWorkflow m_lwf;

		public bool LinearWorkflowHasChanged { get; private set; }

		public LinearWorkflow LinearWorkflow
		{
			set
			{
				if (m_lwf == null)
				{
					m_lwf = value;
					LinearWorkflowHasChanged = true;
				}
				else
				{
					LinearWorkflowHasChanged = !m_lwf.Equals(value);
					if (LinearWorkflowHasChanged)
					{
						m_lwf = value;
					}
				}

				if (m_lwf.Active)
				{
					Gamma = m_lwf.Gamma;
				}
				else
				{
					Gamma = 1.0f;
				}
			}
			get
			{
				return m_lwf;
			}
		}

		private void ClearLinearWorkflow()
		{
			LinearWorkflowHasChanged = false;
		}

		protected override void ApplyLinearWorkflowChanges(CQLinearWorkflow lw)
		{
			LinearWorkflow = new LinearWorkflow(lw);
			sdd.WriteLine(string.Format("LinearWorkflow {0} {1} {2}", lw.Active, lw.Gamma, lw.GammaReciprocal));
		}

		/// <summary>
		/// Upload camera (viewport) changes to Cycles.
		/// </summary>
		public void UploadCameraChanges()
		{
			if (!m_camera_db.HasChanges()) return;

			var view = m_camera_db.LatestView();
			UploadCamera(view);
		}

		/// <summary>
		/// Set the camera based on CyclesView
		/// </summary>
		/// <param name="view"></param>
		private void UploadCamera(CyclesView view)
		{
			var scene = m_render_engine.Session.Scene;
			m_render_engine.RenderDimension = new Size(view.Width, view.Height);
			var size = m_render_engine.RenderDimension;

			var viewportRenderEngine = m_render_engine as ViewportRenderEngine;
			if (viewportRenderEngine != null)
			{
				viewportRenderEngine.UnsetRenderSize();
			}

			var ha = size.Width > size.Height ? view.Horizontal: view.Vertical;

			var angle = (float) Math.Atan(Math.Tan(ha)/view.ViewAspectRatio) * 2.0f;

			//System.Diagnostics.Debug.WriteLine("size: {0}, matrix: {1}, angle: {2}, Sensorsize: {3}x{4}", size, view.Transform, angle, Settings.SensorHeight, Settings.SensorWidth);

			scene.Camera.Size = size;
			scene.Camera.Matrix = view.Transform;
			scene.Camera.Type = view.Projection;
			scene.Camera.Fov = angle;
			if (view.Projection == CameraType.Orthographic || view.TwoPoint) scene.Camera.SetViewPlane(view.Viewplane.Left, view.Viewplane.Right, view.Viewplane.Top, view.Viewplane.Bottom);
			else if(view.Projection == CameraType.Perspective) scene.Camera.ComputeAutoViewPlane();
			scene.Camera.SensorHeight = m_render_engine.Settings.SensorHeight;
			scene.Camera.SensorWidth = m_render_engine.Settings.SensorWidth;
			scene.Camera.Update();
		}

		/// <summary>
		/// Handle view changes.
		/// </summary>
		/// <param name="viewInfo"></param>
		protected override void ApplyViewChange(ViewInfo viewInfo)
		{
			if (!IsPreview && !viewInfo.Viewport.Id.Equals(ViewId)) return;

			m_current_view_info = viewInfo;

			//System.Diagnostics.Debug.WriteLine(String.Format("ChangeDatabase ApplyViewChange on view {0}", viewInfo.Name));

			var vp = viewInfo.Viewport;

			// camera transform, camera to world conversion
			var rhinocam = vp.GetXform(CoordinateSystem.Camera, CoordinateSystem.World);
			// lens length
			var lenslength = vp.Camera35mmLensLength;

			// lets see if we need to do magic for two-point perspective
			var twopoint = false; // @todo add support for vp.IsTwoPointPerspectiveProjection;

			// frustum values, used for two point
			double frt, frb, frr, frl, frf, frn;
			vp.GetFrustum(out frl, out frr, out frb, out frt, out frn, out frf);

			//System.Diagnostics.Debug.WriteLine(String.Format(
			//	"Frustum l {0} r {1} t {2} b {3} n {4} f{5}", frl, frr, frt, frb, frn, frf));

			// distance between top and bottom of frustum
			var dist = frt - frb;
			var disthalf = dist/2.0f;

			// if we have a disthalf and twopoint, adjust frustum top and bottom
			if (twopoint && Math.Abs(dist) >= 0.001)
			{
				frt = disthalf;
				frb = -disthalf;
				//System.Diagnostics.Debug.WriteLine(String.Format(
				//	"ADJUSTED Frustum l {0} r {1} t {2} b {3} n {4} f{5}", frl, frr, frt, frb, frn, frf));
			}

			var parallel = vp.IsParallelProjection;
			var viewscale = vp.ViewScale;

			/*System.Diagnostics.Debug.WriteLine(String.Format(
				"Camera projection type {0}, lens length {1}, scale {2}x{3}, two-point {4}, dist {5}, disthalf {6}", parallel ? "ORTHOGRAPHIC" : "PERSPECTIVE",
				lenslength, viewscale.Width, viewscale.Height, twopoint, dist, disthalf));

			System.Diagnostics.Debug.WriteLine(String.Format(
				"Frustum l {0} r {1} t {2} b {3} n {4} f{5}", frl, frr, frt, frb, frn, frf));*/

			int near, far;
			var screenport = vp.GetScreenPort(out near, out far);
			var bottom = screenport.Bottom;
			var top = screenport.Top;
			var left = screenport.Left;
			var right = screenport.Right;

			var w = Math.Abs(right - left);
			var h = Math.Abs(bottom - top);
			var portrait = w < h;
			var view_aspectratio = portrait ? h/(float)w : w/(float)h;

			// get camera angles
			double diagonal, vertical, horizontal;
			vp.GetCameraAngles(out diagonal, out vertical, out horizontal);

			// convert rhino transform to ccsycles transform
			var t = CclXformFromRhinoXform(rhinocam);
			// then convert to Cycles orientation
			t = t * Transform.RhinoToCyclesCam;

			// ready, lets push our data
			var cyclesview = new CyclesView
			{
				LensLength = lenslength,
				Transform = t,
				Diagonal =  diagonal,
				Vertical = vertical,
				Horizontal = horizontal,
				ViewAspectRatio = view_aspectratio,
				Projection = parallel ? CameraType.Orthographic : CameraType.Perspective,
				Viewplane = new ViewPlane((float)frl, (float)frr, (float)frt, (float)frb),
				TwoPoint = twopoint,
				Width = w,
				Height = h,
			};
			m_camera_db.AddViewChange(cyclesview);
		}

		/// <summary>
		/// Handle mesh changes
		/// </summary>
		/// <param name="deleted"></param>
		/// <param name="added"></param>
		protected override void ApplyMeshChanges(Guid[] deleted, List<CqMesh> added)
		{
			//System.Diagnostics.Debug.WriteLine("ChangeDatabase ApplyMeshChanges, deleted {0}, added {1}", deleted.Length, added.Count);

			foreach (var guid in deleted)
			{
				// only delete those that aren't listed in the added list
				if (!(from mesh in added where mesh.Id() == guid select mesh).Any())
				{
					//System.Diagnostics.Debug.WriteLine("Deleting {0}", guid);
					m_object_db.DeleteMesh(guid);
				}
			}

			foreach (var cqm in added)
			{
				var meshes = cqm.GetMeshes();
				var count = meshes.Length;
				var meshguid = cqm.Id();
				//System.Diagnostics.Debug.WriteLine("ChangeQueueMesh {0} has {1} sub-meshes", meshguid, count);

				var mesh_index = 0;

				foreach(var meshdata in meshes)
				{
					// Get face indices flattened to an
					// integer array.
					var findices = meshdata.Faces.ToIntArray(true);

					// Get texture coordinates and
					// flattens to a float array.
					var tc = meshdata.TextureCoordinates;
					var rhuv = tc.ToFloatArray();

					// Get rhino vertex normals and
					// flatten to a float array.
					var vn = meshdata.Normals;
					var rhvn = vn.ToFloatArray();

					// now convert UVs: from vertex indexed array to per face per vertex
					var cmuv = rhuv.Length > 0 ? new float[findices.Length * 2] : null;
					if (cmuv != null)
					{
						for (var fi = 0; fi < findices.Length; fi++)
						{
							var fioffs = fi * 2;
							var findex = findices[fi];
							var findex2 = findex * 2;
							var rhuvit = rhuv[findex2];
							var rhuvit1 = rhuv[findex2 + 1];
							cmuv[fioffs] = rhuvit;
							cmuv[fioffs + 1] = rhuvit1;
						}
					}

					var meshid = new Tuple<Guid, int>(meshguid, mesh_index);

					var crc = m_object_shader_db.FindRenderHashForMeshId(meshid);
					if (crc == uint.MaxValue) crc = 0;

					// now we have everything we need
					// so we can create a CyclesMesh that the
					// RenderEngine can eventually commit to Cycles
					var cycles_mesh = new CyclesMesh
					{
						MeshId = meshid,
						verts = meshdata.Vertices.ToFloatArray(),
						faces = findices,
						uvs = cmuv,
						vertex_normals = rhvn,
						matid = crc
					};
					mesh_index++;
					m_object_db.AddMesh(cycles_mesh);
				}
			}
		}

		/// <summary>
		/// Convert a Rhino.Geometry.Transform to ccl.Transform
		/// </summary>
		/// <param name="rt">Rhino.Geometry.Transform</param>
		/// <returns>ccl.Transform</returns>
		static Transform CclXformFromRhinoXform(Rhino.Geometry.Transform rt)
		{
			var t = new Transform(
				(float) rt.M00, (float) rt.M01, (float) rt.M02, (float) rt.M03,
				(float) rt.M10, (float) rt.M11, (float) rt.M12, (float) rt.M13,
				(float) rt.M20, (float) rt.M21, (float) rt.M22, (float) rt.M23,
				(float) rt.M30, (float) rt.M31, (float) rt.M32, (float) rt.M33
				);

			return t;
		}

		protected override void ApplyMeshInstanceChanges(List<uint> deleted, List<MeshInstance> addedOrChanged)
		{
			// helper list to ensure we don't add same material multiple times.
			var addedmats = new List<uint>();

			foreach (var d in deleted)
			{
				var cob = m_object_db.FindObjectRelation(d);
				var delob = new CyclesObject {cob = cob};
				m_object_db.DeleteObject(delob);
				//System.Diagnostics.Debug.WriteLine("Deleted MI {0}", d);
			}
			foreach (var a in addedOrChanged)
			{
				var matid = a.MaterialId;
				var mat = MaterialFromId(matid);


				if (!addedmats.Contains(matid))
				{
					HandleRenderMaterial(mat);
					addedmats.Add(matid);
				}

				var meshid = new Tuple<Guid, int>(a.MeshId, a.MeshIndex);
				//System.Diagnostics.Debug.WriteLine("Added MI {0}", a.InstanceId);
				var ob = new CyclesObject {obid = a.InstanceId, meshid = meshid, Transform = CclXformFromRhinoXform(a.Transform), matid = a.MaterialId};

				var shaderchange = new CyclesObjectShader(a.InstanceId)
				{
					OldShaderHash = uint.MaxValue,
					NewShaderHash = a.MaterialId
				};

				m_shader_db.AddObjectMaterialChange(shaderchange);

				m_object_shader_db.RecordRenderHashRelation(a.MaterialId, meshid, a.InstanceId);
				m_object_db.RecordObjectIdMeshIdRelation(a.InstanceId, meshid);
				m_object_db.AddOrUpdateObject(ob);
			}
		}

		#region SHADERS

		/// <summary>
		/// Handle RenderMaterial - will queue new shader if necessary
		/// </summary>
		/// <param name="mat"></param>
		private void HandleRenderMaterial(RenderMaterial mat)
		{
			if (m_shader_db.HasShader(mat.RenderHash)) return;

			//System.Diagnostics.Debug.WriteLine("Add new material with RenderHash {0}", mat.RenderHash);
			var sh = m_shader_converter.CreateCyclesShader(mat.TopLevelParent as RenderMaterial, Gamma);
			m_shader_db.AddShader(sh);
		}

		private void HandleMaterialChangeOnObject(RenderMaterial mat, uint obid)
		{
			var oldhash = m_object_shader_db.FindRenderHashForObjectId(obid);
			// skip if no change in renderhash
			if (oldhash != mat.RenderHash)
			{
				var o = new CyclesObjectShader(obid)
				{
					NewShaderHash = mat.RenderHash,
					OldShaderHash = oldhash
				};

				//System.Diagnostics.Debug.WriteLine("CQMat.Id: {0} meshinstanceid: {1}", mat.Id, obid);

				m_shader_db.AddObjectMaterialChange(o);
			}
		}


		/// <summary>
		/// Handle changes in materials to create (or re-use) shaders.
		/// </summary>
		/// <param name="mats">List of <c>CQMaterial</c></param>
		protected override void ApplyMaterialChanges(List<CqMaterial> mats)
		{
			// list of material hashes
			var distinct_mats = new List<uint>();

			foreach (var mat in mats)
			{
				var rm = MaterialFromId(mat.Id);

				if (!distinct_mats.Contains(mat.Id))
				{
					distinct_mats.Add(mat.Id);
				}

				var obid = mat.MeshInstanceId;

				HandleMaterialChangeOnObject(rm, obid);
			}

			// list over material hashes, check if they exist. Create if new
			foreach (var distinct in distinct_mats)
			{
				var existing = m_shader_db.GetShaderFromHash(distinct);
				if (existing == null)
				{
					var rm = MaterialFromId(distinct);
					HandleRenderMaterial(rm);
				}
			}
		}

		/// <summary>
		/// Upload changes to shaders
		/// </summary>
		public void UploadShaderChanges()
		{
			// map shaders. key is RenderHash
			foreach (var shader in m_shader_db.ShaderChanges)//m_cq_shaders)
			{
				if (m_render_engine.CancelRender) return;

				// create a cycles shader
				var sh = m_render_engine.CreateMaterialShader(shader);
				m_shader_db.RecordRhCclShaderRelation(shader.Id, sh);
				m_shader_db.Add(shader, sh);
				// add the new shader to scene
				var scshid = m_render_engine.Client.Scene.AddShader(sh);
				m_shader_db.RecordCclShaderSceneId(shader.Id, scshid);

				sh.Tag();
			}
		}

		#endregion SHADERS

		#region GROUNDPLANE

		/// <summary>
		/// Guid of our groundplane object.
		/// </summary>
		private readonly Tuple<Guid, int> m_groundplane_guid = new Tuple<Guid, int>(new Guid("306690EC-6E86-4676-B55B-1A50066D7432"), 0);


		/// <summary>
		/// The mesh instance id for ground plane
		/// </summary>
		private const uint GroundPlaneMeshInstanceId = 1;

		/// <summary>
		/// True if ground plane has been initialised
		/// </summary>
		private bool m_groundplane_initialised;

		private void InitialiseGroundPlane(CqGroundPlane gp)
		{
			m_groundplane_initialised = true;
			var gpid = m_groundplane_guid;
			var altitude = (float)(gp.Enabled ? gp.Altitude : 0.0);
			var vertices = new[]
			{
				 10000.0f, -10000.0f, 0.0f,
				 10000.0f,  10000.0f, 0.0f,
				-10000.0f,  10000.0f, 0.0f,
				-10000.0f, -10000.0f, 0.0f 
			};
			var findices = new[]
			{
				0, 1, 2,
				0, 2, 3
			};
			var cmuv = new[]
			{
				1.0f, 0.0f, 1.0f, 1.0f, 0.0f, 1.0f,
				1.0f, 0.0f, 0.0f, 1.0f, 0.0f, 0.0f
			};

			var cycles_mesh = new CyclesMesh
				{
					MeshId = gpid,
					verts = vertices,
					faces = findices,
					uvs = cmuv,
					vertex_normals = null,
					matid = gp.MaterialId
				};

			var t = Transform.Translate(0.0f, 0.0f, altitude);
			var cycles_object = new CyclesObject
			{
				matid = gp.MaterialId,
				obid = GroundPlaneMeshInstanceId,
				meshid = gpid,
				Transform = t,
				Visible = gp.Enabled
			};

			m_object_db.AddMesh(cycles_mesh);
			m_object_shader_db.RecordRenderHashRelation(gp.MaterialId, gpid, GroundPlaneMeshInstanceId);
			m_object_db.RecordObjectIdMeshIdRelation(GroundPlaneMeshInstanceId, gpid);
			m_object_db.AddOrUpdateObject(cycles_object);
		}
		/// <summary>
		/// Handle ground plane changes.
		/// </summary>
		/// <param name="gp"></param>
		protected override void ApplyGroundPlaneChanges(CqGroundPlane gp)
		{
			//System.Diagnostics.Debug.WriteLine("groundplane");
			if(!m_groundplane_initialised) InitialiseGroundPlane(gp);
			// find groundplane
			var altitude = (float)gp.Altitude;
			var t = Transform.Translate(0.0f, 0.0f, altitude);
			var cycles_object = new CyclesObject
			{
				meshid = m_groundplane_guid,
				obid = GroundPlaneMeshInstanceId,
				matid = gp.MaterialId,
				Transform = t,
				Visible = gp.Enabled
			};
			m_object_db.AddOrUpdateObject(cycles_object);

			var mat = MaterialFromId(gp.MaterialId);
			HandleRenderMaterial(mat);

			var obid = GroundPlaneMeshInstanceId;

			HandleMaterialChangeOnObject(mat, obid);
		}

		#endregion

		/// <summary>
		/// Handle dynamic object transforms
		/// </summary>
		/// <param name="dynamicObjectTransforms">List of DynamicObject transforms</param>
		protected override void ApplyDynamicObjectTransforms(List<DynamicObjectTransform> dynamicObjectTransforms)
		{
			foreach (var dot in dynamicObjectTransforms)
			{
				//System.Diagnostics.Debug.WriteLine("DynObXform {0}", dot.MeshInstanceId);
				var cot = new CyclesObjectTransform(dot.MeshInstanceId, CclXformFromRhinoXform(dot.Transform));
				m_object_db.AddDynamicObjectTransform(cot);
			}
		}

		#region LIGHT & SUN

		/// <summary>
		/// Upload all light changes to the Cycles render engine
		/// </summary>
		public void UploadLightChanges()
		{

			/* new light shaders and lights. */
			foreach (var l in m_light_db.LightsToAdd)
			{
				if (m_render_engine.CancelRender) return;

				var lgsh = m_render_engine.CreateSimpleEmissionShader(l);
				m_render_engine.Client.Scene.AddShader(lgsh);
				m_shader_db.Add(l, lgsh);

				if (m_render_engine.CancelRender) return;

				var light = new CclLight(m_render_engine.Client, m_render_engine.Client.Scene, lgsh)
				{
					Type = l.Type,
					Size = l.Size,
					Location = l.Co,
					Direction = l.Dir,
					UseMis = l.UseMis,
					CastShadow = l.CastShadow,
					Samples = 1,
					MaxBounces = 1024,
					SizeU = l.SizeU,
					SizeV = l.SizeV,
					AxisU = l.AxisU,
					AxisV = l.AxisV,
				};

				switch (l.Type)
				{
					case LightType.Area:
						break;
					case LightType.Point:
						break;
					case LightType.Spot:
						light.SpotAngle = l.SpotAngle;
						light.SpotSmooth = l.SpotSmooth;
						break;
					case LightType.Distant:
						break;
				}

				light.TagUpdate();
				m_light_db.RecordLightRelation(l.Id, light);
			}

			// update existing ones
			foreach (var l in m_light_db.LightsToUpdate)
			{
				var existing_l = m_light_db.ExistingLight(l.Id);
				m_render_engine.ReCreateSimpleEmissionShader(existing_l.Shader, l);
				existing_l.Type = l.Type;
				existing_l.Size = l.Size;
				existing_l.Location = l.Co;
				existing_l.Direction = l.Dir;
				existing_l.UseMis = l.UseMis;
				existing_l.CastShadow = l.CastShadow;
				existing_l.Samples = 1;
				existing_l.MaxBounces = 1024;
				existing_l.SizeU = l.SizeU;
				existing_l.SizeV = l.SizeV;
				existing_l.AxisU = l.AxisU;
				existing_l.AxisV = l.AxisV;

				switch (l.Type)
				{
					case LightType.Area:
						break;
					case LightType.Point:
						break;
					case LightType.Spot:
						existing_l.SpotAngle = l.SpotAngle;
						existing_l.SpotSmooth = l.SpotSmooth;
						break;
					case LightType.Distant:
						break;
				}
				existing_l.TagUpdate();
			}
		}

		/// <summary>
		/// Handle light changes
		/// </summary>
		/// <param name="lightChanges"></param>
		protected override void ApplyLightChanges(List<CqLight> lightChanges)
		{
			foreach (var light in lightChanges)
			{
				var cl = m_shader_converter.ConvertLight(this, light, m_current_view_info, Gamma);

				//System.Diagnostics.Debug.WriteLine("light {0} == {1} == {2} ({3})", light.Id, cl.Id, lg.Id, light.ChangeType);

				m_light_db.AddLight(cl);
			}
		}

		protected override void ApplyDynamicLightChanges(List<Light> dynamicLightChanges)
		{
			foreach (var light in dynamicLightChanges)
			{
				var cl = m_shader_converter.ConvertLight(light, Gamma);
				//System.Diagnostics.Debug.WriteLine("dynlight {0} @ {1}", light.Id, light.Location);
				m_light_db.AddLight(cl);
			}
		}

		/// <summary>
		/// Sun ID
		/// </summary>
		private readonly Guid m_sun_guid = new Guid("82FE2C29-9632-473D-982B-9121E150E1D2");

		/// <summary>
		/// Handle sun changes
		/// </summary>
		/// <param name="sun"></param>
		protected override void ApplySunChanges(Light sun)
		{
			var cl = m_shader_converter.ConvertLight(sun, Gamma);
			cl.Id = m_sun_guid;
			m_light_db.AddLight(cl);
			//System.Diagnostics.Debug.WriteLine("Sun {0} {1} {2}", sun.Id, sun.Intensity, sun.Diffuse);
		}

		#endregion

		public void UploadEnvironmentChanges()
		{
			if (m_env_db.BackgroundHasChanged)
			{
				var curbg = m_env_db.CurrentBackgroundShader;
				m_render_engine.RecreateBackgroundShader(m_env_db.CyclesShader, out curbg);
			}
		}

		/// <summary>
		/// Upload object changes
		/// </summary>
		public void UploadObjectChanges()
		{
			// first delete objects
			foreach (var ob in m_object_db.DeletedObjects)
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
			foreach (var ob in m_object_db.NewOrUpdatedObjects)
			{
				// mesh for this object id
				var mesh = m_object_db.FindMeshRelation(ob.meshid);

				// hmm, no mesh. Oh well, lets get on with the next
				if (mesh == null) continue;

				// see if we already have an object here.
				// update it, otherwise create new one
				var cob = m_object_db.FindObjectRelation(ob.obid);

				var newcob = cob == null;

				// new object, so lets create it and record necessary stuff about it
				if (newcob)
				{
					cob = new CclObject(m_render_engine.Client);
					m_object_db.RecordObjectRelation(ob.obid, cob);
					m_object_db.RecordObjectIdMeshIdRelation(ob.obid, ob.meshid);
				}

				// set mesh reference and other stuff
				cob.Mesh = mesh;
				cob.Transform = ob.Transform;
				cob.Visibility = ob.Visible ? PathRay.AllVisibility : PathRay.Hidden;
				cob.TagUpdate();
			}
		}

		/// <summary>
		/// Handle skylight changes
		/// </summary>
		/// <param name="skylight">New skylight information</param>
		protected override void ApplySkylightChanges(CqSkylight skylight)
		{
			//System.Diagnostics.Debug.WriteLine("{0}", skylight);
			m_env_db.SetSkylightEnabled(skylight.Enabled);
			m_env_db.SetGamma(Gamma);
		}

		protected override void ApplyBackgroundChanges(RenderSettings rs)
		{
			if (rs != null)
			{
				//System.Diagnostics.Debug.WriteLine("ApplyBackgroundChanges: fillstyle {0} color1 {1} color2 {2}", rs.BackgroundStyle, rs.BackgroundColorTop, rs.BackgroundColorBottom);
				m_env_db.SetBackgroundData(rs.BackgroundStyle, rs.BackgroundColorTop, rs.BackgroundColorBottom);
				m_env_db.SetGamma(Gamma);
				m_render_engine.Settings.SetQuality(rs.AntialiasLevel);
			}
		}

		/// <summary>
		/// Handle environment changes
		/// </summary>
		/// <param name="usage"></param>
		protected override void ApplyEnvironmentChanges(RenderEnvironment.Usage usage)
		{
			var env_id = EnvironmentIdForUsage(usage);
			var env = EnvironmentForid(env_id);
			m_env_db.SetBackground(env, usage);
			m_env_db.SetGamma(Gamma);

			//System.Diagnostics.Debug.WriteLine("{0}, env {1}", usage, env);
		}

		/// <summary>
		/// We get notified of (dynamic?) changes.
		/// </summary>
		protected override void NotifyBeginUpdates()
		{
			// nothing
		}

		/// <summary>
		/// Changes have been signalled.
		/// </summary>
		protected override void NotifyEndUpdates()
		{
			m_render_engine.Flush = true;
		}

		protected override void NotifyDynamicUpdatesAreAvailable()
		{
			// nothing
			//System.Diagnostics.Debug.WriteLine("dyn changes...");
		}

		/// <summary>
		/// Tell ChangeQueue we want baking for
		/// - Decals
		/// - ProceduralTextures
		/// - MultipleMappingChannels
		/// </summary>
		/// <returns></returns>
		protected override BakingFunctions BakeFor()
		{
			return BakingFunctions.Decals | BakingFunctions.ProceduralTextures | BakingFunctions.MultipleMappingChannels;
		}

	}
}
