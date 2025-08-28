using static System.Formats.Asn1.AsnWriter;

namespace Import3D.FBX {
    /** FBX parsing class, takes a list of input tokens and generates a hierarchy
 *  of nested #FBXScope instances, representing the fbx DOM.*/
    public unsafe class FBXParser {
        FBXScope GetRootScope() {
            return *root;
        }

        bool IsBinary() {
            return is_binary;
        }

        StackAllocator &GetAllocator() {
            return allocator;
        }

        List<FBXToken> tokens;
        FBXToken last, current;
        int cursor = 0;
        public FBXScope root;

        public readonly bool is_binary;

        /** Parse given a token list. Does not take ownership of the tokens -
            *  the objects must persist during the entire parser lifetime */
        public FBXParser(List<FBXToken> tokenList, bool is_binary) {
            this.tokens = tokenList;
            this.cursor = 0;// tokenList[0];
            this.is_binary = is_binary;
            this.root = new FBXScope(this, true);
        }
        // ------------------------------------------------------------------------------------------------
        FBXToken FBXParser::AdvanceToNextToken() {
            last = current;
            if (cursor == tokens.end()) {
                current = null;
            }
            else {
                current = *cursor++;
            }
            return current;
        }

        // ------------------------------------------------------------------------------------------------
        FBXToken FBXParser::CurrentToken() {
            return current;
        }

        // ------------------------------------------------------------------------------------------------
        FBXToken FBXParser::LastToken() {
            return last;
        }


        //////////////////////////////////////////

        // ------------------------------------------------------------------------------------------------
        UInt64 ParseTokenAsID(FBXToken t, char*&err_out) {
            err_out = null;

            if (t.Type() != TokenType_DATA) {
                err_out = "expected TOK_DATA token";
                return 0L;
            }

            if (t.IsBinary()) {
                char* data = t.begin();
                if (data[0] != 'L') {
                    err_out = "failed to parse ID, unexpected data type, expected L(ong) (binary)";
                    return 0L;
                }

                UInt64 id = Utility.SafeParse<UInt64>(data + 1, t.end());
                //AI_SWAP8(id);
                return id;
            }

            // XXX: should use int here
            uint length = static_cast<uint>(t.end() - t.begin());
            System.Diagnostics.Debug.Assert(length > 0);

            char* outValue = null;
            UInt64 id = Import3D.Utility.strtoul10_64(t.begin(), outValue, &length);
            if (outValue > t.end()) {
                err_out = "failed to parse ID (text)";
                return 0L;
            }

            return id;
        }






        // ------------------------------------------------------------------------------------------------


        // ------------------------------------------------------------------------------------------------
        mat4 ReadMatrix(FBXElement element) {
            List<float> values;
            ParseVectorDataArray(values, element);

            if (values.Count != 16) {
                ParseError("expected 16 matrix elements");
            }

            mat4 result;

            result.a1 = values[0];
            result.a2 = values[1];
            result.a3 = values[2];
            result.a4 = values[3];

            result.b1 = values[4];
            result.b2 = values[5];
            result.b3 = values[6];
            result.b4 = values[7];

            result.c1 = values[8];
            result.c2 = values[9];
            result.c3 = values[10];
            result.c4 = values[11];

            result.d1 = values[12];
            result.d2 = values[13];
            result.d3 = values[14];
            result.d4 = values[15];

            result.Transpose();
            return result;
        }

        // ------------------------------------------------------------------------------------------------
        // wrapper around ParseTokenAsString() with ParseError handling
        string ParseTokenAsString(FBXToken t) {
            char* err;
            string &i = ParseTokenAsString(t, err);
            if (err) {
                ParseError(err, t);
            }
            return i;
        }

        bool HasElement(FBXScope sc, string &index) {
            FBXElement* el = sc[index];
            if (null == el) {
                return false;
            }

            return true;
        }



    }
}
