using System.Xml.Linq;
using System;
using System.Linq;

namespace Import3D.FBX {
	/** DOM class for BlendShapeChannel deformers */
	public unsafe class FBXBlendShapeChannel : FBXDeformer {
		public FBXBlendShapeChannel(UInt64 id, FBXElement element, FBXDocument doc, string name)
			: base(id, element, doc, name) {
			FBXScope sc = GetRequiredScope(element);
			FBXElement* DeformPercent = sc["DeformPercent"];
			if (DeformPercent) {
				percent = ParseTokenAsFloat(GetRequiredToken(*DeformPercent, 0));
			}
			FBXElement* FullWeights = sc["FullWeights"];
			if (FullWeights) {
				ParseVectorDataArray(fullWeights, *FullWeights);
			}
			List<FBXConnection*> & conns = doc.GetConnectionsByDestinationSequenced(ID(), "Geometry");
			shapeGeometries.reserve(conns.Count);
			for (FBXConnection* con : conns) {
				ShapeGeometry* sg = ProcessSimpleConnection<ShapeGeometry>(*con, false, "Shape . BlendShapeChannel", element);
				if (sg) {
					auto pr = shapeGeometries.insert(sg);
					if (!pr.second) {
						FBXImporter::LogWarn("there is the same shapeGeometrie id ", sg.ID());
					}
				}
			}
		}

		float DeformPercent() {
			return percent;
		}
		List<float> GetFullWeights() {
			return fullWeights;
		}

		HashSet<ShapeGeometry> GetShapeGeometries() {
			return shapeGeometries;
		}


		float percent;
		List<float> fullWeights;
		HashSet<ShapeGeometry> shapeGeometries;
	}
}
