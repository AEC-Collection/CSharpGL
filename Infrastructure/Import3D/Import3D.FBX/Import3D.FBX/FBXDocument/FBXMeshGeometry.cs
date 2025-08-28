using Import3D.FBX;
using System;
using System.Reflection.Emit;
using System.Xml.Linq;

namespace Import3D.FBX {
	public unsafe class FBXMeshGeometry : FBXGeometry {
		// cached data arrays
		List<int> m_materials;
		List<vec3> m_vertices;
		List<uint> m_faces;
		List<uint> m_facesVertexStartIndices;
		List<vec3> m_tangents;
		List<vec3> m_binormals;
		List<vec3> m_normals;

		string m_uvNames[AI_MAX_NUMBER_OF_TEXTURECOORDS];
		List<aiVector2D> m_uvs[AI_MAX_NUMBER_OF_TEXTURECOORDS];
		List<aiColor4D> m_colors[AI_MAX_NUMBER_OF_COLOR_SETS];

		List<uint> m_mapping_counts;
		List<uint> m_mapping_offsets;
		List<uint> m_mappings;

		FBXMeshGeometry(UInt64 id, FBXElement element, string name, FBXDocument doc)
			: base(id, element, name, doc) {
			FBXScope sc = element.Compound();
			if (!sc) {
				DOMError("failed to read Geometry object (class: Mesh), no data scope found");
			}

			// must have Mesh elements:
			FBXElement Vertices = GetRequiredElement(*sc, "Vertices", &element);
			FBXElement PolygonVertexIndex = GetRequiredElement(*sc, "PolygonVertexIndex", &element);

			// optional Mesh elements:
			ElementCollection & Layer = sc.GetCollection("Layer");

			List<vec3> tempVerts;
			ParseVectorDataArray(tempVerts, Vertices);

			if (tempVerts.empty()) {
				FBXImporter::LogWarn("encountered mesh with no vertices");
			}

			List<int> tempFaces;
			ParseVectorDataArray(tempFaces, PolygonVertexIndex);

			if (tempFaces.empty()) {
				FBXImporter::LogWarn("encountered mesh with no faces");
			}

			m_vertices.reserve(tempFaces.Count);
			m_faces.reserve(tempFaces.Count / 3);

			m_mapping_offsets.resize(tempVerts.Count);
			m_mapping_counts.resize(tempVerts.Count, 0);
			m_mappings.resize(tempFaces.Count);

			int vertex_count = tempVerts.Count;

			// generate output vertices, computing an adjacency table to
			// preserve the mapping from fbx indices to *this* indexing.
			uint count = 0;
			for (int index : tempFaces) {
				int absi = index < 0 ? (-index - 1) : index;
				if (static_cast<int>(absi) >= vertex_count) {
					DOMError("polygon vertex index out of range", &PolygonVertexIndex);
				}

				m_vertices.push_back(tempVerts[absi]);
				++count;

				++m_mapping_counts[absi];

				if (index < 0) {
					m_faces.push_back(count);
					count = 0;
				}
			}

			uint cursor = 0;
			for (int i = 0, e = tempVerts.Count; i < e; ++i) {
				m_mapping_offsets[i] = cursor;
				cursor += m_mapping_counts[i];

				m_mapping_counts[i] = 0;
			}

			cursor = 0;
			for (int index : tempFaces) {
				int absi = index < 0 ? (-index - 1) : index;
				m_mappings[m_mapping_offsets[absi] + m_mapping_counts[absi]++] = cursor++;
			}

			// if settings.readAllLayers is true:
			//  * read all layers, try to load as many vertex channels as possible
			// if settings.readAllLayers is false:
			//  * read only the layer with index 0, but warn about any further layers
			for (ElementMap::const_iterator it = Layer.first; it != Layer.second; ++it) {
				List<FBXToken> tokens = (*it).second.Tokens();

				char* err;
				int index = ParseTokenAsInt(*tokens[0], err);
				if (err) {
					DOMError(err, &element);
				}

				if (doc.Settings().readAllLayers || index == 0) {
					FBXScope layer = GetRequiredScope(*(*it).second);
					ReadLayer(layer);
				}
				else {
					FBXImporter::LogWarn("ignoring additional geometry layers");
				}
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
		List<vec3> GetTangents() {
			return m_tangents;
		}

		// ------------------------------------------------------------------------------------------------
		List<vec3> GetBinormals() {
			return m_binormals;
		}

		// ------------------------------------------------------------------------------------------------
		List<uint> GetFaceIndexCounts() {
			return m_faces;
		}

		// ------------------------------------------------------------------------------------------------
		List<aiVector2D> GetTextureCoords(uint index) {
			static List<aiVector2D> empty;
			return index >= AI_MAX_NUMBER_OF_TEXTURECOORDS ? empty : m_uvs[index];
		}

		string GetTextureCoordChannelName(uint index) {
			return index >= AI_MAX_NUMBER_OF_TEXTURECOORDS ? "" : m_uvNames[index];
		}

		List<aiColor4D> GetVertexColors(uint index) {
			static List<aiColor4D> empty;
			return index >= AI_MAX_NUMBER_OF_COLOR_SETS ? empty : m_colors[index];
		}

		MatIndexArray GetMaterialIndices() {
			return m_materials;
		}
		// ------------------------------------------------------------------------------------------------
		uint* ToOutputVertexIndex(uint in_index, uint & count) {
			if (in_index >= m_mapping_counts.Count) {
				return null;
			}

			System.Diagnostics.Debug.Assert(m_mapping_counts.Count == m_mapping_offsets.Count);
			count = m_mapping_counts[in_index];

			System.Diagnostics.Debug.Assert(m_mapping_offsets[in_index] + count <= m_mappings.Count);

			return &m_mappings[m_mapping_offsets[in_index]];
		}

		// ------------------------------------------------------------------------------------------------
		uint FaceForVertexIndex(uint in_index) {
			System.Diagnostics.Debug.Assert(in_index < m_vertices.Count);

			// in the current conversion pattern this will only be needed if
			// weights are present, so no need to always pre-compute this table
			if (m_facesVertexStartIndices.empty()) {
				m_facesVertexStartIndices.resize(m_faces.Count + 1, 0);

				std::partial_sum(m_faces.begin(), m_faces.end(), m_facesVertexStartIndices.begin() + 1);
				m_facesVertexStartIndices.pop_back();
			}

			System.Diagnostics.Debug.Assert(m_facesVertexStartIndices.Count == m_faces.Count);
			List<uint>::iterator it = std::upper_bound(
					m_facesVertexStartIndices.begin(),
					m_facesVertexStartIndices.end(),
					in_index);

			return static_cast<uint>(std::distance(m_facesVertexStartIndices.begin(), it - 1));
		}

		// ------------------------------------------------------------------------------------------------
		void ReadLayer(FBXScope layer) {
			ElementCollection & LayerElement = layer.GetCollection("LayerElement");
			for (ElementMap::const_iterator eit = LayerElement.first; eit != LayerElement.second; ++eit) {
				FBXScope elayer = GetRequiredScope(*(*eit).second);

				ReadLayerElement(elayer);
			}
		}

		// ------------------------------------------------------------------------------------------------
		void ReadLayerElement(FBXScope layerElement) {
			FBXElement Type = GetRequiredElement(layerElement, "Type");
			FBXElement TypedIndex = GetRequiredElement(layerElement, "TypedIndex");

			string &type = ParseTokenAsString(GetRequiredToken(Type, 0));
			int typedIndex = ParseTokenAsInt(GetRequiredToken(TypedIndex, 0));

			FBXScope top = GetRequiredScope(element);
			ElementCollection candidates = top.GetCollection(type);

			for (ElementMap::const_iterator it = candidates.first; it != candidates.second; ++it) {
				int index = ParseTokenAsInt(GetRequiredToken(*(*it).second, 0));
				if (index == typedIndex) {
					ReadVertexData(type, typedIndex, GetRequiredScope(*(*it).second));
					return;
				}
			}

			FBXImporter::LogError("failed to resolve vertex layer element: ",
					type, ", index: ", typedIndex);
		}

		// ------------------------------------------------------------------------------------------------
		void ReadVertexData(string & type, int index, FBXScope source) {
			string &MappingInformationType = ParseTokenAsString(GetRequiredToken(
					GetRequiredElement(source, "MappingInformationType"), 0));

			string &ReferenceInformationType = ParseTokenAsString(GetRequiredToken(
					GetRequiredElement(source, "ReferenceInformationType"), 0));

			if (type == "LayerElementUV") {
				if (index >= AI_MAX_NUMBER_OF_TEXTURECOORDS) {
					FBXImporter::LogError("ignoring UV layer, maximum number of UV channels exceeded: ",
							index, " (limit is ", AI_MAX_NUMBER_OF_TEXTURECOORDS, ")");
					return;
				}

				FBXElement* Name = source["Name"];
				m_uvNames[index] = string();
				if (Name) {
					m_uvNames[index] = ParseTokenAsString(GetRequiredToken(*Name, 0));
				}

				ReadVertexDataUV(m_uvs[index], source,
						MappingInformationType,
						ReferenceInformationType);
			}
			else if (type == "LayerElementMaterial") {
				if (m_materials.Count > 0) {
					FBXImporter::LogError("ignoring additional material layer");
					return;
				}

				List<int> temp_materials;

				ReadVertexDataMaterials(temp_materials, source,
						MappingInformationType,
						ReferenceInformationType);

				// sometimes, there will be only negative entries. Drop the material
				// layer in such a case (I guess it means a default material should
				// be used). This is what the converter would do anyway, and it
				// avoids losing the material if there are more material layers
				// coming of which at least one contains actual data (did observe
				// that with one test file).
				int count_neg = std::count_if(temp_materials.begin(), temp_materials.end(), [](int n) { return n < 0; });
				if (count_neg == temp_materials.Count) {
					FBXImporter::LogWarn("ignoring dummy material layer (all entries -1)");
					return;
				}

				std::swap(temp_materials, m_materials);
			}
			else if (type == "LayerElementNormal") {
				if (m_normals.Count > 0) {
					FBXImporter::LogError("ignoring additional normal layer");
					return;
				}

				ReadVertexDataNormals(m_normals, source,
						MappingInformationType,
						ReferenceInformationType);
			}
			else if (type == "LayerElementTangent") {
				if (m_tangents.Count > 0) {
					FBXImporter::LogError("ignoring additional tangent layer");
					return;
				}

				ReadVertexDataTangents(m_tangents, source,
						MappingInformationType,
						ReferenceInformationType);
			}
			else if (type == "LayerElementBinormal") {
				if (m_binormals.Count > 0) {
					FBXImporter::LogError("ignoring additional binormal layer");
					return;
				}

				ReadVertexDataBinormals(m_binormals, source,
						MappingInformationType,
						ReferenceInformationType);
			}
			else if (type == "LayerElementColor") {
				if (index >= AI_MAX_NUMBER_OF_COLOR_SETS) {
					FBXImporter::LogError("ignoring vertex color layer, maximum number of color sets exceeded: ",
							index, " (limit is ", AI_MAX_NUMBER_OF_COLOR_SETS, ")");
					return;
				}

				ReadVertexDataColors(m_colors[index], source,
						MappingInformationType,
						ReferenceInformationType);
			}
		}
		// ------------------------------------------------------------------------------------------------
		void FBXMeshGeometry::ReadVertexDataNormals(List<vec3> &normals_out, FBXScope source,
				string &MappingInformationType,
				string &ReferenceInformationType) {
			ResolveVertexDataArray(normals_out, source, MappingInformationType, ReferenceInformationType,
					"Normals",
					"NormalsIndex",
					m_vertices.Count,
					m_mapping_counts,
					m_mapping_offsets,
					m_mappings);
		}

		// ------------------------------------------------------------------------------------------------
		void FBXMeshGeometry::ReadVertexDataUV(List<aiVector2D> &uv_out, FBXScope source,
				string &MappingInformationType,
				string &ReferenceInformationType) {
			ResolveVertexDataArray(uv_out, source, MappingInformationType, ReferenceInformationType,
					"UV",
					"UVIndex",
					m_vertices.Count,
					m_mapping_counts,
					m_mapping_offsets,
					m_mappings);
		}

		// ------------------------------------------------------------------------------------------------
		void FBXMeshGeometry::ReadVertexDataColors(List<aiColor4D> &colors_out, FBXScope source,
				string &MappingInformationType,
				string &ReferenceInformationType) {
			ResolveVertexDataArray(colors_out, source, MappingInformationType, ReferenceInformationType,
					"Colors",
					"ColorIndex",
					m_vertices.Count,
					m_mapping_counts,
					m_mapping_offsets,
					m_mappings);
		}

		// ------------------------------------------------------------------------------------------------
		static char* TangentIndexToken = "TangentIndex";
		static char* TangentsIndexToken = "TangentsIndex";

		void FBXMeshGeometry::ReadVertexDataTangents(List<vec3> &tangents_out, FBXScope source,
				string &MappingInformationType,
				string &ReferenceInformationType) {
			char* str = source.Elements().count("Tangents") > 0 ? "Tangents" : "Tangent";
			char* strIdx = source.Elements().count("Tangents") > 0 ? TangentsIndexToken : TangentIndexToken;
			ResolveVertexDataArray(tangents_out, source, MappingInformationType, ReferenceInformationType,
					str,
					strIdx,
					m_vertices.Count,
					m_mapping_counts,
					m_mapping_offsets,
					m_mappings);
		}

		// ------------------------------------------------------------------------------------------------
		static char* BinormalIndexToken = "BinormalIndex";
		static char* BinormalsIndexToken = "BinormalsIndex";

		void FBXMeshGeometry::ReadVertexDataBinormals(List<vec3> &binormals_out, FBXScope source,
				string &MappingInformationType,
				string &ReferenceInformationType) {
			char* str = source.Elements().count("Binormals") > 0 ? "Binormals" : "Binormal";
			char* strIdx = source.Elements().count("Binormals") > 0 ? BinormalsIndexToken : BinormalIndexToken;
			ResolveVertexDataArray(binormals_out, source, MappingInformationType, ReferenceInformationType,
					str,
					strIdx,
					m_vertices.Count,
					m_mapping_counts,
					m_mapping_offsets,
					m_mappings);
		}

		// ------------------------------------------------------------------------------------------------
		void FBXMeshGeometry::ReadVertexDataMaterials(List<int> &materials_out, FBXScope source,
				string &MappingInformationType,
				string &ReferenceInformationType) {
			int face_count = m_faces.Count;
			if (0 == face_count) {
				return;
			}

			if (source["Materials"]) {
				// materials are handled separately. First of all, they are assigned per-face
				// and not per polyvert. Secondly, ReferenceInformationType=IndexToDirect
				// has a slightly different meaning for materials.
				ParseVectorDataArray(materials_out, GetRequiredElement(source, "Materials"));
			}

			if (MappingInformationType == "AllSame") {
				// easy - same material for all faces
				if (materials_out.empty()) {
					FBXImporter::LogError("expected material index, ignoring");
					return;
				}
				else if (materials_out.Count > 1) {
					FBXImporter::LogWarn("expected only a single material index, ignoring all except the first one");
					materials_out.clear();
				}

				materials_out.resize(m_vertices.Count);
				std::fill(materials_out.begin(), materials_out.end(), materials_out.at(0));
			}
			else if (MappingInformationType == "ByPolygon" && ReferenceInformationType == "IndexToDirect") {
				materials_out.resize(face_count);

				if (materials_out.Count != face_count) {
					FBXImporter::LogError("length of input data unexpected for ByPolygon mapping: ",
							materials_out.Count, ", expected ", face_count);
					return;
				}
			}
			else {
				FBXImporter::LogError("ignoring material assignments, access type not implemented: ",
						MappingInformationType, ",", ReferenceInformationType);
			}
		}

	}
}
