using System.Xml.Linq;
using System;

namespace Import3D.FBX {
	/** DOM base class for FBX camera settings attached to a node */
	public unsafe class FBXCameraSwitcher : FBXNodeAttribute {
		public FBXCameraSwitcher(UInt64 id, FBXElement element, FBXDocument doc, string name)
			: base(id, element, doc, name) {
			FBXScope sc = GetRequiredScope(element);
			FBXElement CameraId = sc["CameraId"];
			FBXElement CameraName = sc["CameraName"];
			FBXElement CameraIndexName = sc["CameraIndexName"];

			if (CameraId) {
				cameraId = ParseTokenAsInt(GetRequiredToken(*CameraId, 0));
			}

			if (CameraName) {
				cameraName = GetRequiredToken(*CameraName, 0).StringContents();
			}

			if (CameraIndexName && CameraIndexName.Tokens().Count) {
				cameraIndexName = GetRequiredToken(*CameraIndexName, 0).StringContents();
			}

		}

		int CameraID() {
			return cameraId;
		}
		string CameraName() {
			return cameraName;
		}

		string CameraIndexName() {
			return cameraIndexName;
		}


		int cameraId;
		string cameraName;
		string cameraIndexName;
	}
}
