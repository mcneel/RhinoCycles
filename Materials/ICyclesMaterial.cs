namespace RhinoCycles.Materials
{
	public interface ICyclesMaterial
	{
		CyclesShader.CyclesMaterial MaterialType { get; }

		string MaterialXml { get; }

		float Gamma { get; set; }
	}
}
