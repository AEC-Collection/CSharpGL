using System.Diagnostics;
using System.Xml.Linq;
using System;

namespace Import3D.FBX {
	/** DOM class for deformers */
	public unsafe class FBXDeformer : FBXObject {
		public FBXDeformer(UInt64 id, FBXElement element, FBXDocument doc, string name)
			: base(id, element, name) {
			FBXScope sc = GetRequiredScope(element);

			string &classname = ParseTokenAsString(GetRequiredToken(element, 2));
			props = GetPropertyTable(doc, "Deformer.Fbx" + classname, element, sc, true);

		}

		FBXPropertyTable Props() {
			Debug.Assert(props.get());
			return *props;
		}


		FBXPropertyTable props;
	}
}
