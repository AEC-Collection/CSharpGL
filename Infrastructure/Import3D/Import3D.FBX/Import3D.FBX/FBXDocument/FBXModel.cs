using Import3D.FBX;
using System.Xml.Linq;
using System;
using System.Linq;

namespace Import3D.FBX {
	/** DOM base class for FBX models (even though its semantics are more "node" than "model" */
	public unsafe class FBXModel : FBXObject {
		public enum RotOrder {
			RotOrder_EulerXYZ = 0,
			RotOrder_EulerXZY,
			RotOrder_EulerYZX,
			RotOrder_EulerYXZ,
			RotOrder_EulerZXY,
			RotOrder_EulerZYX,

			RotOrder_SphericXYZ,

			RotOrder_MAX // end-of-enum sentinel
		};

		public enum TransformInheritance {
			TransformInheritance_RrSs = 0,
			TransformInheritance_RSrs,
			TransformInheritance_Rrs,

			TransformInheritance_MAX // end-of-enum sentinel
		};

		public FBXModel(UInt64 id, FBXElement element, FBXDocument doc, string name)
			: base(id, element, name) {
			this.shading = "Y";
			FBXScope sc = GetRequiredScope(element);
			FBXElement Shading = sc["Shading"];
			FBXElement Culling = sc["Culling"];

			if (Shading) {
				shading = GetRequiredToken(*Shading, 0).StringContents();
			}

			if (Culling) {
				culling = ParseTokenAsString(GetRequiredToken(*Culling, 0));
			}

			props = GetPropertyTable(doc, "Model.FbxNode", element, sc);
			ResolveLinks(element, doc);

		}


		int QuaternionInterpolate() {
			return PropertyGet<int>(Props(), "QuaternionInterpolate", (0));
		}

		vec3 RotationOffset() {
			return PropertyGet<vec3>(Props(), "RotationOffset", (vec3()));
		}
		vec3 RotationPivot() {
			return PropertyGet<vec3>(Props(), "RotationPivot", (vec3()));
		}
		vec3 ScalingOffset() {
			return PropertyGet<vec3>(Props(), "ScalingOffset", (vec3()));
		}
		vec3 ScalingPivot() {
			return PropertyGet<vec3>(Props(), "ScalingPivot", (vec3()));
		}
		bool TranslationActive() {
			return PropertyGet<bool>(Props(), "TranslationActive", (false));
		}

		vec3 TranslationMin() {
			return PropertyGet<vec3>(Props(), "TranslationMin", (vec3()));
		}
		vec3 TranslationMax() {
			return PropertyGet<vec3>(Props(), "TranslationMax", (vec3()));
		}

		bool TranslationMinX() {
			return PropertyGet<bool>(Props(), "TranslationMinX", (false));
		}
		bool TranslationMaxX() {
			return PropertyGet<bool>(Props(), "TranslationMaxX", (false));
		}
		bool TranslationMinY() {
			return PropertyGet<bool>(Props(), "TranslationMinY", (false));
		}
		bool TranslationMaxY() {
			return PropertyGet<bool>(Props(), "TranslationMaxY", (false));
		}
		bool TranslationMinZ() {
			return PropertyGet<bool>(Props(), "TranslationMinZ", (false));
		}
		bool TranslationMaxZ() {
			return PropertyGet<bool>(Props(), "TranslationMaxZ", (false));
		}

		RotOrder RotationOrder() {
			int ival = PropertyGet<int>(Props(), "RotationOrder", (int)(0));
			if (ival < 0 || ival >= (int)RotOrder.RotOrder_MAX) {
				//(void)((!!((int)(0) >= 0)) || (Assimp::aiAssertViolation("(int)(0) >= 0", "D:\\Projects\\assimp\\code\\AssetLib\\FBX\\FBXDocument.h", 466), 0));
				//(void)((!!((int)(0) < RotOrder_MAX)) || (Assimp::aiAssertViolation("(int)(0) < AI_CONCAT(RotOrder, _MAX)", "D:\\Projects\\assimp\\code\\AssetLib\\FBX\\FBXDocument.h", 466), 0));
				return (RotOrder)(0);
			}
			return (RotOrder)(ival);
		}
		bool RotationSpaceForLimitOnly() {
			return PropertyGet<bool>(Props(), "RotationSpaceForLimitOnly", (false));
		}
		float RotationStiffnessX() {
			return PropertyGet<float>(Props(), "RotationStiffnessX", (0.0f));
		}
		float RotationStiffnessY() {
			return PropertyGet<float>(Props(), "RotationStiffnessY", (0.0f));
		}
		float RotationStiffnessZ() {
			return PropertyGet<float>(Props(), "RotationStiffnessZ", (0.0f));
		}
		float AxisLen() {
			return PropertyGet<float>(Props(), "AxisLen", (0.0f));
		}

