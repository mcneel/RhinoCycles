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
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ccl;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;
using Rhino.Render.ChangeQueue;
using sdd = System.Diagnostics.Debug;
using CqMaterial = Rhino.Render.ChangeQueue.Material;
using CqMesh = Rhino.Render.ChangeQueue.Mesh;
using CqGroundPlane = Rhino.Render.ChangeQueue.GroundPlane;
using CqLight = Rhino.Render.ChangeQueue.Light;
using CqSkylight = Rhino.Render.ChangeQueue.Skylight;
using CclLight = ccl.Light;
using CclMesh = ccl.Mesh;
using CclObject = ccl.Object;
using RGLight = Rhino.Geometry.Light;
using Rhino.Geometry;
using RhinoCyclesCore.Converters;
using RhinoCyclesCore.Core;
using RhinoCyclesCore.Shaders;
using RhinoCyclesCore.ExtensionMethods;

namespace RhinoCyclesCore.Database
{
	public class ChangeDatabase : ChangeQueue
	{
		/// <summary>
		/// Reference to the Cycles render engine C# level implementation.
		/// </summary>
		private readonly RenderEngine _renderEngine;

		/// <summary>
		/// Note that this ViewInfo is valid only during the Apply* function calls
		/// for the ongoing Flush. At the end this should be set to null.
		/// </summary>
		private ViewInfo _currentViewInfo;

		#region DATABASES

		/// <summary>
		/// Database responsible for keeping track of objects/meshes and their shaders.
		/// </summary>
		private readonly ObjectShaderDatabase _objectShaderDatabase;

		/// <summary>
		/// Database responsible for all material shaders
		/// </summary>
		private readonly ShaderDatabase _shaderDatabase = new ShaderDatabase();

		/// <summary>
		/// Database responsible for keeping track of objects and meshes and their relations between
		/// Rhino and Cycles.
		/// </summary>
		private readonly ObjectDatabase _objectDatabase = new ObjectDatabase();

		/// <summary>
		/// The database responsible for keeping track of light changes and the relations between Rhino
		/// and Cycles lights and their shaders.
		/// </summary>
		private LightDatabase _lightDatabase { get; } = new LightDatabase();

		/// <summary>
		/// Database responsible for keeping track of background and environment changes and their
		/// relations between Rhino and Cycles.
		/// </summary>
		private readonly EnvironmentDatabase _environmentDatabase = new EnvironmentDatabase();

		/// <summary>
		/// Database responsible for managing camera transforms from Rhino to Cycles.
		/// </summary>
		private readonly CameraDatabase _cameraDatabase = new CameraDatabase();

		/// <summary>
		/// Database responsible for managing render settings.
		/// </summary>
		private readonly RenderSettingsDatabase _renderSettingsDatabase = new RenderSettingsDatabase();

		#endregion

		private readonly ShaderConverter _shaderConverter = new ShaderConverter();

		private readonly bool _modalRenderer;

		public bool SupportClippingPlanes { get; set; }

		public uint Blades { get; } = RcCore.It.EngineSettings.Blades;
		public float BladesRotation { get; } = RcCore.It.EngineSettings.BladesRotation;
		public float ApertureRatio { get; } = RcCore.It.EngineSettings.ApertureRatio;

		internal ChangeDatabase(Guid pluginId, RenderEngine engine, uint doc, ViewInfo view, DisplayPipelineAttributes attributes, bool modal) : base(pluginId, doc, view, attributes, true, !modal)
		{
			_renderEngine = engine;
			_objectShaderDatabase = new ObjectShaderDatabase(_objectDatabase);
			_modalRenderer = modal;
		}


		/// <summary>
		/// Constructor for our changequeue implementation
		/// </summary>
		/// <param name="pluginId">Id of the plugin instantiating the render change queue</param>
		/// <param name="engine">Reference to our render engine</param>
		/// <param name="createPreviewEventArgs">preview event arguments</param>
		internal ChangeDatabase(Guid pluginId, RenderEngine engine, CreatePreviewEventArgs createPreviewEventArgs) : base(pluginId, createPreviewEventArgs)
		{
			_renderEngine = engine;
			_modalRenderer = true;
			_objectShaderDatabase = new ObjectShaderDatabase(_objectDatabase);
		}

		protected override void Dispose(bool isDisposing)
		{
			_environmentDatabase?.Dispose();
			_objectShaderDatabase?.Dispose();
			_objectDatabase?.Dispose();
			base.Dispose(isDisposing);
		}

		/// <summary>
		/// Change shaders on objects and their meshes
		/// </summary>
		public void UploadObjectShaderChanges()
		{
			RcCore.OutputDebugString($"Uploading object shader changes {_shaderDatabase.ObjectShaderChanges.Count}\n");
			foreach (var obshad in _shaderDatabase.ObjectShaderChanges)
			{

				var cob = _objectDatabase.FindObjectRelation(obshad.Id);
				if(cob!=null)
				{
					// get shaders
					var newShader = _shaderDatabase.GetShaderFromHash(obshad.NewShaderHash);
					var oldShader = _shaderDatabase.GetShaderFromHash(obshad.OldShaderHash);
					if (newShader != null)
					{
						cob.Mesh?.ReplaceShader(newShader);
						newShader.Tag();
					}
					oldShader?.Tag();
					cob.TagUpdate();
					_objectShaderDatabase.ReplaceShaderRelation(obshad.OldShaderHash, obshad.NewShaderHash, obshad.Id);
				}
			}
		}

		public event EventHandler<LinearWorkflowChangedEventArgs> LinearWorkflowChanged;
		public event EventHandler<MaterialShaderUpdatedEventArgs> MaterialShaderChanged;
		public event EventHandler<LightShaderUpdatedEventArgs> LightShaderChanged;
		public event EventHandler FilmUpdateTagged;

		public void UploadGammaChanges()
		{
			if (LinearWorkflowHasChanged)
			{
				BitmapConverter.ApplyGammaToTextures(PreProcessGamma);

				_environmentDatabase.CurrentBackgroundShader?.Reset();

				foreach (var tup in _shaderDatabase.AllShaders)
				{
					var cclsh = tup.Item2;
					if (tup.Item1 is CyclesShader matsh)
					{
						RcCore.OutputDebugString($"Updating material {cclsh.Id}, old gamma {matsh.Gamma} new gamma ");
						matsh.Gamma = PreProcessGamma;
						RcCore.OutputDebugString($"{matsh.Gamma}\n");
						TriggerMaterialShaderChanged(matsh, cclsh);
					}

					if (tup.Item1 is CyclesLight lgsh)
					{
						RcCore.OutputDebugString($"Updating light {cclsh.Id}, old gamma {lgsh.Gamma} new gamma ");
						lgsh.Gamma = PreProcessGamma;
						RcCore.OutputDebugString($"{lgsh.Gamma}\n");
						TriggerLightShaderChanged(lgsh, cclsh);
					}

				}

				TriggerLinearWorkflowUploaded();
				TriggerFilmUpdateTagged();
			}
		}

		internal void TriggerFilmUpdateTagged()
		{
			FilmUpdateTagged?.Invoke(this, EventArgs.Empty);
		}

		internal void TriggerMaterialShaderChanged(CyclesShader rcShader, Shader cclShader)
		{
			MaterialShaderChanged?.Invoke(this, new MaterialShaderUpdatedEventArgs(rcShader, cclShader));
		}

		internal void TriggerLightShaderChanged(CyclesLight rcLightShader, Shader cclShader)
		{
			LightShaderChanged?.Invoke(this, new LightShaderUpdatedEventArgs(rcLightShader, cclShader));
		}

		internal void TriggerLinearWorkflowUploaded()
		{
			LinearWorkflowChanged?.Invoke(this, new LinearWorkflowChangedEventArgs(LinearWorkflow));
		}

