using System.Diagnostics;
using System.Xml.Linq;
using System;

namespace Import3D.FBX {
	/** Represents a FBX animation layer (i.e. a list of node animations) */
	public unsafe class FBXAnimationLayer : FBXObject {

		public FBXAnimationLayer(UInt64 id, FBXElement element, string name, FBXDocument doc) {
			FBXScope sc = GetRequiredScope(element);

			// note: the props table here bears little importance and is usually absent
			props = GetPropertyTable(doc, "AnimationLayer.FbxAnimLayer", element, sc, true);

		}

		FBXPropertyTable Props() {
			Debug.Assert(props.get());
			return *props;
		}

		/* the optional white list specifies a list of property names for which the caller
        wants animations for. Curves not matching this list will not be added to the
        animation layer. */
		List<FBXAnimationCurveNode> Nodes(char** target_prop_whitelist = null, int whitelist_size = 0) {
			List<FBXAnimationCurveNode> nodes;

			// resolve attached animation nodes
			List<FBXConnection> & conns = doc.GetConnectionsByDestinationSequenced(ID(), "AnimationCurveNode");
			nodes.reserve(conns.Count);

			for (FBXConnection con : conns) {

				// link should not go to a property
				if (con.PropertyName().length()) {
					continue;
				}

				FBXObject* ob = con.SourceObject();
				if (!ob) {
					DOMWarning("failed to read source object for AnimationCurveNode.AnimationLayer link, ignoring", &element);
					continue;
				}

				FBXAnimationCurveNode anim = dynamic_cast<FBXAnimationCurveNode>(ob);
				if (!anim) {
					DOMWarning("source object for .AnimationLayer link is not an AnimationCurveNode", &element);
					continue;
				}

				if (target_prop_whitelist) {
					char* s = anim.TargetProperty().c_str();
					bool ok = false;
					for (int i = 0; i < whitelist_size; ++i) {
						if (!strcmp(s, target_prop_whitelist[i])) {
							ok = true;
							break;
						}
					}
					if (!ok) {
						continue;
					}
				}
				nodes.push_back(anim);
			}

			return nodes; // pray for NRVO

		}


		FBXPropertyTable props;
		FBXDocument doc;
	}
}
