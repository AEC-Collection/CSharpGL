using System.Xml.Linq;
using System;
using System.Linq;

namespace Import3D.FBX {
	/** DOM class for skin deformers */
	public unsafe class FBXSkin : FBXDeformer {
		public FBXSkin(UInt64 id, FBXElement element, FBXDocument doc, string name)
			: base(id, element, doc, name) {
			FBXScope sc = GetRequiredScope(element);

			FBXElement* Link_DeformAcuracy = sc["Link_DeformAcuracy"];
			if (Link_DeformAcuracy) {
				accuracy = ParseTokenAsFloat(GetRequiredToken(*Link_DeformAcuracy, 0));
			}

			// resolve assigned clusters
			List<FBXConnection*> & conns = doc.GetConnectionsByDestinationSequenced(ID(), "Deformer");

			clusters.reserve(conns.Count);
			for (FBXConnection* con : conns) {

				FBXCluster* cluster = ProcessSimpleConnection<FBXCluster>(*con, false, "Cluster . Skin", element);
				if (cluster) {
					clusters.push_back(cluster);
					continue;
				}
			}

		}

		float DeformAccuracy() {
			return accuracy;
		}
		List<FBXCluster> Clusters() {
			return clusters;
		}


		float accuracy;
		List<FBXCluster> clusters;
	}
}
