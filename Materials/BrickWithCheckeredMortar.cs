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

using System.Drawing;
using Rhino.Render;

namespace RhinoCyclesCore.Materials
{
	[System.Runtime.InteropServices.Guid("67C5F9EC-7929-4FF8-9BF6-2FB8DF49AF78")]
	[CustomRenderContent(IsPrivate=true)]
	public class BrickWithCheckeredMortarMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName => "Cycles Brick with Checkered Mortar Material (DEV)";
		public override string TypeDescription => "Cycles Brick Checkered Mortar Material (DEV)";

		public float Gamma { get; set; }

		public override void SimulateMaterial(ref Rhino.DocObjects.Material simulatedMaterial, bool forDataOnly)
		{
			base.SimulateMaterial(ref simulatedMaterial, forDataOnly);

			simulatedMaterial.DiffuseColor = Color.DeepSkyBlue;
		}

		public override Rhino.DocObjects.Material SimulateMaterial(bool isForDataOnly)
		{
			var m = base.SimulateMaterial(isForDataOnly);

			SimulateMaterial(ref m, isForDataOnly);

			return m;
		}


		public CyclesShader.CyclesMaterial MaterialType => CyclesShader.CyclesMaterial.BrickCheckeredMortar;


		public string MaterialXml
		{
			get
			{

				var nodegraph = string.Format(
					ccl.Utilities.Instance.NumberFormatInfo,
					"<texture_coordinate name=\"texcoord\" />"+
					"<brick_texture name=\"brick\" " +
					"color1=\"0.8 0.8 0.8\" " +
					"color2=\"0.2 0.2 0.2\" " +
					"mortar=\"0.0 0.0 0.0\" " +
					"scale=\"1.0\" " +
					"bias=\"0.0\" " +
					"offset=\"0.5\" " +
					"offset_frequency=\"2\" "+
					"squash=\"1.0\" "+
					"squash_frequency=\"2\" "+
					"brick_width=\"0.5\" " +
					"row_height=\"0.25\" " +
					"/>" +
					"<mapping name=\"mapping\" mapping_type=\"point\" rotation=\"0.0 0.0 1.570796\"  scale=\"1.0 1.0 1.0\" />" +
					"<checker_texture name=\"checker\" scale=\"5.0\" color1=\"0.0 0.4 0.8\" color2=\"0.2 0.0 0.7\" />" +
					"<diffuse_bsdf name=\"diff\" roughness=\"0.0\"/>" +
					"<connect from=\"texcoord uv\" to=\"mapping vector\" />" +
					"<connect from=\"mapping vector\" to=\"brick vector\" />" +
					"<connect from=\"texcoord normal\" to=\"checker vector\" />" +
					"<connect from=\"checker color\" to=\"brick mortar\" />" +
					"<connect from=\"brick color\" to=\"diff color\" />" +
					"<connect from=\"diff bsdf\" to=\"output surface\" />"
					);
				return nodegraph;
			}
		}
	}
}
