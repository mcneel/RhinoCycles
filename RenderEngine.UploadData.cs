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

namespace RhinoCycles
{
	partial class RenderEngine
	{
		/// <summary>
		/// Main entry point for uploading data to Cycles.
		/// </summary>
		protected bool UploadData()
		{
			// adding the locking guard makes viewport updates
			// on changes much blockier. For now disabling, even
			// though http://mcneel.myjetbrains.com/youtrack/issue/RH-31968
			// may happen. @todo figure out a better way to solve
			//if (!Session.Scene.TryLock()) return false;

			if (CancelRender) return false;

			// linear workflow changes
			Database.UploadLinearWorkflowChanges();

			if (CancelRender) return false;

			// gamma changes
			Database.UploadGammaChanges();

			if (CancelRender) return false;

			// environment changes
			Database.UploadEnvironmentChanges();

			if (CancelRender) return false;

			// transforms on objects, no geometry changes
			Database.UploadDynamicObjectTransforms();

			if (CancelRender) return false;

			// viewport changes
			Database.UploadCameraChanges();

			if (CancelRender) return false;

			// new shaders we've got
			Database.UploadShaderChanges();

			if (CancelRender) return false;

			// light changes
			Database.UploadLightChanges();

			if (CancelRender) return false;

			// mesh changes (new ones, updated ones)
			Database.UploadMeshChanges();

			if (CancelRender) return false;

			// shader changes on objects (replacement)
			Database.UploadObjectShaderChanges();

			if (CancelRender) return false;

			// object changes (new ones, deleted ones)
			Database.UploadObjectChanges();

			if (CancelRender) return false;

			// done, now clear out our change queue stuff so we're ready for the next time around :)
			Database.ResetChangeQueue();

			if (CancelRender) return false;

			//Session.Scene.Unlock();

			return true;
		}
	}
}
