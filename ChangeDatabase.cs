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
using RhinoCycles.Shaders;
using sdd = System.Diagnostics.Debug;
using CQMaterial = Rhino.Render.ChangeQueue.Material;
using CQMesh = Rhino.Render.ChangeQueue.Mesh;
using GroundPlane = Rhino.Render.ChangeQueue.GroundPlane;
using Light = Rhino.Render.ChangeQueue.Light;
using Skylight = Rhino.Render.ChangeQueue.Skylight;
using CQLinearWorkflow = Rhino.Render.ChangeQueue.LinearWorkflow;
using CclLight = ccl.Light;

namespace RhinoCycles
{
	public class ChangeDatabase : ChangeQueue
	{
		/// <summary>
		/// RhinoCycles shaders and Cycles shaders relations
		/// </summary>
		public readonly List<Tuple<object, Shader>> m_all_shaders = new List<Tuple<object, Shader>>();

		/// <summary>
		/// Reference to the Cycles render engine C# level implementation.
		/// </summary>
		RenderEngine RenderEngine { get; set; }

		private ViewInfo m_current_view_info;

		/// <summary>
		/// Constructor for our changequeue implementation
		/// </summary>
		/// <param name="pluginId">Id of the plugin instantiating the render change queue</param>
		/// <param name="engine">Reference to our render engine</param>
		/// <param name="doc">Document runtime serial number</param>
		/// <param name="view">Reference to the RhinoView for which this queue is created.</param>
		internal ChangeDatabase(Guid pluginId, RenderEngine engine, uint doc, RhinoView view) : base(pluginId, doc, view)
		{
			RenderEngine = engine;
		}


