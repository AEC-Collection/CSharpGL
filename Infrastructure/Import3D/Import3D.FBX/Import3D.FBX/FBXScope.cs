using System;
using System.Xml.Linq;

namespace Import3D.FBX {
	/** FBX data entity that consists of a 'scope', a collection
 *  of not necessarily unique #FBXElement instances.
 *
 *  Example:
 *  @verbatim
 *    GlobalSettings:  {
 *        Version: 1000
 *        Properties70:
 *        [...]
 *    }
 *  @endverbatim  */
	public unsafe class FBXScope {
		public FBXElement this[string index] {
			get {
				//Dictionary<string, List<FBXElement>>::const_iterator it = elements.find(index);
				//return it == elements.end()? null : (* it).second;
				throw new NotImplementedException();
			}
		}

		public FBXElement FindElementCaseInsensitive(string &elementName) {
			char* elementNameCStr = elementName.c_str();
			for (auto element = elements.begin(); element != elements.end(); ++element) {
				if (!ASSIMP_strincmp(element.first.c_str(), elementNameCStr, AI_MAXLEN)) {
					return element.second;
				}
			}
			return null;
		}

		public ElementCollection GetCollection(string &index) {
			return elements.equal_range(index);
		}

		public Dictionary<string, List<FBXElement>> Elements() {
			return elements;
		}

		Dictionary<string, List<FBXElement>> elements;

		public FBXScope(FBXParser fBXParser, bool topLevel) {
			if (!topLevel) {
				TokenPtr t = parser.CurrentToken();
				if (t.Type() != TokenType_OPEN_BRACKET) {
					ParseError("expected open bracket", t);
				}
			}

			StackAllocator & allocator = parser.GetAllocator();
			TokenPtr n = parser.AdvanceToNextToken();
			if (n == null) {
				ParseError("unexpected end of file");
			}

			// note: empty scopes are allowed
			while (n.Type() != TokenType_CLOSE_BRACKET) {
				if (n.Type() != TokenType_KEY) {
					ParseError("unexpected token, expected TOK_KEY", n);
				}

				string &str = n.StringContents();
				if (str.empty()) {
					ParseError("unexpected content: empty string.");
				}

				auto* element = new_Element(*n, parser);

				// FBXElement() should stop at the next Key token (or right after a Close token)
				n = parser.CurrentToken();
				if (n == null) {
					if (topLevel) {
						elements.insert(ElementMap::value_type(str, element));
						return;
					}
					delete_Element(element);
					ParseError("unexpected end of file", parser.LastToken());
				}
				else {
					elements.insert(ElementMap::value_type(str, element));
				}
			}

		}
	}
}