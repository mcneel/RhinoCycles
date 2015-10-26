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
using ccl;
using ccl.ShaderNodes;
using RhinoCycles.Shaders;

namespace RhinoCycles
{
	partial class RenderEngine
	{
		internal Shader CreateMaterialShader(CyclesShader shader)
		{
			Shader sh;
			switch (shader.CyclesMaterialType)
			{
				case CyclesShader.CyclesMaterial.No:
					sh = CreateCyclesShaderFromRhinoV6BasicMat(shader);
					break;
				default:
					sh = CreateCyclesShaderFromXml(shader);
					break;
			}

			return sh;
		}

		internal Shader RecreateMaterialShader(CyclesShader shader, Shader existing)
		{
			Shader sh;
			switch (shader.CyclesMaterialType)
			{
				case CyclesShader.CyclesMaterial.No:
					sh = RecreateCyclesShaderFromRhinoV6BasicMat(shader, existing);
					break;
				default:
					sh = RecreateCyclesShaderFromXml(shader, existing);
					break;
			}

			return sh;
		}

		internal Shader CreateCyclesShaderFromXml(CyclesShader shader)
		{
			var sh = new Shader(Client, Shader.ShaderType.Material)
			{
				UseMis = true,
				UseTransparentShadow = true,
				HeterogeneousVolume = false,
				Name = shader.Name ?? String.Format("V6 Basic Material {0}", shader.Id)
			};

			Shader.ShaderFromXml(ref sh, shader.Crm.MaterialXml);

			return sh;
		}

		internal Shader RecreateCyclesShaderFromXml(CyclesShader shader, Shader existing)
		{
			existing.Recreate();
			Shader.ShaderFromXml(ref existing, shader.Crm.MaterialXml);

			return existing;
		}

		internal static void SetTextureImage(ImageTextureNode imnode, CyclesTextureImage texture)
		{
			if (texture.HasTextureImage)
			{
				if (texture.HasByteImage)
				{
					imnode.ByteImage = texture.TexByte;
				}
				else if (texture.HasFloatImage)
				{
					imnode.FloatImage = texture.TexFloat;
				}
				imnode.Filename = texture.Name;
				imnode.Width = (uint) texture.TexWidth;
				imnode.Height = (uint) texture.TexHeight;
			}
		}

		internal static void SetTextureImage(EnvironmentTextureNode envnode, CyclesTextureImage texture)
		{
			if (texture.HasTextureImage)
			{
				if (texture.HasByteImage)
				{
					envnode.ByteImage = texture.TexByte;
				}
				else if (texture.HasFloatImage)
				{
					envnode.FloatImage = texture.TexFloat;
				}
				envnode.Filename = texture.Name;
				envnode.Width = (uint) texture.TexWidth;
				envnode.Height = (uint) texture.TexHeight;
			}
		}

		internal Shader CreateCyclesShaderFromRhinoV6BasicMat(CyclesShader shader)
		{
			var v6 = RhinoShader.CreateRhinoMaterialShader(Client, shader);

			return v6.GetShader();
		}

		internal Shader RecreateCyclesShaderFromRhinoV6BasicMat(CyclesShader shader, Shader existing)
		{
			var v6 = RhinoShader.RecreateRhinoMaterialShader(Client, shader, existing);

			return v6.GetShader();
		}

		internal Shader CreateBackgroundShader(CyclesShader shader)
		{
			var rhinobg = RhinoShader.CreateRhinoBackgroundShader(Client, m_cq_background, null);
			return rhinobg.GetShader();
		}

		private RhinoShader m_current_background_shader;
		internal void RecreateBackgroundShader()
		{
			var bg = Session.Scene.Background.Shader;
			var rhinobg = RhinoShader.CreateRhinoBackgroundShader(Client, m_cq_background, bg);
			rhinobg.Reset();
			Session.Scene.Background.Shader = rhinobg.GetShader();
			m_current_background_shader = rhinobg;
		}

		internal Shader CreateSimpleEmissionShader(CyclesLight light)
		{
			var rhinolight = RhinoShader.CreateRhinoLightShader(Client, light, null);

			return rhinolight.GetShader();
		}

		internal Shader ReCreateSimpleEmissionShader(Shader emission_shader, CyclesLight light)
		{
			var rhinolight = RhinoShader.CreateRhinoLightShader(Client, light, emission_shader);

			return rhinolight.GetShader();
		}
	}
}