		/// <summary>
		/// Constructor for our changequeue implementation
		/// </summary>
		/// <param name="pluginId">Id of the plugin instantiating the render change queue</param>
		/// <param name="engine">Reference to our render engine</param>
		/// <param name="createPreviewEventArgs">preview event arguments</param>
		internal ChangeDatabase(Guid pluginId, RenderEngine engine, CreatePreviewEventArgs createPreviewEventArgs) : base(pluginId, createPreviewEventArgs)
		{
			RenderEngine = engine;
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

		public void UploadGammaChanges()
		{
			if (GammaHasChanged)
			{
				Plugin.ApplyGammaToTextures(Gamma);


				if (m_current_background_shader != null)
				{
					m_current_background_shader.Reset();
					RenderEngine.Session.Scene.Background.Shader = m_current_background_shader.GetShader();
				}

				foreach (var tup in m_all_shaders)
				{
					var matsh = tup.Item1 as CyclesShader;
					if (matsh != null)
					{
						matsh.Gamma = Gamma;
						RenderEngine.RecreateMaterialShader(matsh, tup.Item2);
						tup.Item2.Tag();
					}

					var lgsh = tup.Item1 as CyclesLight;
					if (lgsh != null)
					{
						lgsh.Gamma = Gamma;
						RenderEngine.ReCreateSimpleEmissionShader(tup.Item2, lgsh);
						tup.Item2.Tag();
					}

				}

				RenderEngine.Session.Scene.Film.Exposure = Gamma;
				RenderEngine.Session.Scene.Film.Update();
			}
		}

		public void ClearChanges()
		{
			ClearGamma();
			ClearLinearWorkflow();
			ClearBackground();
			ClearViewChanges();
			ClearLights();
		}

		public bool HasChanges()
		{
			return
				m_cq_view_changes.Any() ||
				m_cq_light_changes.Any() || 
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
		/// record view changes to push to cycles
		/// </summary>
		private readonly List<CyclesView> m_cq_view_changes = new List<CyclesView>();

		/// <summary>
		/// Clear view change queue
		/// </summary>
		private void ClearViewChanges()
		{
			m_cq_view_changes.Clear();
		}

		/// <summary>
		/// Record view change
		/// </summary>
		/// <param name="t">view info</param>
		private void ChangeView(CyclesView t)
		{
			m_cq_view_changes.Add(t);
		}

		/// <summary>
		/// Upload camera (viewport) changes to Cycles.
		/// </summary>
		public void UploadCameraChanges()
		{
			if (m_cq_view_changes.Count <= 0) return;

			var view = m_cq_view_changes.Last();
			UploadCamera(view);
		}

		/// <summary>
		/// Set the camera based on CyclesView
		/// </summary>
		/// <param name="view"></param>
		private void UploadCamera(CyclesView view)
		{
			var scene = RenderEngine.Session.Scene;
			RenderEngine.RenderDimension = new Size(view.Width, view.Height);
			var size = RenderEngine.RenderDimension;
			RenderEngine.UnsetRenderSize();

			var ha = size.Width > size.Height ? view.Horizontal: view.Vertical;

			var angle = (float) Math.Atan(Math.Tan(ha)/view.ViewAspectRatio) * 2.0f;

			//System.Diagnostics.Debug.WriteLine("size: {0}, matrix: {1}, angle: {2}, Sensorsize: {3}x{4}", size, view.Transform, angle, Settings.SensorHeight, Settings.SensorWidth);

			scene.Camera.Size = size;
			scene.Camera.Matrix = view.Transform;
			scene.Camera.Type = view.Projection;
			scene.Camera.Fov = angle;
			if (view.Projection == CameraType.Orthographic || view.TwoPoint) scene.Camera.SetViewPlane(view.Viewplane.Left, view.Viewplane.Right, view.Viewplane.Top, view.Viewplane.Bottom);
			else if(view.Projection == CameraType.Perspective) scene.Camera.ComputeAutoViewPlane();
			scene.Camera.SensorHeight = RenderEngine.Settings.SensorHeight;
			scene.Camera.SensorWidth = RenderEngine.Settings.SensorWidth;
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
			ChangeView(cyclesview);
		}

		/// <summary>
		/// Handle mesh changes
		/// </summary>
		/// <param name="deleted"></param>
		/// <param name="added"></param>
		protected override void ApplyMeshChanges(Guid[] deleted, List<CQMesh> added)
		{
			//System.Diagnostics.Debug.WriteLine("ChangeDatabase ApplyMeshChanges, deleted {0}, added {1}", deleted.Length, added.Count);

			foreach (var guid in deleted)
			{
				// only delete those that aren't listed in the added list
				if (!(from mesh in added where mesh.Id() == guid select mesh).Any())
				{
					//System.Diagnostics.Debug.WriteLine("Deleting {0}", guid);
					RenderEngine.DeleteMesh(guid);
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

					var crc = RenderEngine.FindRenderHashForMeshId(meshid);
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
					RenderEngine.AddMesh(cycles_mesh);
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
				var cob = RenderEngine.FindObjectRelation(d);
				var delob = new CyclesObject {cob = cob};
				RenderEngine.AddObjectDelete(delob);
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

				RenderEngine.AddObjectMaterialChange(shaderchange);

				RenderEngine.RecordRenderHashRelation(a.MaterialId, meshid, a.InstanceId);
				RenderEngine.RecordObjectIdMeshIdRelation(a.InstanceId, meshid);
				RenderEngine.AddNewOrUpdateObject(ob);
			}
		}

		/// <summary>
		/// Handle RenderMaterial - will queue new shader if necessary
		/// </summary>
		/// <param name="mat"></param>
		private void HandleRenderMaterial(RenderMaterial mat)
		{
			if (RenderEngine.HasShader(mat.RenderHash)) return;

			//System.Diagnostics.Debug.WriteLine("Add new material with RenderHash {0}", mat.RenderHash);
			var sh = Plugin.CreateCyclesShader(mat.TopLevelParent as RenderMaterial, Gamma);
			RenderEngine.AddShader(sh);
		}

		/// <summary>
		/// Handle changes in materials to create (or re-use) shaders.
		/// </summary>
		/// <param name="mats">List of <c>CQMaterial</c></param>
		protected override void ApplyMaterialChanges(List<CQMaterial> mats)
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
				var existing = RenderEngine.GetShaderFromHash(distinct);
				if (existing == null)
				{
					var rm = MaterialFromId(distinct);
					HandleRenderMaterial(rm);
				}
			}
		}

		/// <summary>
		/// Handle ground plane changes.
		/// </summary>
		/// <param name="gp"></param>
		protected override void ApplyGroundPlaneChanges(GroundPlane gp)
		{
			//System.Diagnostics.Debug.WriteLine("groundplane");
			if(!RenderEngine.GroundPlaneInitialised) RenderEngine.InitialiseGroundPlane(gp);
			// find groundplane
			var altitude = (float)gp.Altitude;
			var t = Transform.Translate(0.0f, 0.0f, altitude);
			var cycles_object = new CyclesObject
			{
				meshid = RenderEngine.GroundPlaneId,
				obid = RenderEngine.GroundPlaneMeshInstanceId,
				matid = gp.MaterialId,
				Transform = t,
				Visible = gp.Enabled
			};
			RenderEngine.AddNewOrUpdateObject(cycles_object);

			var mat = MaterialFromId(gp.MaterialId);
			HandleRenderMaterial(mat);

			var obid = RenderEngine.GroundPlaneMeshInstanceId;

			HandleMaterialChangeOnObject(mat, obid);
		}

		private void HandleMaterialChangeOnObject(RenderMaterial mat, uint obid)
		{
			var oldhash = RenderEngine.FindRenderHashForObjectId(obid);
			// skip if no change in renderhash
			if (oldhash != mat.RenderHash)
			{
				var o = new CyclesObjectShader(obid)
				{
					NewShaderHash = mat.RenderHash,
					OldShaderHash = oldhash
				};

				//System.Diagnostics.Debug.WriteLine("CQMat.Id: {0} meshinstanceid: {1}", mat.Id, obid);

				RenderEngine.AddObjectMaterialChange(o);
			}
		}

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
				RenderEngine.AddDynamicObjectTransform(cot);
			}
		}