		vec3 PreRotation() {
			return PropertyGet<vec3>(Props(), "PreRotation", (vec3()));
		}
		vec3 PostRotation() {
			return PropertyGet<vec3>(Props(), "PostRotation", (vec3()));
		}
		bool RotationActive() {
			return PropertyGet<bool>(Props(), "RotationActive", (false));
		}

		vec3 RotationMin() {
			return PropertyGet<vec3>(Props(), "RotationMin", (vec3()));
		}
		vec3 RotationMax() {
			return PropertyGet<vec3>(Props(), "RotationMax", (vec3()));
		}

		bool RotationMinX() {
			return PropertyGet<bool>(Props(), "RotationMinX", (false));
		}
		bool RotationMaxX() {
			return PropertyGet<bool>(Props(), "RotationMaxX", (false));
		}
		bool RotationMinY() {
			return PropertyGet<bool>(Props(), "RotationMinY", (false));
		}
		bool RotationMaxY() {
			return PropertyGet<bool>(Props(), "RotationMaxY", (false));
		}
		bool RotationMinZ() {
			return PropertyGet<bool>(Props(), "RotationMinZ", (false));
		}
		bool RotationMaxZ() {
			return PropertyGet<bool>(Props(), "RotationMaxZ", (false));
		}
		fbx_simple_enum_property(InheritType, TransformInheritance, 0)


	bool ScalingActive() {
			return PropertyGet<bool>(Props(), "ScalingActive", (false));
		}
		vec3 ScalingMin() {
			return PropertyGet<vec3>(Props(), "ScalingMin", (vec3()));
		}
		vec3 ScalingMax() {
			return PropertyGet<vec3>(Props(), "ScalingMax", (vec3(1.0f, 1.0f, 1.0f)));
		}
		bool ScalingMinX() {
			return PropertyGet<bool>(Props(), "ScalingMinX", (false));
		}
		bool ScalingMaxX() {
			return PropertyGet<bool>(Props(), "ScalingMaxX", (false));
		}
		bool ScalingMinY() {
			return PropertyGet<bool>(Props(), "ScalingMinY", (false));
		}
		bool ScalingMaxY() {
			return PropertyGet<bool>(Props(), "ScalingMaxY", (false));
		}
		bool ScalingMinZ() {
			return PropertyGet<bool>(Props(), "ScalingMinZ", (false));
		}
		bool ScalingMaxZ() {
			return PropertyGet<bool>(Props(), "ScalingMaxZ", (false));
		}

		vec3 GeometricTranslation() {
			return PropertyGet<vec3>(Props(), "GeometricTranslation", (vec3()));
		}
		vec3 GeometricRotation() {
			return PropertyGet<vec3>(Props(), "GeometricRotation", (vec3()));
		}
		vec3 GeometricScaling() {
			return PropertyGet<vec3>(Props(), "GeometricScaling", (vec3(1.0f, 1.0f, 1.0f)));
		}

		float MinDampRangeX() {
			return PropertyGet<float>(Props(), "MinDampRangeX", (0.0f));
		}
		float MinDampRangeY() {
			return PropertyGet<float>(Props(), "MinDampRangeY", (0.0f));
		}
		float MinDampRangeZ() {
			return PropertyGet<float>(Props(), "MinDampRangeZ", (0.0f));
		}
		float MaxDampRangeX() {
			return PropertyGet<float>(Props(), "MaxDampRangeX", (0.0f));
		}
		float MaxDampRangeY() {
			return PropertyGet<float>(Props(), "MaxDampRangeY", (0.0f));
		}
		float MaxDampRangeZ() {
			return PropertyGet<float>(Props(), "MaxDampRangeZ", (0.0f));
		}

