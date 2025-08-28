using System.Numerics;
using System.Xml.Linq;
using System;

namespace Import3D.FBX {
	/** DOM class for skin deformer clusters (aka sub-deformers) */
	public unsafe class FBXCluster : FBXDeformer {
		public FBXCluster(UInt64 id, FBXElement element, FBXDocument doc, string name)
			: base(id, element, doc, name) {
			FBXScope sc = GetRequiredScope(element);

			FBXElement* Indexes = sc["Indexes"];
			FBXElement* Weights = sc["Weights"];

			FBXElement Transform = GetRequiredElement(sc, "Transform", &element);
			FBXElement TransformLink = GetRequiredElement(sc, "TransformLink", &element);

			transform = ReadMatrix(Transform);
			transformLink = ReadMatrix(TransformLink);

			// it is actually possible that there are Deformer's with no weights
			if (!!Indexes != !!Weights) {
				DOMError("either Indexes or Weights are missing from Cluster", &element);
			}

			if (Indexes) {
				ParseVectorDataArray(indices, *Indexes);
				ParseVectorDataArray(weights, *Weights);
			}

			if (indices.Count != weights.Count) {
				DOMError("sizes of index and weight array don't match up", &element);
			}

			// read assigned node
			List<FBXConnection*> & conns = doc.GetConnectionsByDestinationSequenced(ID(), "Model");
			for (FBXConnection* con : conns) {
				FBXModel* mod = ProcessSimpleConnection<FBXModel>(*con, false, "Model . Cluster", element);
				if (mod) {
					node = mod;
					break;
				}
			}

			if (!node) {
				DOMError("failed to read target Node for Cluster", &element);
			}

		}



		/** get the list of deformer weights associated with this cluster.
         *  Use #GetIndices() to get the associated vertices. Both arrays
         *  have the same size (and may also be empty). */
		List<float> GetWeights() {
			return weights;
		}

		/** get indices into the vertex data of the geometry associated
         *  with this cluster. Use #GetWeights() to get the associated weights.
         *  Both arrays have the same size (and may also be empty). */
		List<uint> GetIndices() {
			return indices;
		}

		/** */
		mat4 Transform() {
			return transform;
		}

		mat4 TransformLink() {
			return transformLink;
		}

		FBXModel TargetNode() {
			return node;
		}


		List<float> weights;
		List<uint> indices;

		mat4 transform;
		mat4 transformLink;

		FBXModel node;
	}
}
