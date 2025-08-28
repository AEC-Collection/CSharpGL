using static System.Formats.Asn1.AsnWriter;

namespace Import3D.FBX {

	/** FBX data entity that consists of a key:value tuple.
     *
     *  Example:
     *  @verbatim
     *    AnimationCurve: 23, "AnimCurve::", "" {
     *        [..]
     *    }
     *  @endverbatim
     *
     *  As can be seen in this sample, elements can contain nested #FBXScope
     *  as their trailing member.
    **/
	public unsafe class FBXElement {
		public FBXElement(FBXToken key_token, FBXParser parser) {
			this.key_token = key_token;
			FBXToken n = null;
			do {
				n = parser.AdvanceToNextToken();
				if (!n) {
					Log.WriteLine("unexpected end of file, expected closing bracket", parser.LastToken());
				}

				if (n.Type() == TokenType.TokenType_DATA) {
					tokens.push_back(n);
					FBXToken prev = n;
					n = parser.AdvanceToNextToken();
					if (!n) {
						Log.WriteLine("unexpected end of file, expected bracket, comma or key", parser.LastToken());
					}

					TokenType ty = n.Type();

					// some exporters are missing a comma on the next line
					if (ty == TokenType.TokenType_DATA && prev.Type() == TokenType.TokenType_DATA && (n.Line() == prev.Line() + 1)) {
						tokens.push_back(n);
						continue;
					}

					if (ty != TokenType.TokenType_OPEN_BRACKET && ty != TokenType.TokenType_CLOSE_BRACKET && ty != TokenType.TokenType_COMMA && ty != TokenType.TokenType_KEY) {
						Log.WriteLine("unexpected token; expected bracket, comma or key", n);
					}
				}

				if (n.Type() == TokenType.TokenType_OPEN_BRACKET) {
					compound = new_Scope(parser);

					// current token should be a TOK_CLOSE_BRACKET
					n = parser.CurrentToken();
					System.Diagnostics.Debug.Assert(n);

					if (n.Type() != TokenType.TokenType_CLOSE_BRACKET) {
						Log.WriteLine("expected closing bracket", n);
					}

					parser.AdvanceToNextToken();
					return;
				}
			}
			while (n.Type() != TokenType.TokenType_KEY && n.Type() != TokenType.TokenType_CLOSE_BRACKET);

		}

		public FBXScope Compound() {
			return compound;
		}

		public FBXToken KeyToken() {
			return key_token;
		}

		public List<FBXToken> Tokens() {
			return tokens;
		}

		public readonly FBXToken key_token;
		public readonly List<FBXToken> tokens;
		public readonly FBXScope compound;

	}
}
