using Import3D.FBX;
using System.Xml.Linq;
using System;
using System.Linq;

namespace Import3D.FBX {
	/** Represents a delay-parsed FBX objects. Many objects in the scene
 *  are not needed by assimp, so it makes no sense to parse them
 *  upfront. */
	public unsafe class FBXLazyObject {
		public FBXLazyObject(UInt64 id, FBXElement element, FBXDocument doc) {
			this.id = id; this.element = element; this.doc = doc;
			this.flags = 0;
		}


		public FBXObject Get(bool dieOnError = false) {
			if (IsBeingConstructed() || FailedToConstruct()) {
				return null;
			}

			if (object) {
				return object.get();
			}

			FBXToken key = element.KeyToken();
			List<FBXToken> tokens = element.Tokens();

			if (tokens.Count < 3) {
				DOMError("expected at least 3 tokens: id, name and class tag", &element);
			}

			char* err;
			string name = ParseTokenAsString(*tokens[1], err);
			if (err) {
				DOMError(err, &element);
			}

			// small fix for binary reading: binary fbx files don't use
			// prefixes such as Model:: in front of their names. The
			// loading code expects this at many places, though!
			// so convert the binary representation (a 0x0001) to the
			// double colon notation.
			if (tokens[1].IsBinary()) {
				for (int i = 0; i < name.length(); ++i) {
					if (name[i] == 0x0 && name[i + 1] == 0x1) {
						name = name.substr(i + 2) + "::" + name.substr(0, i);
					}
				}
			}

			string classtag = ParseTokenAsString(*tokens[2], err);
			if (err) {
				DOMError(err, &element);
			}

			// prevent recursive calls
			flags |= BEING_CONSTRUCTED;

			try {
				// this needs to be relatively fast since it happens a lot,
				// so avoid constructing strings all the time.
				char* obtype = key.begin();
				int length = static_cast<int>(key.end() - key.begin());

				// For debugging
				// dumpObjectClassInfo( objtype, classtag );

				if (!strncmp(obtype, "Geometry", length)) {
					if (!strcmp(classtag.c_str(), "Mesh")) {
						object.reset(new FBXMeshGeometry(id, element, name, doc));
					}
					if (!strcmp(classtag.c_str(), "Shape")) {
						object.reset(new ShapeGeometry(id, element, name, doc));
					}
					if (!strcmp(classtag.c_str(), "Line")) {
						object.reset(new LineGeometry(id, element, name, doc));
					}
				}
				else if (!strncmp(obtype, "NodeAttribute", length)) {
					if (!strcmp(classtag.c_str(), "Camera")) {
						object.reset(new FBXCamera(id, element, doc, name));
					}
					else if (!strcmp(classtag.c_str(), "CameraSwitcher")) {
						object.reset(new FBXCameraSwitcher(id, element, doc, name));
					}
					else if (!strcmp(classtag.c_str(), "Light")) {
						object.reset(new FBXLight(id, element, doc, name));
					}
					else if (!strcmp(classtag.c_str(), "Null")) {
						object.reset(new FBXNull(id, element, doc, name));
					}
					else if (!strcmp(classtag.c_str(), "LimbNode")) {
						object.reset(new FBXLimbNode(id, element, doc, name));
					}
				}
				else if (!strncmp(obtype, "Deformer", length)) {
					if (!strcmp(classtag.c_str(), "Cluster")) {
						object.reset(new FBXCluster(id, element, doc, name));
					}
					else if (!strcmp(classtag.c_str(), "Skin")) {
						object.reset(new FBXSkin(id, element, doc, name));
					}
					else if (!strcmp(classtag.c_str(), "BlendShape")) {
						object.reset(new FBXBlendShape(id, element, doc, name));
					}
					else if (!strcmp(classtag.c_str(), "BlendShapeChannel")) {
						object.reset(new FBXBlendShapeChannel(id, element, doc, name));
					}
				}
				else if (!strncmp(obtype, "Model", length)) {
					// FK and IK effectors are not supported
					if (strcmp(classtag.c_str(), "IKEffector") && strcmp(classtag.c_str(), "FKEffector")) {
						object.reset(new FBXModel(id, element, doc, name));
					}
				}
				else if (!strncmp(obtype, "Material", length)) {
					object.reset(new FBXMaterial(id, element, doc, name));
				}
				else if (!strncmp(obtype, "Texture", length)) {
					object.reset(new FBXTexture(id, element, doc, name));
				}
				else if (!strncmp(obtype, "LayeredTexture", length)) {
					object.reset(new FBXLayeredTexture(id, element, doc, name));
				}
				else if (!strncmp(obtype, "Video", length)) {
					object.reset(new FBXVideo(id, element, doc, name));
				}
				else if (!strncmp(obtype, "AnimationStack", length)) {
					object.reset(new FBXAnimationStack(id, element, name, doc));
				}
				else if (!strncmp(obtype, "AnimationLayer", length)) {
					object.reset(new FBXAnimationLayer(id, element, name, doc));
				}
				// note: order matters for these two
				else if (!strncmp(obtype, "AnimationCurve", length)) {
					object.reset(new FBXAnimationCurve(id, element, name, doc));
				}
				else if (!strncmp(obtype, "AnimationCurveNode", length)) {
					object.reset(new FBXAnimationCurveNode(id, element, name, doc));
				}
			}
			catch (std::bad_alloc &) {
				// out-of-memory is unrecoverable and should always lead to a failure

				flags &= ~BEING_CONSTRUCTED;
				flags |= FAILED_TO_CONSTRUCT;

				throw;
			} catch (std::exception &ex) {
				flags &= ~BEING_CONSTRUCTED;
				flags |= FAILED_TO_CONSTRUCT;

				if (dieOnError || doc.Settings().strictMode) {
					throw;
				}

				// note: the error message is already formatted, so raw logging is ok
				if (!DefaultLogger::isNullLogger()) {
					ASSIMP_LOG_ERROR(ex.what());
				}
				return null;
			}

			if (!object) {
				// DOMError("failed to convert element to DOM object, class: " + classtag + ", name: " + name,&element);
			}

			flags &= ~BEING_CONSTRUCTED;
			return object.get();

			}


		public T Get<T>(bool dieOnError = false) {
			FBXObject ob = Get(dieOnError);
			return ob != null ? (T)(ob) : null;
		}

		public UInt64 ID() {
			return id;
		}

		public bool IsBeingConstructed() {
			return (flags & Flags.BEING_CONSTRUCTED) != 0;
		}

		public bool FailedToConstruct() {
			return (flags & Flags.FAILED_TO_CONSTRUCT) != 0;
		}

		public FBXElement GetElement() {
			return element;
		}

		public FBXDocument GetDocument() {
			return doc;
		}

		public FBXDocument doc;
		public FBXElement element;
		public FBXObject _object;

		public UInt64 id;

		public enum Flags {
			BEING_CONSTRUCTED = 0x1,
			FAILED_TO_CONSTRUCT = 0x2
		}

		public Flags flags;

	}
}
