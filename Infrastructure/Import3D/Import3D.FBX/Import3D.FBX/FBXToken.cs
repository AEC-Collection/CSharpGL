using System.Runtime.InteropServices;
using System.Text;

namespace Import3D.FBX {
    internal unsafe class FBXToken {
        public const uint BINARY_MARKER = uint.MaxValue;

        public readonly byte* begin;
        public readonly byte* end;
        public readonly TokenType type;

        [StructLayout(LayoutKind.Explicit)]
        public struct Union {
            [FieldOffset(0)]
            public long line;
            [FieldOffset(0)]
            public long offset;
        }
        public readonly Union u;

        public readonly uint column;

        public readonly string content;

        public FBXToken(byte* sbeg, byte* send, TokenType type, long offset, uint column = BINARY_MARKER) {
            this.begin = sbeg; this.end = send;
            this.type = type;
            this.u.offset = offset; this.column = column;

            var builder = new StringBuilder();
            var length = send - sbeg;
            for (long i = 0; i < length; i++) {
                var b = *(sbeg + i);
                var c = (char)b;
                builder.Append(c);
            }
            this.content = builder.ToString();
        }

        public override string ToString() {
            return $"{content}";
        }
    }

    /** Rough classification for text FBX tokens used for constructing the
 *  basic scope hierarchy. */
    internal enum TokenType {
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