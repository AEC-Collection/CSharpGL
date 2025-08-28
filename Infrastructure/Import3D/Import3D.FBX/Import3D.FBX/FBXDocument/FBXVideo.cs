using System.Diagnostics;
using System.Xml.Linq;
using System;

namespace Import3D.FBX {
	/** DOM class for generic FBX videos */
	public unsafe class FBXVideo : FBXObject {
		public FBXVideo(UInt64 id, FBXElement element, FBXDocument doc, string name)
			: base(id, element, name) {
			FBXScope sc = GetRequiredScope(element);

			FBXElement* Type = sc["Type"];
			FBXElement* FileName = sc.FindElementCaseInsensitive("FileName"); // some files retain the information as "Filename", others "FileName", who knows
			FBXElement* RelativeFilename = sc["RelativeFilename"];
			FBXElement* Content = sc["Content"];

			if (Type) {
				type = ParseTokenAsString(GetRequiredToken(*Type, 0));
			}

			if (FileName) {
				fileName = ParseTokenAsString(GetRequiredToken(*FileName, 0));
			}

			if (RelativeFilename) {
				relativeFileName = ParseTokenAsString(GetRequiredToken(*RelativeFilename, 0));
			}

			if (Content && !Content.Tokens().empty()) {
				// this field is omitted when the embedded texture is already loaded, let's ignore if it's not found
				try {
					FBXToken & token = GetRequiredToken(*Content, 0);
					char* data = token.begin();
					if (!token.IsBinary()) {
						if (*data != '"') {
							DOMError("embedded content is not surrounded by quotation marks", &element);
						}
						else {
							int targetLength = 0;
							auto numTokens = Content.Tokens().Count;
							// First time compute size (it could be large like 64Gb and it is good to allocate it once)
							for (UInt32 tokenIdx = 0; tokenIdx < numTokens; ++tokenIdx) {
								FBXToken & dataToken = GetRequiredToken(*Content, tokenIdx);
								int tokenLength = dataToken.end() - dataToken.begin() - 2; // ignore double quotes
								char* base64data = dataToken.begin() + 1;
								int outLength = Util::ComputeDecodedSizeBase64(base64data, tokenLength);
								if (outLength == 0) {
									DOMError("Corrupted embedded content found", &element);
								}
								targetLength += outLength;
							}
							if (targetLength == 0) {
								DOMError("Corrupted embedded content found", &element);
							}
							content = new byte[targetLength];
							contentLength = static_cast<UInt64>(targetLength);
							int dst_offset = 0;
							for (UInt32 tokenIdx = 0; tokenIdx < numTokens; ++tokenIdx) {
								FBXToken & dataToken = GetRequiredToken(*Content, tokenIdx);
								int tokenLength = dataToken.end() - dataToken.begin() - 2; // ignore double quotes
								char* base64data = dataToken.begin() + 1;
								dst_offset += Util::DecodeBase64(base64data, tokenLength, content + dst_offset, targetLength - dst_offset);
							}
							if (targetLength != dst_offset) {
								delete[] content;
								contentLength = 0;
								DOMError("Corrupted embedded content found", &element);
							}
						}
					}
					else if (static_cast<int>(token.end() - data) < 5) {
						DOMError("binary data array is too short, need five (5) bytes for type signature and element count", &element);
					}
					else if (*data != 'R') {
						DOMWarning("video content is not raw binary data, ignoring", &element);
					}
					else {
						// read number of elements
						UInt32 len = 0;

						//todo: ::memcpy(&len, data + 1, sizeof(len));
						//AI_SWAP4(len);

						contentLength = len;

						content = new byte[len];

						//todo: ::memcpy(content, data + 5, len);
					}
				}
				catch (runtime_error &runtimeError) {
					// we don't need the content data for contents that has already been loaded
					ASSIMP_LOG_VERBOSE_DEBUG("Caught exception in FBXMaterial (likely because content was already loaded): ",
							runtimeError.what());
				}
				}

				props = GetPropertyTable(doc, "Video.FbxVideo", element, sc);

			}
			string Type() {
				return type;
			}
			string FileName() {
				return fileName;
			}

			string RelativeFilename() {
				return relativeFileName;
			}

			FBXPropertyTable Props() {
				Debug.Assert(props.get());
				return *props;
			}

			byte* Content() {
				Debug.Assert(content);
				return content;
			}

			UInt64 ContentLength() {
				return contentLength;
			}

			byte* RelinquishContent() {
				byte* ptr = content;
				content = null;
				return ptr;
			}


			string type;
			string relativeFileName;
			string fileName;
			FBXPropertyTable props;

			UInt64 contentLength;
			byte* content;
		}
	}