		#region LIGHT & SUN

		/// <summary>
		/// record light changes to push to cycles
		/// </summary>
		private readonly List<CyclesLight> m_cq_light_changes = new List<CyclesLight>();
		/// <summary>
		/// record what Guid corresponds to what light in cycles
		/// </summary>
		private readonly Dictionary<Guid, CclLight> m_rh_ccl_lights = new Dictionary<Guid, CclLight>();

		/// <summary>
		/// Clear out list of light changes.
		/// </summary>
		public void ClearLights()
		{
			m_cq_light_changes.Clear();
		}

		/// <summary>
		/// Record light changes
		/// </summary>
		/// <param name="light"></param>
		public void AddLight(CyclesLight light)
		{
			m_cq_light_changes.Add(light);
		}

		/// <summary>
		/// Record Cycles lights that correspond to specific Rhino light ID
		/// </summary>
		/// <param name="id"></param>
		/// <param name="cLight"></param>
		public void RecordLightRelation(Guid id, CclLight cLight)
		{
			m_rh_ccl_lights[id] = cLight;
		}

		/// <summary>
		/// Upload all light changes to the Cycles render engine
		/// </summary>
		public void UploadLightChanges()
		{
			var light_ids = (from light in m_cq_light_changes select light.Id).ToList();
			var add_ids = from lightkey in light_ids where !m_rh_ccl_lights.ContainsKey(lightkey) select lightkey;
			var update_ids = from lightkey in light_ids where m_rh_ccl_lights.ContainsKey(lightkey) select lightkey;

			var add_lights = (from aid in add_ids from ll in m_cq_light_changes where aid == ll.Id select ll).ToList();
			var update_lights = (from uid in update_ids from ll in m_cq_light_changes where uid == ll.Id select ll).ToList();

			/* new light shaders and lights. */
			foreach (var l in add_lights)
			{
				if (RenderEngine.CancelRender) return;

				var lgsh = RenderEngine.CreateSimpleEmissionShader(l);
				RenderEngine.Client.Scene.AddShader(lgsh);
				m_all_shaders.Add(new Tuple<object, Shader>(l, lgsh));

				if (RenderEngine.CancelRender) return;

				var light = new CclLight(RenderEngine.Client, RenderEngine.Client.Scene, lgsh)
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
				RecordLightRelation(l.Id, light);
			}

			// update existing ones
			foreach (var l in update_lights)
			{
				var existing_l = m_rh_ccl_lights[l.Id];
				RenderEngine.ReCreateSimpleEmissionShader(existing_l.Shader, l);
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
		protected override void ApplyLightChanges(List<Light> lightChanges)
		{
			foreach (var light in lightChanges)
			{

				var cl = Plugin.ConvertLight(this, light, m_current_view_info, Gamma);

				//System.Diagnostics.Debug.WriteLine("light {0} == {1} == {2} ({3})", light.Id, cl.Id, lg.Id, light.ChangeType);

				AddLight(cl);
			}
		}

		protected override void ApplyDynamicLightChanges(List<Rhino.Geometry.Light> dynamicLightChanges)
		{
			foreach (var light in dynamicLightChanges)
			{
				var cl = Plugin.ConvertLight(light, Gamma);
				//System.Diagnostics.Debug.WriteLine("dynlight {0} @ {1}", light.Id, light.Location);
				AddLight(cl);
			}
		}

		/// <summary>
		/// Handle sun changes
		/// </summary>
		/// <param name="sun"></param>
		protected override void ApplySunChanges(Rhino.Geometry.Light sun)
		{
			var cl = Plugin.ConvertLight(sun, Gamma);
			cl.Id = RenderEngine.SunId;
			AddLight(cl);
			//System.Diagnostics.Debug.WriteLine("Sun {0} {1} {2}", sun.Id, sun.Intensity, sun.Diffuse);
		}

		#endregion

		/// <summary>
		/// record background shader changes to push to cycles
		/// note that we have only one object that gets updated when necessary.
		/// </summary>
		public CyclesBackground m_cq_background = new CyclesBackground();
		public RhinoShader m_current_background_shader;

		public bool BackgroundHasChanged
		{
			get { return m_cq_background.modified; }
		}

		private void ClearBackground()
		{
			m_cq_background.Clear();
		}

		public void UploadEnvironmentChanges()
		{
			if(BackgroundHasChanged)
				RenderEngine.RecreateBackgroundShader();
		}

		/// <summary>
		/// Handle skylight changes
		/// </summary>
		/// <param name="skylight">New skylight information</param>
		protected override void ApplySkylightChanges(Skylight skylight)
		{
			//System.Diagnostics.Debug.WriteLine("{0}", skylight);
			m_cq_background.skylight_enabled =  skylight.Enabled;
			m_cq_background.gamma = Gamma;
			m_cq_background.modified = true;
		}

		protected override void ApplyBackgroundChanges(RenderSettings rs)
		{
			if (rs != null)
			{
				//System.Diagnostics.Debug.WriteLine("ApplyBackgroundChanges: fillstyle {0} color1 {1} color2 {2}", rs.BackgroundStyle, rs.BackgroundColorTop, rs.BackgroundColorBottom);
				m_cq_background.background_fill = rs.BackgroundStyle;
				m_cq_background.color1 = rs.BackgroundColorTop;
				m_cq_background.color2 = rs.BackgroundColorBottom;
				m_cq_background.gamma = Gamma;
				m_cq_background.modified = true;
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
			switch (usage)
			{
				case RenderEnvironment.Usage.Background:
					m_cq_background.background_environment = env;
					break;
				case RenderEnvironment.Usage.Skylighting:
					m_cq_background.skylight_environment = env;
					break;
				case RenderEnvironment.Usage.ReflectionAndRefraction:
					m_cq_background.reflection_environment = env;
					break;
			}
			m_cq_background.gamma = Gamma;

			m_cq_background.HandleEnvironments();

			m_cq_background.modified = true;

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
			RenderEngine.Flush = true;
		}

		protected override void NotifyDynamicUpdatesAreAvailable()
		{
			// nothing
			//System.Diagnostics.Debug.WriteLine("dyn changes...");
		}

		protected override BakingFunctions BakeFor()
		{
			return BakingFunctions.Decals | BakingFunctions.ProceduralTextures | BakingFunctions.MultipleMappingChannels;
		}
	}
}
