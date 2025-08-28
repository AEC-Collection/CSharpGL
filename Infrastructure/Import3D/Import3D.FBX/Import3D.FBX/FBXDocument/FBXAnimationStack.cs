using System.Diagnostics;
using System.Reflection.Metadata;
using System.Xml.Linq;
using System;
namespace Import3D.FBX {
	/** Represents a FBX animation stack (i.e. a list of animation layers) */
	public unsafe class FBXAnimationStack : FBXObject {

		public FBXAnimationStack(UInt64 id, FBXElement element, string name, FBXDocument doc) {
			FBXScope sc = GetRequiredScope(element);

			// note: we don't currently use any of these properties so we shouldn't bother if it is missing
			props = GetPropertyTable(doc, "AnimationStack.FbxAnimStack", element, sc, true);

			// resolve attached animation layers
			List<FBXConnection> & conns = doc.GetConnectionsByDestinationSequenced(ID(), "AnimationLayer");
			layers.reserve(conns.Count);

			for (FBXConnection con : conns) {

				// link should not go to a property
				if (con.PropertyName().length()) {
					continue;
				}

				FBXObject* ob = con.SourceObject();
				if (!ob) {
					DOMWarning("failed to read source object for AnimationLayer.AnimationStack link, ignoring", &element);
					continue;
				}

				FBXAnimationLayer* anim = dynamic_cast<FBXAnimationLayer*>(ob);
				if (!anim) {
					DOMWarning("source object for .AnimationStack link is not an AnimationLayer", &element);
					continue;
				}
				layers.push_back(anim);
			}

		}

		UInt64 LocalStart() {
			return PropertyGet<UInt64>(Props(), "LocalStart", (0L));
		}
		UInt64 LocalStop() {
			return PropertyGet<UInt64>(Props(), "LocalStop", (0L));
		}
		UInt64 ReferenceStart() {
			return PropertyGet<UInt64>(Props(), "ReferenceStart", (0L));
		}
		UInt64 ReferenceStop() {
			return PropertyGet<UInt64>(Props(), "ReferenceStop", (0L));
		}


		FBXPropertyTable Props() {
			Debug.Assert(props.get());
			return *props;
		}

		List<FBXAnimationLayer*> Layers() {
			return layers;
		}


		FBXPropertyTable props;
		List<FBXAnimationLayer*> layers;
	}
}
