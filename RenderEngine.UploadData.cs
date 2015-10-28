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
		protected void UploadData()
		{
			// linear workflow changes
			Database.UploadLinearWorkflowChanges();

			// gamma changes
			Database.UploadGammaChanges();

			// environment changes
			Database.UploadEnvironmentChanges();

			// transforms on objects, no geometry changes
			Database.UploadDynamicObjectTransforms();

			// viewport changes
			Database.UploadCameraChanges();

			// new shaders we've got
			Database.UploadShaderChanges();

			// light changes
			Database.UploadLightChanges();

			// mesh changes (new ones, updated ones)
			Database.UploadMeshChanges();

			// shader changes on objects (replacement)
			Database.UploadObjectShaderChanges();

			// object changes (new ones, deleted ones)
			Database.UploadObjectChanges();

			// done, now clear out our change queue stuff so we're ready for the next time around :)
			Database.ResetChangeQueue();
		}
	}
}
