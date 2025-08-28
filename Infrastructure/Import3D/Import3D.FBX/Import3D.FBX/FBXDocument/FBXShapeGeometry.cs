using System.Xml.Linq;
using System;

namespace Import3D.FBX {
	public unsafe class FBXShapeGeometry : FBXGeometry {
		List<vec3> m_vertices;
		List<vec3> m_normals;
		List<uint> m_indices;

		FBXShapeGeometry(UInt64 id, FBXElement element, string name, FBXDocument doc)
			: base(id, element, name, doc) {
			FBXScope sc = element.Compound();
			if (null == sc) {
				DOMError("failed to read Geometry object (class: Shape), no data scope found");
			}
			FBXElement Indexes = GetRequiredElement(*sc, "Indexes", &element);
			FBXElement Vertices = GetRequiredElement(*sc, "Vertices", &element);
			ParseVectorDataArray(m_indices, Indexes);
			ParseVectorDataArray(m_vertices, Vertices);

			if ((*sc)["Normals"]) {
				FBXElement Normals = GetRequiredElement(*sc, "Normals", &element);
				ParseVectorDataArray(m_normals, Normals);
			}
		}
		// ------------------------------------------------------------------------------------------------
		List<vec3> GetVertices() {
			return m_vertices;
		}
		// ------------------------------------------------------------------------------------------------
		List<vec3> GetNormals() {
			return m_normals;
		}
		// ------------------------------------------------------------------------------------------------
		List<uint> GetIndices() {
			return m_indices;
		}

	}
}
