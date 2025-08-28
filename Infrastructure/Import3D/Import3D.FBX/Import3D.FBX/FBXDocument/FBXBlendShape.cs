using System.Xml.Linq;
using System;
using System.Linq;

namespace Import3D.FBX {
	/** DOM class for BlendShape deformers */
	public unsafe class FBXBlendShape : FBXDeformer {
		public FBXBlendShape(UInt64 id, FBXElement element, FBXDocument doc, string name)
			: base(id, element, doc, name) {
			List<FBXConnection*> & conns = doc.GetConnectionsByDestinationSequenced(ID(), "Deformer");
			blendShapeChannels.reserve(conns.Count);
			for (FBXConnection* con : conns) {
				FBXBlendShapeChannel* bspc = ProcessSimpleConnection<FBXBlendShapeChannel>(*con, false, "BlendShapeChannel . BlendShape", element);
				if (bspc) {
					auto pr = blendShapeChannels.insert(bspc);
					if (!pr.second) {
						FBXImporter::LogWarn("there is the same blendShapeChannel id ", bspc.ID());
					}
				}
			}
		}


		HashSet<FBXBlendShapeChannel> BlendShapeChannels() {
			return blendShapeChannels;
		}


		HashSet<FBXBlendShapeChannel> blendShapeChannels;
	}
}