		/// <summary>
		/// Handle dynamic object transforms
		/// </summary>
		public void UploadDynamicObjectTransforms()
		{
			foreach (var cot in _objectDatabase.ObjectTransforms)
			{
				var cob = _objectDatabase.FindObjectRelation(cot.Id);
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
			RcCore.OutputDebugString("UploadMeshChanges\n");
			// handle mesh deletes first
			foreach (var meshDelete in _objectDatabase.MeshesToDelete)
			{
				var cobs = _objectDatabase.GetCyclesObjectsForGuid(meshDelete);

				foreach (var cob in cobs)
				{
					RcCore.OutputDebugString($"\tDeleting mesh {cob.Id}.{cob.Mesh.Id} ({meshDelete}\n");
					// remove mesh data
					cob.Mesh.ClearData();
					cob.Mesh.TagRebuild();
					// hide object containing the mesh
					cob.Visibility = PathRay.Hidden;
					cob.TagUpdate();
				}
			}

			var curmesh = 0;
			var totalmeshes = _objectDatabase.MeshChanges.Count;
			RcCore.OutputDebugString($"\tUploading {totalmeshes} mesh changes\n");
			foreach (var meshChange in _objectDatabase.MeshChanges)
			{
				var cyclesMesh = meshChange.Value;
				var mid = meshChange.Key;

				var me = _objectDatabase.FindMeshRelation(mid);

				// newme true if we have to upload new mesh data
				var newme = me == null;

				if (_renderEngine.CancelRender) return;

				// lets find the shader for this, or use 0 if none found.
				uint shid;
				var matid = _objectShaderDatabase.FindRenderHashForMeshId(cyclesMesh.MeshId);
				try
				{
					// @todo check this is correct naming and dictionary to query from
					shid = _shaderDatabase.GetShaderIdForMatId(matid);
				}
				catch (Exception)
				{
					shid = 0;
				}

				var shader = _renderEngine.Client.Scene.ShaderFromSceneId(shid);

				// creat a new mesh to upload mesh data to
				if (newme)
				{
					me = new CclMesh(_renderEngine.Client, shader);
				}

				me.Resize((uint)cyclesMesh.Verts.Length/3, (uint)cyclesMesh.Faces.Length/3);

				// update status bar of render window.
				var stat =
					$"Upload mesh {curmesh}/{totalmeshes} [v: {cyclesMesh.Verts.Length/3}, t: {cyclesMesh.Faces.Length/3} using shader {shid}]";
				RcCore.OutputDebugString($"\t\t{stat}\n");

				// set progress, but without rendering percentage (hence the -1.0f)
				_renderEngine.SetProgress(_renderEngine.RenderWindow, stat, -1.0f);

				// upload, if we get false back we were signalled to stop rendering by user
				if (!UploadMeshData(me, cyclesMesh)) return;

				// if we re-uploaded mesh data, we need to make sure the shader
				// information doesn't get lost.
				if (!newme) me.ReplaceShader(shader);

				// don't forget to record this new mesh
				if(newme) _objectDatabase.RecordObjectMeshRelation(cyclesMesh.MeshId, me);
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
			var verts = cyclesMesh.Verts;
			me.SetVerts(ref verts);
			if (_renderEngine.CancelRender) return false;
			// set the triangles
			var faces = cyclesMesh.Faces;
			me.SetVertTris(ref faces, cyclesMesh.VertexNormals != null);
			if (_renderEngine.CancelRender) return false;
			// set vertex normals
			if (cyclesMesh.VertexNormals != null)
			{
				var vertex_normals = cyclesMesh.VertexNormals;
				me.SetVertNormals(ref vertex_normals);
			}
			if (_renderEngine.CancelRender) return false;
			// set uvs
			if (cyclesMesh.Uvs != null)
			{
				var uvs = cyclesMesh.Uvs;
				me.SetUvs(ref uvs);
			}
			// set vertex colors
			if(cyclesMesh.VertexColors != null)
			{
				var vcs = cyclesMesh.VertexColors;
				me.SetVertexColors(ref vcs);
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
			_currentViewInfo = null;
			DisplayPipelineAttributesChanged = false;
			ClearLinearWorkflow();
			_environmentDatabase.ResetBackgroundChangeQueue();
			_cameraDatabase.ResetViewChangeQueue();
			_lightDatabase.ResetLightChangeQueue();
			_shaderDatabase.ClearShaders();
			_shaderDatabase.ClearObjectShaderChanges();
			_objectDatabase.ResetObjectsChangeQueue();
			_objectDatabase.ResetMeshChangeQueue();
			_objectDatabase.ResetDynamicObjectTransformChangeQueue();
		}

		/// <summary>
		/// Tell if any changes have been recorded by the ChangeQueue mechanism since
		/// the last flush.
		/// </summary>
		/// <returns>True if changes where recorded, false otherwise.</returns>
		public bool HasChanges()
		{
			return
				_cameraDatabase.HasChanges() ||
				_environmentDatabase.BackgroundHasChanged ||
				_lightDatabase.HasChanges() || 
				_shaderDatabase.HasChanges() ||
				_objectDatabase.HasChanges() ||
				LinearWorkflowHasChanged ||
				DisplayPipelineAttributesChanged;
		}


		private LinearWorkflow _linearWorkflow = new LinearWorkflow();

		public bool LinearWorkflowHasChanged { get; private set; }

		public float PreProcessGamma => _linearWorkflow.PreProcessColors || _linearWorkflow.PreProcessTextures ? _linearWorkflow.PreProcessGamma : 1.0f;

		public LinearWorkflow LinearWorkflow
		{
			set
			{
				if (_linearWorkflow.Equals(value)) return;

				_linearWorkflow.CopyFrom(value);
				LinearWorkflowHasChanged = true;
			}
			get
			{
				return _linearWorkflow;
			}
		}

		private void ClearLinearWorkflow()
		{
			LinearWorkflowHasChanged = false;
		}

		protected override void ApplyLinearWorkflowChanges(Rhino.Render.LinearWorkflow lw)
		{
			sdd.WriteLine($"LinearWorkflow {lw.PreProcessColors} {lw.PreProcessTextures} {lw.PostProcessFrameBuffer} {lw.PreProcessGamma} {lw.PostProcessGammaReciprocal}");
			LinearWorkflow = lw;
			_environmentDatabase.SetGamma(PreProcessGamma);
		}


		private const uint ClippingPlaneMeshInstanceId = 2;
		private readonly float cp_side_extension = 1.0E+7f;

		private readonly Tuple<Guid, int> _clippingPlaneGuid = new Tuple<Guid, int>(new Guid("6A7DB550-7E42-4129-A36D-A4C8AAB06F4B"), 0);
		private readonly Dictionary<Guid, Plane> ClippingPlanes = new Dictionary<Guid, Plane>(100);

		protected override void ApplyClippingPlaneChanges(Guid[] deleted, List<ClippingPlane> addedOrModified)
		{
			if (!SupportClippingPlanes)
			{
				sceneBoundingBoxDirty = true;
				ClippingPlanes.Clear();
				return;
			}

			SceneBoundingBox = GetQueueSceneBoundingBox();
			foreach (var d in deleted)
			{
				ClippingPlanes.Remove(d);
			}
			foreach (var cp in addedOrModified)
			{
				if (cp.IsEnabled && cp.ViewIds.Contains(ViewId))
					ClippingPlanes[cp.Id] = new Plane(cp.Plane);
				else
					ClippingPlanes.Remove(cp.Id);
			}
			sceneBoundingBoxDirty = true;
			CalculateClippingObjects();
		}

		public void CalculateClippingObjects()
		{
			if (sceneBoundingBoxDirty)
			{
				var sbb = SceneBoundingBox;
				sbb.Inflate(0.1);
				var sbbr = sbb.ToBrep();
				sbb.Inflate(0.1);
				var sbbbrep = sbb.ToBrep();
				var sbbl = new List<Brep>(1)
					{
						sbbr
					};

				List<Plane> planes = new List<Plane>(10);
				foreach(var pa in ClippingPlanes.Values)
				{
					int i = 0;
					int dropab = 0; // -1: drop a, 0: drop neither, 1: drop b
					foreach (var pb in planes)
					{
						if (pb.Normal.IsParallelTo(pa.Normal, Math.PI / 720.0) == 1)
						{
							if (pb.DistanceTo(pa.Origin) < 0.0)
							{
								dropab = -1;
							}
							else
							{
								dropab = 1;
							}
							break;
						}
						i++;
					}
					if (dropab == -1) continue;
					if (dropab == 1)
					{
						planes.RemoveAt(i);
					}
					planes.Add(pa);
				}

				var xext = new Interval(-cp_side_extension, cp_side_extension);
				var zext = new Interval(0, cp_side_extension);
				List<Brep> boxes = new List<Brep>(ClippingPlanes.Values.Count);
				foreach (var p in planes)
				{
					/*Curve[] crv;
					Point3d[] pts;
					if(Rhino.Geometry.Intersect.Intersection.BrepPlane(sbbbrep, p, 0.01, out crv, out pts))
					{
						p.Origin.CompareTo(p.Origin);
					}*/
					var tp = new Plane(p);
					tp.Flip();
					Box b = new Box(tp, xext, xext, zext);
					boxes.Add(b.ToBrep());
				}
				if (boxes.Count < 1)
				{
					_objectDatabase.DeleteMesh(_clippingPlaneGuid.Item1);
					var cob = _objectDatabase.FindObjectRelation(ClippingPlaneMeshInstanceId);
					if (cob != null)
					{
						var delob = new CyclesObject { cob = cob };
						_objectDatabase.DeleteObject(delob);
					}
				}
				else
				{
					var simplified = Brep.CreateBooleanUnion(boxes, 0.05);

					if (simplified == null) simplified = boxes.ToArray();

					var bounded = Brep.CreateBooleanIntersection(simplified, sbbl, 0.05);
					if (bounded == null) bounded = simplified;

					var meshed =
						(from s in bounded
						 select Rhino.Geometry.Mesh.CreateFromBrep(s, mpclipping)
						 .Aggregate(
							 new Rhino.Geometry.Mesh(),
							 (workingMesh, next) => { workingMesh.Append(next); return workingMesh; }
						 ))
						.Aggregate(
							new Rhino.Geometry.Mesh(),
							(workingMesh, next) => { workingMesh.Append(next); return workingMesh; }
							);
					meshed.RebuildNormals();
					var cpid = _clippingPlaneGuid;

					Rhino.Geometry.Transform tfm = Rhino.Geometry.Transform.Identity;

					HandleMeshData(cpid.Item1, cpid.Item2, meshed, true);

					var mat = Rhino.DocObjects.Material.DefaultMaterial.RenderMaterial;

					var t = ccl.Transform.Translate(0.0f, 0.0f, 0.0f);

					var cyclesObject = new CyclesObject
					{
						matid = mat.RenderHash,
						obid = ClippingPlaneMeshInstanceId,
						meshid = cpid,
						Transform = t,
						Visible = ClippingPlanes.Count > 0,
						CastShadow = false,
						IsShadowCatcher = false,
						Cutout = true,
					};

					_objectShaderDatabase.RecordRenderHashRelation(mat.RenderHash, cpid, ClippingPlaneMeshInstanceId);
					_objectDatabase.RecordObjectIdMeshIdRelation(ClippingPlaneMeshInstanceId, cpid);
					_objectDatabase.AddOrUpdateObject(cyclesObject);
				}
				sceneBoundingBoxDirty = false;
			}
		}

		/// <summary>
		/// Upload camera (viewport) changes to Cycles.
		/// </summary>
		public void UploadCameraChanges()
		{
			if (!_cameraDatabase.HasChanges()) return;

			var view = _cameraDatabase.LatestView();
			if(view!=null)
			{
				UploadCamera(view);
			}
			var fb = _cameraDatabase.GetBlur();
			UploadFocalBlur(fb);
		}

		/// <summary>
		/// Event arguments for ViewChanged event.
		/// </summary>
		public class ViewChangedEventArgs: EventArgs
		{
			/// <summary>
			/// Construct ViewChangedEventArgs
			/// </summary>
			/// <param name="view">The new CRC for the view</param>
			/// <param name="sizeChanged">true if the render size has changed</param>
			/// <param name="newSize">The render size</param>
			public ViewChangedEventArgs(ViewInfo view, bool sizeChanged, Size newSize)
			{
				View = view;
				SizeChanged = sizeChanged;
				NewSize = newSize;
			}

			/// <summary>
			/// View CRC
			/// </summary>
			public ViewInfo View { get; private set; }
			/// <summary>
			/// True if the render size has changed
			/// </summary>
			public bool SizeChanged { get; private set; }
			/// <summary>
			/// The new rendering dimension
			/// </summary>
			public Size NewSize { get; private set; }
		}

		/// <summary>
		/// Event that gets fired when the Rhino viewport has changed. This
		/// event gives the new CRC for the view, true if the render
		/// size has changed and the new render size
		/// </summary>
		public event EventHandler<ViewChangedEventArgs> ViewChanged;

		private void TriggerViewChanged(ViewInfo view, bool sizeChanged, Size newSize)
		{
			ViewChanged?.Invoke(this, new ViewChangedEventArgs(view, sizeChanged, newSize));
		}

		private void UploadFocalBlur(FocalBlur fb)
		{
			var scene = _renderEngine.Session.Scene;
			scene.Camera.FocalDistance = fb.FocalDistance;
			scene.Camera.ApertureSize = fb.FocalAperture;
			scene.Camera.Blades = Blades;
			scene.Camera.BladesRotation = (float)Rhino.RhinoMath.ToRadians(BladesRotation);
			scene.Camera.ApertureRatio = ApertureRatio;

		}

		/// <summary>
		/// Set the camera based on CyclesView
		/// </summary>
		/// <param name="view"></param>
		private void UploadCamera(CyclesView view)
		{
			var scene = _renderEngine.Session.Scene;
			var oldSize = _renderEngine.RenderDimension;
			var newSize = new Size(view.Width, view.Height);
			_renderEngine.RenderDimension = newSize;

			TriggerViewChanged(view.View, oldSize!=newSize, newSize);

			// Pick smaller of the angles
			var angle = newSize.Width > newSize.Height ? (float)view.Vertical * 2.0f : (float)view.Horizontal * 2.0f;

			//System.Diagnostics.Debug.WriteLine("size: {0}, matrix: {1}, angle: {2}, Sensorsize: {3}x{4}", size, view.Transform, angle, Settings.SensorHeight, Settings.SensorWidth);

			scene.Camera.Size = newSize;
			scene.Camera.Matrix = view.Transform;
			scene.Camera.Type = view.Projection;
			scene.Camera.Fov = angle;
			scene.Camera.BladesRotation = (float)Rhino.RhinoMath.ToRadians(BladesRotation);
			scene.Camera.ApertureRatio = ApertureRatio;
			scene.Camera.Blades = Blades;

			//scene.Camera.NearClip = (float)view.Near;
			scene.Camera.FarClip = (float)view.Far; // 1.0E+14f; // gp_side_extension;
			if (view.Projection == CameraType.Orthographic || view.TwoPoint) scene.Camera.SetViewPlane(view.Viewplane.Left, view.Viewplane.Right, view.Viewplane.Top, view.Viewplane.Bottom);
			else if(view.Projection == CameraType.Perspective) scene.Camera.ComputeAutoViewPlane();

			scene.Camera.SensorHeight = RcCore.It.EngineSettings.SensorHeight;
			scene.Camera.SensorWidth = RcCore.It.EngineSettings.SensorWidth;
			scene.Camera.Update();
		}

		public Size RenderDimension { get; set; }

		/// <summary>
		/// Handle view changes.
		/// </summary>
		/// <param name="viewInfo"></param>
		protected override void ApplyViewChange(ViewInfo viewInfo)
		{
			var fb = _cameraDatabase.HandleBlur(viewInfo);
			SceneBoundingBox = GetQueueSceneBoundingBox();
			if (!_modalRenderer && !viewInfo.Viewport.Id.Equals(ViewId)) return;

			if (_wallpaperInitialized)
			{
				_environmentDatabase.SetGamma(PreProcessGamma);
				_environmentDatabase.BackgroundWallpaper(viewInfo, _previousScaleBackgroundToFit);
			}

			_currentViewInfo = viewInfo;

			var vp = viewInfo.Viewport;

			// camera transform, camera to world conversion
			var rhinocam = vp.GetXform(CoordinateSystem.Camera, CoordinateSystem.World);
			// lens length
			var lenslength = vp.Camera35mmLensLength;

			// lets see if we need to do magic for two-point perspective
			var twopoint = vp.IsTwoPointPerspectiveProjection || vp.IsPerspectiveProjection;

			// frustum values, used for two point
			vp.GetFrustum(out double frl, out double frr, out double frb, out double frt, out double frn, out double frf);

			//sdd.WriteLine(String.Format(
			//	"Frustum l {0} r {1} t {2} b {3} n {4} f{5}", frl, frr, frt, frb, frn, frf));

			// For 2 point perspective frustum needs to be scaled
			if (twopoint)
			{
				// Rhino frustum dimensions
				var rhino_frustum_height = frt - frb;
				var rhino_frustum_width = frr - frl;

				var frustum_scale_factor = 1.0;
				if (rhino_frustum_width >= rhino_frustum_height)
				{
					// Use Cycles frustum height of 2 so that Cycles frustum width is at least 2
					var cycles_frustum_height = 2.0;
					frustum_scale_factor = cycles_frustum_height / rhino_frustum_height;
				}
				else
				{
					// Use Cycles frustum width of 2 so that Cycles frustum height is at least 2
					var cycles_frustum_width = 2.0;
					frustum_scale_factor = cycles_frustum_width / rhino_frustum_width;
				}

				frb = frustum_scale_factor * frb;
				frt = frustum_scale_factor * frt;
				frl = frustum_scale_factor * frl;
				frr = frustum_scale_factor * frr;
				//System.Diagnostics.Debug.WriteLine(String.Format(
				//	"ADJUSTED Frustum l {0} r {1} t {2} b {3} n {4} f{5}", frl, frr, frt, frb, frn, frf));
			}

			var parallel = vp.IsParallelProjection;
			var viewscale = vp.ViewScale;

			/*sdd.WriteLine(String.Format(
				"Camera projection type {0}, lens length {1}, scale {2}x{3}, two-point {4}, dist {5}, disthalf {6}", parallel ? "ORTHOGRAPHIC" : "PERSPECTIVE",
				lenslength, viewscale.Width, viewscale.Height, twopoint, dist, disthalf));

			sdd.WriteLine(String.Format(
				"Frustum l {0} r {1} t {2} b {3} n {4} f{5}", frl, frr, frt, frb, frn, frf));
				*/

			var screenport = vp.GetScreenPort(out int near, out int far);
			var bottom = screenport.Bottom;
			var top = screenport.Top;
			var left = screenport.Left;
			var right = screenport.Right;

			int w = 0;
			int h = 0;

			// We shouldn't be taking render dimensions from the viewport when
			// rendering into render window, since this can be completely
			// different (for instance Rendering panel, custom render size)
			// see http://mcneel.myjetbrains.com/youtrack/issue/RH-32533
			if (!_modalRenderer)
			{
				w = Math.Abs(right - left);
				h = Math.Abs(bottom - top);
			}
			else
			{
				w = RenderDimension.Width;
				h = RenderDimension.Height;
			}
			var viewAspectratio = (float)w / (float)h;

			// get camera angles
			vp.GetCameraAngles(out double diagonal, out double vertical, out double horizontal);

			if (twopoint)
			{
				// Calculate vertical camera angle for 2 point perspective by horizontal camera angle and view aspect ratio.
				vertical = Math.Atan(Math.Tan(horizontal) / viewAspectratio);
			}

			// convert rhino transform to ccsycles transform
			var t = CclXformFromRhinoXform(rhinocam);
			// then convert to Cycles orientation
			t = t * ccl.Transform.RhinoToCyclesCam;

			// ready, lets push our data
			var cyclesview = new CyclesView
			{
				LensLength = lenslength,
				Transform = t,
				Diagonal =  diagonal,
				Vertical = vertical,
				Horizontal = horizontal,
				ViewAspectRatio = viewAspectratio,
				Projection = parallel ? CameraType.Orthographic : CameraType.Perspective,
				Viewplane = new ViewPlane((float)frl, (float)frr, (float)frt, (float)frb),
				TwoPoint = twopoint,
				Width = w,
				Height = h,
				Near = frn,
				Far = frf > 1.0E+10f ? frf : 1.0E+10f,
				View = GetQueueView() // use GetQueueView to ensure we have a valid ViewInfo even after Flush
			};
			_renderEngine.View = null;
			_cameraDatabase.AddViewChange(cyclesview);
		}

		/// <summary>
		/// Handle mesh changes
		/// </summary>
		/// <param name="deleted"></param>
		/// <param name="added"></param>
		protected override void ApplyMeshChanges(Guid[] deleted, List<CqMesh> added)
		{
			SceneBoundingBox = GetQueueSceneBoundingBox();
			RcCore.OutputDebugString($"ChangeDatabase ApplyMeshChanges, deleted {deleted.Length}\n");

			foreach (var guid in deleted)
			{
				// only delete those that aren't listed in the added list
				if (!(from mesh in added where mesh.Id() == guid select mesh).Any())
				{
					RcCore.OutputDebugString($" record mesh deletion {guid}\n");
					_objectDatabase.DeleteMesh(guid);
				}
			}

			RcCore.OutputDebugString($"ChangeDatabase ApplyMeshChanges added {added.Count}\n");

			foreach (var cqm in added)
			{
				var meshes = cqm.GetMeshes();
				var meshguid = cqm.Id();

				var attr = cqm.Attributes;
				bool isClippingObject = false;
				if(attr!=null && attr.HasUserData)
				{
					if(attr.UserData.Find(typeof(RhinoCyclesData)) is RhinoCyclesData ud)
					{
						isClippingObject = ud.IsClippingObject;
					}
				}

				var meshIndex = 0;

				foreach(var meshdata in meshes)
				{
					HandleMeshData(meshguid, meshIndex, meshdata, isClippingObject);
					meshIndex++;
				}
			}
		}

		public void HandleMeshData(Guid meshguid, int meshIndex, Rhino.Geometry.Mesh meshdata, bool isClippingObject)
		{
			RcCore.OutputDebugString($"\tHandleMeshData: {meshdata.Faces.Count}");
			// Get face indices flattened to an
			// integer array. The result will be triangulated faces.
			var findices = meshdata.Faces.ToIntArray(true);
			RcCore.OutputDebugString($" .. {findices.Length/3}\n");

			// Get texture coordinates and
			// flattens to a float array.
			var tc = meshdata.TextureCoordinates;
			var rhuv = tc.ToFloatArray();
			float[] rhvc = meshdata.VertexColors.ToFloatArray(meshdata.Vertices.Count);
			float[] cmvc = rhvc != null ? new float[findices.Length * 3] : null;
			if (cmvc != null)
			{
				for (var fi = 0; fi < findices.Length; fi++)
				{
					var fioffs = fi * 3;
					var findex = findices[fi];
					var findex2 = findex * 3;
					var rhvcit = rhvc[findex2];
					var rhvcit1 = rhvc[findex2 + 1];
					var rhvcit2 = rhvc[findex2 + 2];
					cmvc[fioffs] = rhvcit;
					cmvc[fioffs + 1] = rhvcit1;
					cmvc[fioffs + 2] = rhvcit2;
				}
			}

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

			var meshid = new Tuple<Guid, int>(meshguid, meshIndex);

			var crc = _objectShaderDatabase.FindRenderHashForMeshId(meshid);
			if (crc == uint.MaxValue) crc = 0;

			// now we have everything we need
			// so we can create a CyclesMesh that the
			// RenderEngine can eventually commit to Cycles
			var cyclesMesh = new CyclesMesh
			{
				MeshId = meshid,
				Verts = meshdata.Vertices.ToFloatArray(),
				Faces = findices,
				Uvs = cmuv,
				VertexNormals = rhvn,
				VertexColors = cmvc,
				MatId = crc,
			};
			_objectDatabase.AddMesh(cyclesMesh);
			_objectDatabase.SetIsClippingObject(meshid, isClippingObject);
		}

		/// <summary>
		/// Convert a Rhino.Geometry.Transform to ccl.Transform
		/// </summary>
		/// <param name="rt">Rhino.Geometry.Transform</param>
		/// <returns>ccl.Transform</returns>
		static ccl.Transform CclXformFromRhinoXform(Rhino.Geometry.Transform rt)
		{
			var t = new ccl.Transform(
				(float) rt.M00, (float) rt.M01, (float) rt.M02, (float) rt.M03,
				(float) rt.M10, (float) rt.M11, (float) rt.M12, (float) rt.M13,
				(float) rt.M20, (float) rt.M21, (float) rt.M22, (float) rt.M23,
				(float) rt.M30, (float) rt.M31, (float) rt.M32, (float) rt.M33
				);

			return t;
		}

		private bool sceneBoundingBoxDirty;
		private BoundingBox _sbb;

		public BoundingBox SceneBoundingBox
		{
			get
			{
				return _sbb;
			}
			set
			{
				if(!_sbb.Equals(value))
				{
					sceneBoundingBoxDirty = true;
					_sbb = value;
				}
			}
		}

		protected override void ApplyMeshInstanceChanges(List<uint> deleted, List<MeshInstance> addedOrChanged)
		{
			SceneBoundingBox = GetQueueSceneBoundingBox();
			// helper list to ensure we don't add same material multiple times.
			var addedmats = new List<uint>();

			RcCore.OutputDebugString($"ApplyMeshInstanceChanges: Received {deleted.Count} mesh instance deletes\n");
			foreach (var dm in deleted)
			{
				RcCore.OutputDebugString($"\ttold to DELETE {dm}\n");
			}
			foreach (var aoc in addedOrChanged)
			{
				RcCore.OutputDebugString($"\ttold to ADD {aoc.InstanceId}\n");
			}
			RcCore.OutputDebugString($"ApplyMeshInstanceChanges: Received {deleted.Count} mesh instance deletes\n");
			var inDeleted = from inst in addedOrChanged where deleted.Contains(inst.InstanceId) select inst;
			var skipFromDeleted = (from inst in inDeleted where true select inst.InstanceId).ToList();

			if (skipFromDeleted.Count > 0)
			{
				RcCore.OutputDebugString($"\t{skipFromDeleted.Count} in both deleted and addedOrChanged!\n");
				foreach (var skip in skipFromDeleted)
				{
					RcCore.OutputDebugString($"\t\t{skip} should not be deleted!\n");
				}
			}
			var realDeleted = (from dlt in deleted where !skipFromDeleted.Contains(dlt) select dlt).ToList();
			RcCore.OutputDebugString($"\tActually deleting {realDeleted.Count} mesh instances!\n");
			foreach (var d in realDeleted)
			{
					var cob = _objectDatabase.FindObjectRelation(d);
					if (cob != null)
					{
						var delob = new CyclesObject {cob = cob};
						_objectDatabase.DeleteObject(delob);
						RcCore.OutputDebugString($"\tDeleting mesh instance {d} {cob.Id}\n");
					}
					else
					{
						RcCore.OutputDebugString($"\tMesh instance {d} has no object relation..\n");
					}
			}
			var totalmeshes = addedOrChanged.Count;
			var curmesh = 0;
			RcCore.OutputDebugString($"ApplyMeshInstanceChanges: Received {totalmeshes} mesh instance changes\n");
			foreach (var a in addedOrChanged)
			{
				curmesh++;

				var matid = a.MaterialId;
				var mat = a.RenderMaterial;
				RcCore.OutputDebugString($"\tHandling mesh instance {curmesh}/{totalmeshes}. material {matid} ({mat.Name})\n");

				if (!addedmats.Contains(matid))
				{
					HandleRenderMaterial(mat);
					addedmats.Add(matid);
				}

				var meshid = new Tuple<Guid, int>(a.MeshId, a.MeshIndex);
				var cutout = _objectDatabase.MeshIsClippingObject(meshid);
				var ob = new CyclesObject {obid = a.InstanceId, meshid = meshid, Transform = CclXformFromRhinoXform(a.Transform), matid = a.MaterialId, CastShadow = a.CastShadows, Cutout = cutout};
				var oldhash = _objectShaderDatabase.FindRenderHashForObjectId(a.InstanceId);

				HandleShaderChange(a.InstanceId, oldhash, a.MaterialId, meshid);

				_objectDatabase.AddOrUpdateObject(ob);
			}
		}

		/// <summary>
		/// Record shader change if any change found
		/// </summary>
		/// <param name="obid">object id (meshinstance id)</param>
		/// <param name="oldhash">render hash (material id) from previously used material</param>
		/// <param name="newhash">render hash (material id) from new material</param>
		/// <param name="meshid"> mesh id the materials are for. (guid * mesh index)</param>
		public void HandleShaderChange(uint obid, uint oldhash, uint newhash, Tuple<Guid, int> meshid)
		{
			var shaderchange = new CyclesObjectShader(obid)
			{
				OldShaderHash = oldhash,
				NewShaderHash = newhash
			};

			if (shaderchange.Changed)
			{
				RcCore.OutputDebugString(
					$"\t\tsetting material, from old {shaderchange.OldShaderHash} to new {shaderchange.NewShaderHash}\n");

				_shaderDatabase.AddObjectMaterialChange(shaderchange);

				if (meshid != null)
				{
					_objectShaderDatabase.RecordRenderHashRelation(newhash, meshid, obid);
					_objectDatabase.RecordObjectIdMeshIdRelation(obid, meshid);
				}
			}
		}

		#region SHADERS

		/// <summary>
		/// Handle RenderMaterial - will queue new shader if necessary
		/// </summary>
		/// <param name="mat"></param>
		private void HandleRenderMaterial(RenderMaterial mat)
		{
			if (_shaderDatabase.HasShader(mat.RenderHash)) return;

			//System.Diagnostics.Debug.WriteLine("Add new material with RenderHash {0}", mat.RenderHash);
			var sh = _shaderConverter.CreateCyclesShader(mat.TopLevelParent as RenderMaterial, PreProcessGamma);
			_shaderDatabase.AddShader(sh);
		}

		/// <summary>
		/// Change the material on given object
		/// </summary>
		/// <param name="matid">RenderHash of material</param>
		/// <param name="obid">MeshInstanceId</param>
		/// <param name="meshid">mesh id (Guid * meshindex), can be null</param>
		private void HandleMaterialChangeOnObject(uint matid, uint obid, Tuple<Guid, int> meshid)
		{
			var oldhash = _objectShaderDatabase.FindRenderHashForObjectId(obid);
			RcCore.OutputDebugString($"handle material change on object {oldhash} {matid}\n");

			HandleShaderChange(obid, oldhash, matid, meshid);
		}


		/// <summary>
		/// Handle changes in materials to create (or re-use) shaders.
		/// </summary>
		/// <param name="mats">List of <c>CQMaterial</c></param>
		protected override void ApplyMaterialChanges(List<CqMaterial> mats)
		{
			// list of material hashes
			var distinctMats = new List<uint>();

			RcCore.OutputDebugString($"ApplyMaterialChanges: {mats.Count}\n");

			foreach (var mat in mats)
			{
				RcCore.OutputDebugString($"\t[material {mat.Id}, {mat.MeshInstanceId}, {mat.MeshIndex}]\n");
				var rm = MaterialFromId(mat.Id);

				if (!distinctMats.Contains(mat.Id))
				{
					distinctMats.Add(mat.Id);
				}

				var obid = mat.MeshInstanceId;

				// no mesh id here, but shouldn't be necessary either. Passing in null.
				HandleMaterialChangeOnObject(rm.RenderHash, obid, null);
			}

			// list over material hashes, check if they exist. Create if new
			foreach (var distinct in distinctMats)
			{
				var existing = _shaderDatabase.GetShaderFromHash(distinct);
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
			RcCore.OutputDebugString($"Uploading shader changes {_shaderDatabase.ShaderChanges.Count}\n");
			// map shaders. key is RenderHash
			foreach (var shader in _shaderDatabase.ShaderChanges)
			{
				if (_renderEngine.CancelRender) return;

				shader.Gamma = PreProcessGamma;

				// create a cycles shader
				var sh = _renderEngine.CreateMaterialShader(shader);
				_shaderDatabase.RecordRhCclShaderRelation(shader.Id, sh);
				_shaderDatabase.Add(shader, sh);
				// add the new shader to scene
				var scshid = _renderEngine.Client.Scene.AddShader(sh);
				_shaderDatabase.RecordCclShaderSceneId(shader.Id, scshid);

				sh.Tag();
			}
		}

#endregion SHADERS

#region GROUNDPLANE

		/// <summary>
		/// Guid of our groundplane object.
		/// </summary>
		private readonly Tuple<Guid, int> _groundplaneGuid = new Tuple<Guid, int>(new Guid("306690EC-6E86-4676-B55B-1A50066D7432"), 0);


		/// <summary>
		/// The mesh instance id for ground plane
		/// </summary>
		private const uint GroundPlaneMeshInstanceId = 1;

		private readonly float gp_side_extension = 1.0E+6f;
		private uint currentGpRenderMaterial = 0;
		private void InitialiseGroundPlane(CqGroundPlane gp)
		{
			var gpid = _groundplaneGuid;
			var altitude = (float)(gp.Enabled ? gp.Altitude : 0.0);
			altitude -= 2.5e-4f;
			Point3d pp = new Point3d(0.0, 0.0, altitude);
			Plane p = new Plane(pp, Vector3d.ZAxis);
			Plane pmap = new Plane(pp, Vector3d.ZAxis);
			var xext = new Interval(-gp_side_extension, gp_side_extension);
			var yext = new Interval(-gp_side_extension, gp_side_extension);
			var smext = new Interval(0.0, 1.0);
			var m = Rhino.Geometry.Mesh.CreateFromPlane(p, xext, yext, 100, 100);
			m.Weld(0.1);

			Rhino.Geometry.Transform tfm = Rhino.Geometry.Transform.Identity;
			var texscale = gp.TextureScale;
			var tscale = Rhino.Geometry.Transform.Scale(p, texscale.X, texscale.Y, 1.0);
			tfm *= tscale;
			var motion = new Rhino.Geometry.Vector3d(gp.TextureOffset.X, gp.TextureOffset.Y, 0.0);
			var ttrans = Rhino.Geometry.Transform.Translation(motion);
			tfm *= ttrans;
			var trot = Rhino.Geometry.Transform.Rotation(gp.TextureRotation, pp);
			tfm *= trot;
			var texturemapping = TextureMapping.CreatePlaneMapping(pmap, smext, smext, smext);
			if (texturemapping != null)
			{
				m.SetTextureCoordinates(texturemapping, tfm, false);
				m.SetCachedTextureCoordinates(texturemapping, ref tfm);
			}

			HandleMeshData(gpid.Item1, gpid.Item2, m, false);

			var isshadowonly = gp.IsShadowOnly;
			var def = Rhino.DocObjects.Material.DefaultMaterial.RenderMaterial;
			var mat = isshadowonly ? def : MaterialFromId(gp.MaterialId);

			HandleRenderMaterial(mat);

			var matrenderhash = mat.RenderHash;
			var t = ccl.Transform.Translate(0.0f, 0.0f, 0.0f);
			var cyclesObject = new CyclesObject
			{
				matid = matrenderhash,
				obid = GroundPlaneMeshInstanceId,
				meshid = gpid,
				Transform = t,
				Visible = gp.Enabled,
				CastShadow = true,
				IsShadowCatcher = isshadowonly,
				IgnoreCutout = true,
			};

			HandleShaderChange(GroundPlaneMeshInstanceId, currentGpRenderMaterial, matrenderhash, gpid);
			currentGpRenderMaterial = matrenderhash;

			_objectDatabase.AddOrUpdateObject(cyclesObject);
		}

		private uint old_gp_crc = 0;
		private bool old_gp_enabled = false;
		RenderMaterial gpShadowsOnlyMat = RenderContentType.NewContentFromTypeId(RenderMaterial.PlasterMaterialGuid) as RenderMaterial;
		/// <summary>
		/// Handle ground plane changes.
		/// </summary>
		/// <param name="gp"></param>
		protected override void ApplyGroundPlaneChanges(CqGroundPlane gp)
		{
			var gpcrc = gp.Crc;
			if (old_gp_crc == 0 && !gp.Enabled) return;
			if (gpcrc == old_gp_crc && old_gp_enabled == gp.Enabled) return;

			RcCore.OutputDebugString("ApplyGroundPlaneChanges.\n");

			old_gp_crc = gpcrc;
			old_gp_enabled = gp.Enabled;

			InitialiseGroundPlane(gp);
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
				_objectDatabase.AddDynamicObjectTransform(cot);
			}
		}

#region LIGHT & SUN

		/// <summary>
		/// Upload all light changes to the Cycles render engine
		/// </summary>
		public void UploadLightChanges()
		{

			/* new light shaders and lights. */
			foreach (var l in _lightDatabase.LightsToAdd)
			{
				if (_renderEngine.CancelRender) return;

				l.Gamma = PreProcessGamma;

				var lgsh = l.Type!=LightType.Background ? _renderEngine.CreateSimpleEmissionShader(l) : _renderEngine.Session.Scene.Background.Shader;
				if (l.Type != LightType.Background)
				{
					_renderEngine.Client.Scene.AddShader(lgsh);
					_shaderDatabase.Add(l, lgsh);
				}

				if (_renderEngine.CancelRender) return;

				var light = new CclLight(_renderEngine.Client, _renderEngine.Client.Scene, lgsh)
				{
					Type = l.Type,
					Size = l.Size,
					Location = l.Co,
					Direction = l.Dir,
					UseMis = l.UseMis,
					CastShadow = l.CastShadow,
					Samples = 1,
					MaxBounces = 8,
					SizeU = l.SizeU,
					SizeV = l.SizeV,
					AxisU = l.AxisU,
					AxisV = l.AxisV,
				};

				switch (l.Type)
				{
					case LightType.Spot:
						light.SpotAngle = l.SpotAngle;
						light.SpotSmooth = l.SpotSmooth;
						break;
					default:
						break;
				}

				light.TagUpdate();
				_lightDatabase.RecordLightRelation(l.Id, light);
			}

			// update existing ones
			foreach (var l in _lightDatabase.LightsToUpdate)
			{
				var existingL = _lightDatabase.ExistingLight(l.Id);
				if(l.Type == LightType.Background)
				{
					existingL.Shader = _renderEngine.Session.Scene.Background.Shader;
				} else
				{
					TriggerLightShaderChanged(l, existingL.Shader);
				}

				existingL.Type = l.Type;
				existingL.Size = l.Size;
				existingL.Location = l.Co;
				existingL.Direction = l.Dir;
				existingL.UseMis = l.UseMis;
				existingL.CastShadow = l.CastShadow;
				existingL.Samples = 1;
				existingL.MaxBounces = 8;
				existingL.SizeU = l.SizeU;
				existingL.SizeV = l.SizeV;
				existingL.AxisU = l.AxisU;
				existingL.AxisV = l.AxisV;

				switch (l.Type)
				{
					case LightType.Area:
						break;
					case LightType.Point:
						break;
					case LightType.Spot:
						existingL.SpotAngle = l.SpotAngle;
						existingL.SpotSmooth = l.SpotSmooth;
						break;
					case LightType.Distant:
						break;
				}
				existingL.TagUpdate();
			}
		}

		private uint LinearLightMaterialCRC(Rhino.Geometry.Light ll)
		{
			uint crc = 0xBABECAFE;

			crc = Rhino.RhinoMath.CRC32(crc, ll.Diffuse.R);
			crc = Rhino.RhinoMath.CRC32(crc, ll.Diffuse.G);
			crc = Rhino.RhinoMath.CRC32(crc, ll.Diffuse.B);
			crc = Rhino.RhinoMath.CRC32(crc, ll.Intensity);
			crc = Rhino.RhinoMath.CRC32(crc, ll.ShadowIntensity);
			crc = Rhino.RhinoMath.CRC32(crc, ll.IsEnabled ? 1 : 0);

			return crc;
		}

		private void HandleLightMaterial(Rhino.Geometry.Light rgl)
		{
			var matid = LinearLightMaterialCRC(rgl);
			if (_shaderDatabase.HasShader(matid)) return;

			var emissive = new Materials.EmissiveMaterial();
			Color4f color = new Color4f(rgl.Diffuse);
			emissive.BeginChange(RenderContent.ChangeContexts.Ignore);
			emissive.Gamma = PreProcessGamma;
			emissive.SetParameter("emission_color", color);
			emissive.SetParameter("strength", (float)rgl.Intensity * (rgl.IsEnabled ? 1 : 0));
			emissive.EndChange();
			emissive.BakeParameters();
			var shader = new CyclesShader(matid);
			shader.FrontXmlShader(rgl.Name, emissive);
			shader.Type = CyclesShader.Shader.Diffuse;

			_shaderDatabase.AddShader(shader);
		}

		public void UpdateBackgroundLight()
		{
			_lightDatabase.UpdateBackgroundLight();
		}

		private int _enabledLights = 0;
		public void PushEnabledLight()
		{
			_enabledLights++;
		}

		public void PopEnabledLight()
		{
			_enabledLights--;
			if (_enabledLights < 0) _enabledLights = 0;
		}

		public bool AnyActiveLights()
		{
			return _enabledLights > 0;
		}

		/// <summary>
		/// Handle light changes
		/// </summary>
		/// <param name="lightChanges"></param>
		protected override void ApplyLightChanges(List<CqLight> lightChanges)
		{
			SceneBoundingBox = GetQueueSceneBoundingBox();
			// we don't necessarily get view changes prior to light changes, so
			// the old _currentViewInfo could be null - at the end of a Flush
			// it would be thrown away. Hence we now ask the ChangeQueue for the
			// proper view info. It will be given if one constructed the ChangeQueue
			// with a view to force it to be a single-view only ChangeQueue.
			// See #RH-32345 and #RH-32356
			var v = GetQueueView();

			foreach (var light in lightChanges)
			{
				if (light.ChangeType == CqLight.Event.Deleted) PopEnabledLight();
				else if (light.ChangeType == CqLight.Event.Added || light.ChangeType == CqLight.Event.Undeleted) PushEnabledLight();

				if (light.Data.IsLinearLight)
				{
					uint lightmeshinstanceid = light.IdCrc;
					var ld = light.Data;
					switch (light.ChangeType)
					{
						case CqLight.Event.Deleted:
							var cob = _objectDatabase.FindObjectRelation(lightmeshinstanceid);
							var delob = new CyclesObject {cob = cob};
							_objectDatabase.DeleteObject(delob);
							_objectDatabase.DeleteMesh(ld.Id);
							break;
						default:
							HandleLinearLightAddOrModify(lightmeshinstanceid, ld);
							break;
					}
				}
				else
				{
					var cl = _shaderConverter.ConvertLight(this, light, v, PreProcessGamma);

					_lightDatabase.AddLight(cl);
				}
			}
			_environmentDatabase.TagUpdate();
		}

		private readonly MeshingParameters mp = new MeshingParameters(0.1) { MinimumEdgeLength = 0.001, GridMinCount = 16, JaggedSeams = false };
		private readonly MeshingParameters mpclipping = new MeshingParameters(0.1) { MinimumEdgeLength = 0.1, GridMinCount = 8, JaggedSeams = false };

		private void HandleLinearLightAddOrModify(uint lightmeshinstanceid, RGLight ld)
		{
			var brepf = ld.HasBrepForm;
			var p = new Plane(ld.Location, ld.Direction);
			var circle = new Circle(p, ld.Width.Length);
			var c = new Cylinder(circle, ld.Direction.Length);
			var m = Rhino.Geometry.Mesh.CreateFromBrep(c.ToBrep(true, true), mp);
			var mesh = new Rhino.Geometry.Mesh();
			foreach (var im in m) mesh.Append(im);
			mesh.RebuildNormals();
			var t = ccl.Transform.Identity();

			var ldid = new Tuple<Guid, int>(ld.Id, 0);

			var matid = LinearLightMaterialCRC(ld);

			HandleLightMaterial(ld);

			HandleMeshData(ld.Id, 0, mesh, false);

			var lightObject = new CyclesObject
			{
				matid = matid,
				obid = lightmeshinstanceid,
				meshid = ldid,
				Transform = t,
				Visible = ld.IsEnabled,
				CastShadow = false,
				IsShadowCatcher = false,
				CastNoShadow = ld.ShadowIntensity < 0.00001,
				IgnoreCutout = true,
			};

			_objectDatabase.AddOrUpdateObject(lightObject);
			HandleMaterialChangeOnObject(matid, lightmeshinstanceid, ldid);
		}

		protected override void ApplyDynamicLightChanges(List<RGLight> dynamicLightChanges)
		{
			SceneBoundingBox = GetQueueSceneBoundingBox();
			foreach (var light in dynamicLightChanges)
			{
				if (light.IsLinearLight)
				{
					uint lightmeshinstanceid = CrcFromGuid(light.Id);
					HandleLinearLightAddOrModify(lightmeshinstanceid, light);
				}
				else
				{
					var cl = _shaderConverter.ConvertLight(light, PreProcessGamma);
					//System.Diagnostics.Debug.WriteLine("dynlight {0} @ {1}", light.Id, light.Location);
					_lightDatabase.AddLight(cl);
				}
			}
		}

		/// <summary>
		/// Sun ID
		/// </summary>
		private readonly Guid _sunGuid = new Guid("82FE2C29-9632-473D-982B-9121E150E1D2");

		/// <summary>
		/// Handle sun changes
		/// </summary>
		/// <param name="sun"></param>
		protected override void ApplySunChanges(RGLight sun)
		{
			var cl = _shaderConverter.ConvertLight(sun, PreProcessGamma);
			cl.Id = _sunGuid;
			_lightDatabase.AddLight(cl);
			if (sun.IsEnabled) PushEnabledLight();
			else PopEnabledLight();
			_environmentDatabase.TagUpdate();
			//System.Diagnostics.Debug.WriteLine("Sun {0} {1} {2}", sun.Id, sun.Intensity, sun.Diffuse);
		}

#endregion

		public void UploadEnvironmentChanges()
		{
			if (_environmentDatabase.BackgroundHasChanged)
			{
				_environmentDatabase.CyclesShader.EnabledLights = AnyActiveLights(); 
				RcCore.OutputDebugString($"Uploading background changes, active lights: {_environmentDatabase.CyclesShader.EnabledLights}, sky {_environmentDatabase.CyclesShader.SkylightEnabled} skystrength {_environmentDatabase.CyclesShader.SkyStrength}\n");
				_renderEngine.RecreateBackgroundShader(_environmentDatabase.CyclesShader);
			}
		}

		/// <summary>
		/// Upload object changes
		/// </summary>
		public void UploadObjectChanges()
		{
			// first delete objects
			foreach (var ob in _objectDatabase.DeletedObjects)
			{
				if (ob.cob != null)
				{
					RcCore.OutputDebugString($"UploadObjectChanges: deleting object {ob.obid} {ob.cob.Id}\n");
					var cob = ob.cob;
					// deleting we do (for now?) by marking object as hidden.
					// we *don't* clear mesh data here, since that very mesh
					// may be used elsewhere.
					cob.Visibility = PathRay.Hidden;
					cob.TagUpdate();
				}
			}

			RcCore.OutputDebugString($"UploadObjectChanges: adding/modifying objects {_objectDatabase.NewOrUpdatedObjects.Count}\n");

			// now combine objects and meshes, creating new objects when necessary
			foreach (var ob in _objectDatabase.NewOrUpdatedObjects)
			{
				// mesh for this object id
				var mesh = _objectDatabase.FindMeshRelation(ob.meshid);

				// hmm, no mesh. Oh well, lets get on with the next
				if (mesh == null) continue;

				// see if we already have an object here.
				// update it, otherwise create new one
				var cob = _objectDatabase.FindObjectRelation(ob.obid);

				var newcob = cob == null;

				// new object, so lets create it and record necessary stuff about it
				if (newcob)
				{
					cob = new CclObject(_renderEngine.Client);
					_objectDatabase.RecordObjectRelation(ob.obid, cob);
					_objectDatabase.RecordObjectIdMeshIdRelation(ob.obid, ob.meshid);
				}

				RcCore.OutputDebugString($"\tadding/modifying object {ob.obid} {ob.meshid} {cob.Id}\n");

				// set mesh reference and other stuff
				cob.Mesh = mesh;
				cob.Transform = ob.Transform;
				cob.IsShadowCatcher = ob.IsShadowCatcher;
				var norefl = PathRay.AllVisibility & ~PathRay.Reflect;
				var vis = ob.Visible ? (ob.IsShadowCatcher ? norefl: PathRay.AllVisibility): PathRay.Hidden;
				if (ob.CastShadow == false)
				{
					vis &= ~PathRay.Shadow;
				}
				cob.MeshLightNoCastShadow = ob.CastNoShadow;
				cob.Cutout = ob.Cutout;
				cob.IgnoreCutout = ob.IgnoreCutout;
				cob.Visibility = vis;
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
			_environmentDatabase.SetSkylightEnabled(skylight.Enabled);
			_environmentDatabase.SetGamma(PreProcessGamma);
			_lightDatabase.UpdateBackgroundLight();
		}


		private bool _previousScaleBackgroundToFit = false;
		private bool _wallpaperInitialized = false;
		protected override void ApplyRenderSettingsChanges(RenderSettings rs)
		{
			if (rs != null)
			{
				var trbg = TransparentBackground;
				TransparentBackground = rs.TransparentBackground;
				DisplayPipelineAttributesChanged |= trbg != TransparentBackground;
				_environmentDatabase.SetGamma(PreProcessGamma);
				_environmentDatabase.SetBackgroundData(rs.BackgroundStyle, rs.BackgroundColorTop, rs.BackgroundColorBottom);
				if (rs.BackgroundStyle == BackgroundStyle.WallpaperImage)
				{
					var view = GetQueueView();
					var y = string.IsNullOrEmpty(view.WallpaperFilename);
					RcCore.OutputDebugString(
						$"view has {(y ? "no" : "")} wallpaper {(y ? "" : "with filename ")} {(y ? "" : view.WallpaperFilename)} {(y ? "" : "its grayscale bool")} {(y ? "" : $"{view.ShowWallpaperInGrayScale}")} {(y ? "" : "its hidden bool")} {(y ? "" : $"{view.WallpaperHidden}")}\n");
					_environmentDatabase.BackgroundWallpaper(view, rs.ScaleBackgroundToFit);
					_wallpaperInitialized = true;
				}
				_previousScaleBackgroundToFit = rs.ScaleBackgroundToFit;
				_lightDatabase.UpdateBackgroundLight();
			}
		}

		public bool DisplayPipelineAttributesChanged { get; private set; } = false;
		public int RealtimePreviewPasses { get; private set; } = -1;
		public bool TransparentBackground { get; private set; } = false;
		protected override void ApplyDisplayPipelineAttributesChanges(DisplayPipelineAttributes displayPipelineAttributes)
		{
			if (displayPipelineAttributes.ShowRealtimeRenderProgressBar)
			{
				DisplayPipelineAttributesChanged = true;
				RealtimePreviewPasses = displayPipelineAttributes.RealtimeRenderPasses;
				Rhino.RhinoApp.OutputDebugString($"{displayPipelineAttributes.ShowRealtimeRenderProgressBar} {displayPipelineAttributes.RealtimeRenderPasses}\n");
			}
			bool trbg = TransparentBackground;
			TransparentBackground = displayPipelineAttributes.FillMode == DisplayPipelineAttributes.FrameBufferFillMode.Transparent;
			DisplayPipelineAttributesChanged |= trbg != TransparentBackground;
		}

		public bool UploadDisplayPipelineAttributesChanges()
		{
			if(DisplayPipelineAttributesChanged)
			{
				if (RealtimePreviewPasses>-1 && _renderEngine is RenderEngines.ModalRenderEngine mre)
				{
					mre.requestedSamples = RealtimePreviewPasses;
				}
				_renderEngine.Session.Scene.Background.Transparent = TransparentBackground;
			}
			return true;
		}

		/// <summary>
		/// Handle environment changes
		/// </summary>
		/// <param name="usage"></param>
		protected override void ApplyEnvironmentChanges(RenderEnvironment.Usage usage)
		{
			/* instead of just reading the one environment we have to read everything.
			 * 
			 * The earlier assumption that non-changing EnvironmentIdForUsage meant non-changing
			 * environment instance is wrong. See http://mcneel.myjetbrains.com/youtrack/issue/RH-32418
			 */
			RcCore.OutputDebugString($"ApplyEnvironmentChanges {usage}\n");
			_environmentDatabase.SetGamma(PreProcessGamma);
			UpdateAllEnvironments(usage);
		}

		private void UpdateAllEnvironments(RenderEnvironment.Usage usage)
		{
			switch (usage)
			{
				case RenderEnvironment.Usage.Background:
					var bgenvId = EnvironmentIdForUsage(RenderEnvironment.Usage.Background);
					var bgenv = EnvironmentForid(bgenvId);
					_environmentDatabase.SetBackground(bgenv, RenderEnvironment.Usage.Background);
					break;
				case RenderEnvironment.Usage.Skylighting:
					var skyenvId = EnvironmentIdForUsage(RenderEnvironment.Usage.Skylighting);
					var skyenv = EnvironmentForid(skyenvId);
					_environmentDatabase.SetBackground(skyenv, RenderEnvironment.Usage.Skylighting);
					break;
				case RenderEnvironment.Usage.ReflectionAndRefraction:
					var reflenvId = EnvironmentIdForUsage(RenderEnvironment.Usage.ReflectionAndRefraction);
					var reflenv = EnvironmentForid(reflenvId);

					_environmentDatabase.SetBackground(reflenv, RenderEnvironment.Usage.ReflectionAndRefraction);
					break;
			}

			_environmentDatabase.HandleEnvironments(usage);
			_lightDatabase.UpdateBackgroundLight();
		}

		private static int _updateCounter = 0;

		/// <summary>
		/// We get notified of (dynamic?) changes.
		/// </summary>
		protected override void NotifyBeginUpdates()
		{
			// nothing
			RcCore.OutputDebugString($"NotifyBeginUpdates {++_updateCounter}\n");
			_renderEngine.TriggerBeginChangesNotified();
		}

		/// <summary>
		/// Changes have been signalled.
		/// </summary>
		protected override void NotifyEndUpdates()
		{
			RcCore.OutputDebugString($"NotifyEndUpdates {_updateCounter}\n");
			_renderEngine.Flush = true;
		}

		protected override void NotifyDynamicUpdatesAreAvailable()
		{
			RcCore.OutputDebugString("NotifyDynamicUpdatesAreAvailable\n");
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

		protected override bool ProvideOriginalObject()
		{
			return true;
		}
	}
}
