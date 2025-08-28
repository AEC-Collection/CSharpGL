using System.Xml.Linq;

namespace Import3D.FBX {
	public unsafe class FBXLineGeometry : FBXGeometry {
		List<vec3> m_vertices;
		List<int> m_indices;

		public FBXLineGeometry(UInt64 id, FBXElement element, string name, FBXDocument doc)
			: base(id, element, name, doc) {
			FBXScope sc = element.Compound();
			if (!sc) {
				DOMError("failed to read Geometry object (class: Line), no data scope found");
			}
			FBXElement Points = GetRequiredElement(*sc, "Points", &element);
			FBXElement PointsIndex = GetRequiredElement(*sc, "PointsIndex", &element);
			ParseVectorDataArray(m_vertices, Points);
			ParseVectorDataArray(m_indices, PointsIndex);
		}
		List<vec3> GetVertices() {
			return m_vertices;
		}
		// ------------------------------------------------------------------------------------------------
		List<int> GetIndices() {
			return m_indices;
		}
	} // namespace FBX
}
