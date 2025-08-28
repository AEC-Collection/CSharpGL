using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Import3D.FBX {
	public unsafe class FBXToken {
		public uint BINARY_MARKER = uint.MaxValue;

		public readonly byte* sbegin;
		public readonly byte* send;
		public readonly TokenType type;

		[StructLayout(LayoutKind.Explicit)]
		public struct Union {
			[FieldOffset(0)]
			public int line;
			[FieldOffset(0)]
			public int offset;
		}
		public readonly Union u;

		public readonly uint column;

		public readonly string content;

		public FBXToken(byte* sbeg, byte* send, TokenType type, int line, uint column = BINARY_MARKER) {
			this.sbegin = sbeg; this.send = send;
			this.type = type;
			this.u.line = line; this.column = column;

			var builder = new StringBuilder();
			var length = send - sbeg;
			for (long i = 0; i < length; i++) {
				var b = *(sbeg + i);
				var c = (char)b;
				builder.Append(c);
			}
			this.content = builder.ToString();
			// tokens must be of non-zero length
			System.Diagnostics.Debug.Assert(static_cast<int>(send - sbegin) > 0);

			// binary tokens may have zero length because they are sometimes dummies
			// inserted by TokenizeBinary()
			System.Diagnostics.Debug.Assert(send >= sbegin);

		}

		public override string ToString() {
			return $"{content}";
		}

		public string StringContents() {
			//return string(begin(), end());
			return this.content;
		}

		public bool IsBinary() {
			return column == BINARY_MARKER;
		}

		public byte* begin() {
			return sbegin;
		}

		public byte* end() {
			return send;
		}

		public TokenType Type() {
			return type;
		}

		public int Offset() {
			Debug.Assert(IsBinary());
			return u.offset;
		}

		public int Line() {
			Debug.Assert(!IsBinary());
			return u.line;
		}

		public uint Column() {
			Debug.Assert(!IsBinary());
			return column;
		}

	}

	/** Rough classification for text FBX tokens used for constructing the
 *  basic scope hierarchy. */
	public enum TokenType {
		// {
		TokenType_OPEN_BRACKET = 0,

		// }
		TokenType_CLOSE_BRACKET,

		// '"blablubb"', '2', '*14' - very general token class,
		// further processing happens at a later stage.
		TokenType_DATA,

		//
		TokenType_BINARY_DATA,

		// ,
		TokenType_COMMA,

		// blubb:
		TokenType_KEY
	}

}