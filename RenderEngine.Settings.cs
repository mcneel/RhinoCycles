using System.Drawing;
using ccl;

namespace RhinoCycles
{
	partial class RenderEngine
	{
		public Size RenderDimension { get; set; }

		private EngineSettings m_settings;

		public EngineSettings Settings
		{
			get {
				if (m_settings == null)
				{
					m_settings = new EngineSettings();
				}
				return m_settings;
			}
			set
			{
				m_settings = new EngineSettings(value);
			}
		}
	}
}