		float MinDampStrengthX() {
			return PropertyGet<float>(Props(), "MinDampStrengthX", (0.0f));
		}
		float MinDampStrengthY() {
			return PropertyGet<float>(Props(), "MinDampStrengthY", (0.0f));
		}
		float MinDampStrengthZ() {
			return PropertyGet<float>(Props(), "MinDampStrengthZ", (0.0f));
		}
		float MaxDampStrengthX() {
			return PropertyGet<float>(Props(), "MaxDampStrengthX", (0.0f));
		}
		float MaxDampStrengthY() {
			return PropertyGet<float>(Props(), "MaxDampStrengthY", (0.0f));
		}
		float MaxDampStrengthZ() {
			return PropertyGet<float>(Props(), "MaxDampStrengthZ", (0.0f));
		}

		float PreferredAngleX() {
			return PropertyGet<float>(Props(), "PreferredAngleX", (0.0f));
		}
		float PreferredAngleY() {
			return PropertyGet<float>(Props(), "PreferredAngleY", (0.0f));
		}
		float PreferredAngleZ() {
			return PropertyGet<float>(Props(), "PreferredAngleZ", (0.0f));
		}

		bool Show() {
			return PropertyGet<bool>(Props(), "Show", (true));
		}
		bool LODBox() {
			return PropertyGet<bool>(Props(), "LODBox", (false));
		}
		bool Freeze() {
			return PropertyGet<bool>(Props(), "Freeze", (false));
		}

		string Shading() {
			return shading;
		}

		string Culling() {
			return culling;
		}

		FBXPropertyTable Props() {
			System.Diagnostics.Debug.Assert(props.get());
			return *props;
		}

		/** Get material links */
		List<FBXMaterial> GetMaterials() {
			return materials;
		}

		/** Get geometry links */
		List<FBXGeometry> GetGeometry() {
			return geometry;
		}

		/** Get node attachments */
		List<FBXNodeAttribute> GetAttributes() {
			return attributes;
		}

		/** convenience method to check if the node has a Null node marker */
		bool IsNull() {
			List<FBXNodeAttribute*> & attrs = GetAttributes();
			for (FBXNodeAttribute* att : attrs) {

				FBXNull* null_tag = dynamic_cast<FBXNull*>(att);
				if (null_tag) {
					return true;
				}
			}

			return false;

		}
		void ResolveLinks(FBXElement element, FBXDocument doc) {
			char* arr[] = { "Geometry", "Material", "NodeAttribute" };

			// resolve material
			List<FBXConnection> & conns = doc.GetConnectionsByDestinationSequenced(ID(), arr, 3);

			materials.reserve(conns.Count);
			geometry.reserve(conns.Count);
			attributes.reserve(conns.Count);
			for (FBXConnection con : conns) {

				// material and geometry links should be Object-Object connections
				if (con.PropertyName().length()) {
					continue;
				}

				FBXObject* ob = con.SourceObject();
				if (!ob) {
					DOMWarning("failed to read source object for incoming Model link, ignoring", &element);
					continue;
				}

				FBXMaterial* mat = dynamic_cast<FBXMaterial*>(ob);
				if (mat) {
					materials.push_back(mat);
					continue;
				}

				FBXGeometry* geo = dynamic_cast<FBXGeometry*>(ob);
				if (geo) {
					geometry.push_back(geo);
					continue;
				}

				FBXNodeAttribute* att = dynamic_cast<FBXNodeAttribute*>(ob);
				if (att) {
					attributes.push_back(att);
					continue;
				}

				DOMWarning("source object for model link is neither Material, NodeAttribute nor Geometry, ignoring", &element);
				continue;
			}

		}

		List<FBXMaterial> materials;
		List<FBXGeometry> geometry;
		List<FBXNodeAttribute> attributes;

		string shading;
		string culling;
		FBXPropertyTable props;

	}
}
