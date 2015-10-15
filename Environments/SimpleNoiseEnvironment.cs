using System.Runtime.InteropServices;
using Rhino.Display;
using Rhino.Render;

namespace RhinoCycles.Materials
{
	[Guid("BFA4D96F-A1B5-40BB-9851-7A88D60E6F26")]
	public class SimpleNoiseEnvironment: RenderEnvironment, ICyclesMaterial
	{
		public override string TypeName { get { return "Simple Noise Environment (DEV)"; } }
		public override string TypeDescription { get { return "Simple Noise Environment (DEV)"; } }

		public float Gamma { get; set; }

		public SimpleNoiseEnvironment()
		{
			Fields.Add("scale", 5.0f, "Scale");
			Fields.Add("detail", 2.0f, "Detail");
			Fields.Add("distortion", 0.0f, "Distortion");
			Fields.Add("strength", 1.0f, "Background Strength");
		}

		protected override void OnAddUserInterfaceSections()
		{
			AddAutomaticUserInterfaceSection("Parameters", 0);
		}

		public string MaterialXml
		{
			get {
				float scale;
				float detail;
				float distortion;
				float strength;

				Fields.TryGetValue("scale", out scale);
				Fields.TryGetValue("detail", out detail);
				Fields.TryGetValue("distortion", out distortion);
				Fields.TryGetValue("strength", out strength);

				var nodegraph = string.Format(ccl.Utilities.Instance.NumberFormatInfo,
					"<noise_texture name=\"nt\" scale=\"{0}\" detail=\"{1}\" distortion=\"{2}\" />" +
					"<background name=\"bg\" strength=\"{3}\" />" +
					"<connect from=\"nt color\" to=\"bg color\" />" +
					"<connect from=\"bg background\" to=\"output surface\" />",
					scale, detail, distortion, strength);

				return nodegraph;
			}
		}

		public CyclesShader.CyclesMaterial MaterialType
		{
			get { return CyclesShader.CyclesMaterial.SimpleNoiseEnvironment; }
		}
	}
}