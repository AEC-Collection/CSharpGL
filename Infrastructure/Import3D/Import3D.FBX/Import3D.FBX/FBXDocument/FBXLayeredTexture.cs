using System.Xml.Linq;
using System;

namespace Import3D.FBX {
	/** DOM class for layered FBX textures */
	public unsafe class FBXLayeredTexture : FBXObject {
		public FBXLayeredTexture(UInt64 id, FBXElement element, FBXDocument doc, string name)
			: base(id, element, name) {
			this.blendMode = BlendMode.BlendMode_Modulate;
			this.alpha = 1.0f;
			FBXScope sc = GetRequiredScope(element);

			FBXElement* BlendModes = sc["BlendModes"];
			FBXElement* Alphas = sc["Alphas"];

			if (null != BlendModes) {
				blendMode = (BlendMode)ParseTokenAsInt(GetRequiredToken(*BlendModes, 0));
			}
			if (null != Alphas) {
				alpha = ParseTokenAsFloat(GetRequiredToken(*Alphas, 0));
			}

		}

		// Can only be called after construction of the layered texture object due to construction flag.
		public void fillTexture(FBXDocument doc) {
			List<FBXConnection> & conns = doc.GetConnectionsByDestinationSequenced(ID());
			for (int i = 0; i < conns.Count; ++i) {
				FBXConnection con = conns.at(i);

				FBXObject* ob = con.SourceObject();
				if (null == ob) {
					DOMWarning("failed to read source object for texture link, ignoring", &element);
					continue;
				}

				FBXTexture* tex = dynamic_cast<FBXTexture*>(ob);

				textures.push_back(tex);
			}

		}

		public enum BlendMode {
			BlendMode_Translucent,
			BlendMode_Additive,
			BlendMode_Modulate,
			BlendMode_Modulate2,
			BlendMode_Over,
			BlendMode_Normal,
			BlendMode_Dissolve,
			BlendMode_Darken,
			BlendMode_ColorBurn,
			BlendMode_LinearBurn,
			BlendMode_DarkerColor,
			BlendMode_Lighten,
			BlendMode_Screen,
			BlendMode_ColorDodge,
			BlendMode_LinearDodge,
			BlendMode_LighterColor,
			BlendMode_SoftLight,
			BlendMode_HardLight,
			BlendMode_VividLight,
			BlendMode_LinearLight,
			BlendMode_PinLight,
			BlendMode_HardMix,
			BlendMode_Difference,
			BlendMode_Exclusion,
			BlendMode_Subtract,
			BlendMode_Divide,
			BlendMode_Hue,
			BlendMode_Saturation,
			BlendMode_Color,
			BlendMode_Luminosity,
			BlendMode_Overlay,
			BlendMode_BlendModeCount
		};

		FBXTexture getTexture(int index = 0) {
			return textures[index];
		}
		int textureCount() {
			return (int)(textures.Count);
		}
		BlendMode GetBlendMode() {
			return blendMode;
		}
		float Alpha() {
			return alpha;
		}


		List<FBXTexture> textures;
		BlendMode blendMode;
		float alpha;
	}
}
