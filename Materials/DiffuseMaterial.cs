using System.Drawing;
using System.Runtime.InteropServices;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Render;

namespace RhinoCycles.Materials
{
	[Guid("F4C85EC1-C1CB-4633-A712-1BA2F020B954")]
	public class DiffuseMaterial : RenderMaterial, ICyclesMaterial
	{
		public override string TypeName
		{
			get { return "Cycles Diffuse"; }
		}

		public override string TypeDescription
		{
			get { return "Cycles Diffuse color only material (plaster)"; }
		}

		public float Gamma { get; set; }

		public CyclesShader.CyclesMaterial MaterialType { get { return CyclesShader.CyclesMaterial.Diffuse; } }

		public DiffuseMaterial()
		{
			Fields.Add("diffuse", Color4f.White, "Color");
		}

		public override void SimulateMaterial(ref Material simulatedMaterial, bool forDataOnly)
		{
			base.SimulateMaterial(ref simulatedMaterial, forDataOnly);


			Color4f color;
			if (Fields.TryGetValue("diffuse", out color))
				simulatedMaterial.DiffuseColor = color.AsSystemColor();

			simulatedMaterial.Name = Name;
		}

		public override Material SimulateMaterial(bool isForDataOnly)
		{
			var m = base.SimulateMaterial(isForDataOnly);

			SimulateMaterial(ref m, isForDataOnly);

			return m;
		}

		public string MaterialXml
		{
			get
			{
				Color4f color;

				Fields.TryGetValue("diffuse", out color);

				color = Color4f.ApplyGamma(color, Gamma);

				return string.Format(
					"<diffuse_bsdf color=\"{0} {1} {2}\" name=\"diff\"/>" +
					"<connect from=\"diff bsdf\" to=\"output surface\" />" +
					"",
					color.R, color.G, color.B);
			}
		}
	}
}
