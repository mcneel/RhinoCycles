using ccl;

namespace RhinoCycles
{
	public class CyclesView
	{
		public Transform Transform { get; set; }
		public double LensLength { get; set; }
		public double Diagonal { get; set; }
		public double Vertical { get; set; }
		public double Horizontal { get; set; }
		public double ViewAspectRatio { get; set; }
		public CameraType Projection { get; set; }

		public int Width { get; set; }
		public int Height { get; set; }

		public ViewPlane Viewplane { get; set; }
		public bool TwoPoint { get; set; }
	}
}
