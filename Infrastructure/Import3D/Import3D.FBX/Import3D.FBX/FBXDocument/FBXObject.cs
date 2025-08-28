using System.Xml.Linq;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Diagnostics;
using System.Reflection;

namespace Import3D.FBX {
    /** Base class for in-memory (DOM) representations of FBX objects */
    public unsafe class FBXObject {
        public float kFovUnknown = -1.0f;

        public FBXObject(UInt64 id, FBXElement element, string name) {
            this.id = id; this.element = element; this.name = name;
        }


        FBXElement SourceElement() {
            return element;
        }

        string Name() {
            return name;
        }

        UInt64 ID() {
            return id;
        }


        public readonly FBXElement element;
        public readonly string name;
        public readonly UInt64 id;

        public void ParseError(string message, object obj = null) {
            throw new Exception($"{message} {obj}");
        }
        public void ParseError(byte* message, object obj) {
            throw new Exception($"{message[0]} {obj}");
        }   // ------------------------------------------------------------------------------------------------
        // read the type code and element count of a binary data array and stop there
        void ReadBinaryDataArrayHead(byte* data, byte* end, out byte type, out UInt32 count, FBXElement el) {
            if ((end - data) < 5) {
                ParseError("binary data array is too short, need five (5) bytes for type signature and element count", el);
            }

            // data type
            type = *data;

            // read number of elements
            var len = Utility.SafeParse<UInt32>(data + 1, end);
            ////AI_SWAP4(len);

            count = len;
            data += 5;
        }
        // ------------------------------------------------------------------------------------------------
        // read an array of float3 tuples
        public void ParseVectorDataArray(List<vec3> outValue, FBXElement el) {
            outValue.Clear();//.resize(0);

            List<FBXToken> tok = el.Tokens();
            if (tok.Count == 0) {
                ParseError("unexpected empty element", el);
            }

            if (tok[0].IsBinary()) {
                var data = tok[0].begin(); var end = tok[0].end();

                byte type;
                UInt32 count;
                ReadBinaryDataArrayHead(data, end, out type, out count, el);

                if (count % 3 != 0) {
                    ParseError("number of floats is not a multiple of three (3) (binary)", el);
                }

                if (count == 0) {
                    return;
                }

                if (type != 'd' && type != 'f') {
                    ParseError("expected float or double array (binary)", el);
                }

                byte[] buff;
                ReadBinaryDataArray(type, count, data, end, out buff, el);

                System.Diagnostics.Debug.Assert(data == end);
                var dataToRead = count * (type == 'd' ? 8 : 4);
                if (dataToRead != buff.Length) {
                    ParseError("Invalid read size (binary)", el);
                }

                UInt32 count3 = count / 3;
                outValue.Capacity = (int)count3;//.reserve(count3);

                if (type == 'd') {
                    fixed (byte* pBuff = buff) {
                        var d = (double*)pBuff;// (&buff[0]);
                        for (uint i = 0; i < count3; ++i, d += 3) {
                            outValue.Add(new vec3((float)d[0], (float)d[1], (float)d[2]));
                            //.emplace_back(static_cast<ai_real>(d[0]),
                            //static_cast<ai_real>(d[1]),
                            //static_cast<ai_real>(d[2]));
                        }
                    }
                    // for debugging
                    /*for ( int i = 0; i < outValue.Count; i++ ) {
                        vec3 item = outValue[i];
                        Log.WriteLine(item);
                        vec3 vec3( outValue[ i ] );
                        std::stringstream stream;
                        stream << " vec3.x = " << vec3.x << " vec3.y = " << vec3.y << " vec3.z = " << vec3.z << std::endl;
                        DefaultLogger::get().info( stream.str() );
                    }*/
                }
                else if (type == 'f') {
                    fixed (byte* pBuff = buff) {
                        float* f = (float*)pBuff;// reinterpret_cast<float*>(&buff[0]);
                        for (uint i = 0; i < count3; ++i, f += 3) {
                            //outValue.emplace_back(f[0], f[1], f[2]);
                            outValue.Add(new vec3(f[0], f[1], f[2]));
                        }
                    }
                }

                return;
            }

            int dim = ParseTokenAsDim(tok[0]);

            // may throw bad_alloc if the input is rubbish, but this need
            // not to be prevented - importing would fail but we wouldn't
            // crash since assimp handles this case properly.
            outValue.Capacity = dim;// .reserve(dim);

            FBXScope scope = GetRequiredScope(el);
            FBXElement a = GetRequiredElement(scope, "a", el);

            if (a.Tokens().Count % 3 != 0) {
                ParseError("number of floats is not a multiple of three (3)", el);
            }
            //for (List<FBXToken>::const_iterator it = a.Tokens().begin(), end = a.Tokens().end(); it != end;) 
            vec3 v = new vec3(); int index = 0;
            foreach (var item in a.Tokens()) {
                switch (index) {
                case 0:
                v.x = ParseTokenAsFloat(item);// **it++);
                index++;
                break;
                case 1:
                v.y = ParseTokenAsFloat(item);// **it++);
                index++;
                break;
                case 2:
                v.z = ParseTokenAsFloat(item);// **it++);
                outValue.Add(v);
                index = 0;
                break;
                default:
                break;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------
        // read binary data array, assume cursor points to the 'compression mode' field (i.e. behind the header)
        void ReadBinaryDataArray(byte type, UInt32 count, byte* data, byte* end, out byte[] buff, FBXElement el) {
            UInt32 encmode = Utility.SafeParse<UInt32>(data, end);
            ////AI_SWAP4(encmode);
            data += 4;

            // next comes the compressed length
            UInt32 comp_len = Utility.SafeParse<UInt32>(data, end);
            ////AI_SWAP4(comp_len);
            data += 4;

            System.Diagnostics.Debug.Assert(data + comp_len == end);

            // determine the length of the uncompressed data by looking at the type signature
            UInt32 stride = 0;
            switch ((char)type) {
            case 'f':
            case 'i':
            stride = 4;
            break;

            case 'd':
            case 'l':
            stride = 8;
            break;

            default:
            System.Diagnostics.Debug.Assert(false);
            break;
            }
            ;

            UInt32 full_length = stride * count;
            buff = new byte[full_length];//.resize(full_length);

            if (encmode == 0) {
                System.Diagnostics.Debug.Assert(full_length == comp_len);

                // plain data, no compression
                //std::copy(data, end, buff.begin());
                for (int i = 0; i <= (int)(end - data); i++) {
                    buff[i] = data[i];
                }
            }
            else if (encmode == 1) {
                // zlib/deflate, next comes ZIP head (0x78 0x01)
                // see http://www.ietf.org/rfc/rfc1950.txt
                Compression compress = new Compression();
                if (compress.open(Compression.Format.Binary, Compression.FlushMode.Finish, 0)) {
                    compress.decompress(data, (int)comp_len, buff);
                    compress.close();
                }
            }
            //# ifdef ASSIMP_BUinValueILD_DEBUG
            else {
                // runtime check for this happens at tokenization stage
                System.Diagnostics.Debug.Assert(false);
            }
            //#endif

            data += comp_len;
            System.Diagnostics.Debug.Assert(data == end);
        }

        // ------------------------------------------------------------------------------------------------
        // read an array of color4 tuples
        void ParseVectorDataArray(List<vec4> outValue, FBXElement el) {
            outValue.Clear();//.resize(0);
            List<FBXToken> tok = el.Tokens();
            if (tok.Count == 0) {
                ParseError("unexpected empty element", el);
            }

            if (tok[0].IsBinary()) {
                var data = tok[0].begin(); var end = tok[0].end();

                byte type;
                UInt32 count;
                ReadBinaryDataArrayHead(data, end, out type, out count, el);

                if (count % 4 != 0) {
                    ParseError("number of floats is not a multiple of four (4) (binary)", el);
                }

                if (count == 0) {
                    return;
                }

                if (type != 'd' && type != 'f') {
                    ParseError("expected float or double array (binary)", el);
                }

                byte[] buff;
                ReadBinaryDataArray(type, count, data, end, out buff, el);

                System.Diagnostics.Debug.Assert(data == end);
                var dataToRead = (count) * (type == 'd' ? 8 : 4);
                if (dataToRead != buff.Length) {
                    ParseError("Invalid read size (binary)", el);
                }

                var count4 = count / 4;
                outValue.Capacity = (int)count4;//.reserve(count4);

                if (type == 'd') {
                    fixed (byte* pBuff = buff) {
                        double* d = (double*)pBuff;// reinterpret_cast<double*>(&buff[0]);
                        for (uint i = 0; i < count4; ++i, d += 4) {
                            //outValue.emplace_back(static_cast<float>(d[0]),
                            //        static_cast<float>(d[1]),
                            //        static_cast<float>(d[2]),
                            //        static_cast<float>(d[3]));
                            outValue.Add(new vec4((float)d[0], (float)d[1], (float)d[2], (float)d[3]));
                        }
                    }
                }
                else if (type == 'f') {
                    fixed (byte* pBuff = buff) {
                        float* f = (float*)pBuff;// reinterpret_cast<float*>(&buff[0]);
                        for (uint i = 0; i < count4; ++i, f += 4) {
                            //outValue.emplace_back(f[0], f[1], f[2], f[3]);
                            outValue.Add(new vec4(f[0], f[1], f[2], f[3]));
                        }
                    }
                }
                return;
            }

            int dim = ParseTokenAsDim(tok[0]);

            //  see notes in ParseVectorDataArray() above
            outValue.Capacity = dim;//.reserve(dim);

            FBXScope scope = GetRequiredScope(el);
            FBXElement a = GetRequiredElement(scope, "a", el);

            if (a.Tokens().Count % 4 != 0) {
                ParseError("number of floats is not a multiple of four (4)", el);
            }
            //for (List<FBXToken>::const_iterator it = a.Tokens().begin(), end = a.Tokens().end(); it != end;) 
            var value = new vec4(); var index = 0;
            foreach (var item in a.Tokens()) {
                switch (index) {
                case 0: value.x = ParseTokenAsFloat(item); index++; break;
                case 1: value.y = ParseTokenAsFloat(item); index++; break;
                case 2: value.z = ParseTokenAsFloat(item); index++; break;
                case 3: value.w = ParseTokenAsFloat(item); outValue.Add(value); index = 0; break;
                default:
                break;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------
        // read an array of float2 tuples
        void ParseVectorDataArray(List<vec2> outValue, FBXElement el) {
            outValue.Clear(); // .resize(0);
            List<FBXToken> tok = el.Tokens();
            if (tok.Count == 0) {
                ParseError("unexpected empty element", el);
            }

            if (tok[0].IsBinary()) {
                var data = tok[0].begin(); var end = tok[0].end();

                byte type;
                UInt32 count;
                ReadBinaryDataArrayHead(data, end, out type, out count, el);

                if (count % 2 != 0) {
                    ParseError("number of floats is not a multiple of two (2) (binary)", el);
                }

                if (count == 0) {
                    return;
                }

                if (type != 'd' && type != 'f') {
                    ParseError("expected float or double array (binary)", el);
                }

                byte[] buff;
                ReadBinaryDataArray(type, count, data, end, out buff, el);

                System.Diagnostics.Debug.Assert(data == end);
                var dataToRead = (count) * (type == 'd' ? 8 : 4);
                if (dataToRead != buff.Length) {
                    ParseError("Invalid read size (binary)", el);
                }

                var count2 = count / 2;
                outValue.Capacity = (int)count2;//.reserve(count2);

                if (type == 'd') {
                    fixed (byte* pBuff = buff) {
                        double* d = (double*)pBuff;// reinterpret_cast<double*>(&buff[0]);
                        for (uint i = 0; i < count2; ++i, d += 2) {
                            //outValue.emplace_back(static_cast<float>(d[0]),
                            //static_cast<float>(d[1]));
                            outValue.Add(new vec2((float)d[0], (float)d[1]));
                        }
                    }
                }
                else if (type == 'f') {
                    fixed (byte* pBuff = buff) {
                        float* f = (float*)pBuff;// reinterpret_cast<float*>(&buff[0]);
                        for (uint i = 0; i < count2; ++i, f += 2) {
                            //outValue.emplace_back(f[0], f[1]);
                            outValue.Add(new vec2(f[0], f[1]));
                        }
                    }
                }

                return;
            }

            int dim = ParseTokenAsDim(tok[0]);

            // see notes in ParseVectorDataArray() above
            outValue.Capacity = dim;//.reserve(dim);

            FBXScope scope = GetRequiredScope(el);
            FBXElement a = GetRequiredElement(scope, "a", el);

            if (a.Tokens().Count % 2 != 0) {
                ParseError("number of floats is not a multiple of two (2)", el);
            }
            //for (List<FBXToken>::const_iterator it = a.Tokens().begin(), end = a.Tokens().end(); it != end;) 
            var value = new vec2(); var index = 0;
            foreach (var item in a.Tokens()) {
                switch (index) {
                case 0: value.x = ParseTokenAsFloat(item); index++; break;
                case 1: value.y = ParseTokenAsFloat(item); outValue.Add(value); index = 0; break;
                default:
                break;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------
        // read an array of ints
        void ParseVectorDataArray(List<int> outValue, FBXElement el) {
            outValue.Clear(); // .resize(0);
            List<FBXToken> tok = el.Tokens();
            if (tok.Count == 0) {
                ParseError("unexpected empty element", el);
            }

            if (tok[0].IsBinary()) {
                var data = tok[0].begin(); var end = tok[0].end();

                byte type;
                UInt32 count;
                ReadBinaryDataArrayHead(data, end, out type, out count, el);

                if (count == 0) {
                    return;
                }

                if (type != 'i') {
                    ParseError("expected int array (binary)", el);
                }

                byte[] buff;
                ReadBinaryDataArray(type, count, data, end, out buff, el);

                System.Diagnostics.Debug.Assert(data == end);
                var dataToRead = (count) * 4;
                if (dataToRead != buff.Length) {
                    ParseError("Invalid read size (binary)", el);
                }

                outValue.Capacity = (int)count;//.reserve(count);

                fixed (byte* pBuff = buff) {
                    var ip = (Int32*)pBuff;
                    for (uint i = 0; i < count; ++i, ++ip) {
                        var val = *ip;
                        //AI_SWAP4(val);
                        //outValue.push_back(val);
                        outValue.Add(val);
                    }
                }

                return;
            }

            int dim = ParseTokenAsDim(tok[0]);

            // see notes in ParseVectorDataArray()
            outValue.Capacity = dim;//.reserve(dim);

            FBXScope scope = GetRequiredScope(el);
            FBXElement a = GetRequiredElement(scope, "a", el);

            //for (List<FBXToken>::const_iterator it = a.Tokens().begin(), end = a.Tokens().end(); it != end;) 
            foreach (var item in a.Tokens()) {
                var ival = ParseTokenAsInt(item);
                //outValue.push_back(ival);
                outValue.Add(ival);
            }
        }

        // ------------------------------------------------------------------------------------------------
        // read an array of floats
        void ParseVectorDataArray(List<float> outValue, FBXElement el) {
            outValue.Clear(); // .resize(0);
            List<FBXToken> tok = el.Tokens();
            if (tok.Count == 0) {
                ParseError("unexpected empty element", el);
            }

            if (tok[0].IsBinary()) {
                var data = tok[0].begin(); var end = tok[0].end();

                byte type;
                UInt32 count;
                ReadBinaryDataArrayHead(data, end, out type, out count, el);

                if (count == 0) {
                    return;
                }

                if (type != 'd' && type != 'f') {
                    ParseError("expected float or double array (binary)", el);
                }

                byte[] buff;
                ReadBinaryDataArray(type, count, data, end, out buff, el);

                System.Diagnostics.Debug.Assert(data == end);
                var dataToRead = (count) * (type == 'd' ? 8 : 4);
                if (dataToRead != buff.Length) {
                    ParseError("Invalid read size (binary)", el);
                }

                if (type == 'd') {
                    fixed (byte* pBuff = buff) {
                        double* d = (double*)pBuff; // reinterpret_cast<double*>(&buff[0]);
                        for (uint i = 0; i < count; ++i, ++d) {
                            //outValue.push_back(static_cast<float>(*d));
                            outValue.Add((float)d[i]);
                        }
                    }
                }
                else if (type == 'f') {
                    fixed (byte* pBuff = buff) {
                        float* f = (float*)pBuff; // reinterpret_cast<float*>(&buff[0]);
                        for (uint i = 0; i < count; ++i, ++f) {
                            //outValue.push_back(*f);
                            outValue.Add(f[i]);
                        }
                    }
                }

                return;
            }

            int dim = ParseTokenAsDim(tok[0]);

            // see notes in ParseVectorDataArray()
            outValue.Capacity = dim;//.reserve(dim);

            FBXScope scope = GetRequiredScope(el);
            FBXElement a = GetRequiredElement(scope, "a", el);

            foreach (var item in a.Tokens()) {
                float ival = ParseTokenAsFloat(item);
                //outValue.push_back(ival);
                outValue.Add(ival);
            }
        }

        // ------------------------------------------------------------------------------------------------
        // read an array of uints
        void ParseVectorDataArray(List<uint> outValue, FBXElement el) {
            outValue.Clear(); // .resize(0);
            List<FBXToken> tok = el.Tokens();
            if (tok.Count == 0) {
                ParseError("unexpected empty element", el);
            }

            if (tok[0].IsBinary()) {
                var data = tok[0].begin(); var end = tok[0].end();

                byte type;
                UInt32 count;
                ReadBinaryDataArrayHead(data, end, out type, out count, el);

                if (count == 0) {
                    return;
                }

                if (type != 'i') {
                    ParseError("expected (u)int array (binary)", el);
                }

                byte[] buff;
                ReadBinaryDataArray(type, count, data, end, out buff, el);

                System.Diagnostics.Debug.Assert(data == end);
                var dataToRead = (count) * 4;
                if (dataToRead != buff.Length) {
                    ParseError("Invalid read size (binary)", el);
                }

                outValue.Capacity = (int)count; // .reserve(count);

                fixed (byte* pBuff = buff) {
                    var ip = (Int32*)pBuff;
                    for (uint i = 0; i < count; ++i, ++ip) {
                        var val = *ip;
                        if (val < 0) {
                            ParseError("encountered negative integer index (binary)");
                        }

                        //AI_SWAP4(val);
                        //outValue.push_back(val);
                        outValue.Add((uint)val);
                    }
                }

                return;
            }

            int dim = ParseTokenAsDim(tok[0]);

            // see notes in ParseVectorDataArray()
            outValue.Capacity = dim;//.reserve(dim);

            FBXScope scope = GetRequiredScope(el);
            FBXElement a = GetRequiredElement(scope, "a", el);

            foreach (var item in a.Tokens()) {
                int ival = ParseTokenAsInt(item);
                if (ival < 0) {
                    ParseError("encountered negative integer index");
                }
                //outValue.push_back(static_cast<uint>(ival));
                outValue.Add((uint)ival);
            }
        }

        // ------------------------------------------------------------------------------------------------
        // read an array of uint64_ts
        void ParseVectorDataArray(List<UInt64> outValue, FBXElement el) {
            outValue.Clear(); // .resize(0);
            List<FBXToken> tok = el.Tokens();
            if (tok.Count == 0) {
                ParseError("unexpected empty element", el);
            }

            if (tok[0].IsBinary()) {
                var data = tok[0].begin(); var end = tok[0].end();

                byte type;
                UInt32 count;
                ReadBinaryDataArrayHead(data, end, out type, out count, el);

                if (count == 0) {
                    return;
                }

                if (type != 'l') {
                    ParseError("expected long array (binary)", el);
                }

                byte[] buff;
                ReadBinaryDataArray(type, count, data, end, out buff, el);

                System.Diagnostics.Debug.Assert(data == end);
                var dataToRead = (count) * 8;
                if (dataToRead != buff.Length) {
                    ParseError("Invalid read size (binary)", el);
                }

                outValue.Capacity = (int)count; // .reserve(count);

                fixed (byte* pBuff = buff) {
                    var ip = (UInt64*)pBuff;
                    for (uint i = 0; i < count; ++i, ++ip) {
                        UInt64 val = *ip;
                        //AI_SWAP8(val);
                        //outValue.push_back(val);
                        outValue.Add(val);
                    }
                }

                return;
            }

            int dim = ParseTokenAsDim(tok[0]);

            // see notes in ParseVectorDataArray()
            outValue.Capacity = dim;//.reserve(dim);

            FBXScope scope = GetRequiredScope(el);
            FBXElement a = GetRequiredElement(scope, "a", el);

            foreach (var item in a.Tokens()) {
                UInt64 ival = ParseTokenAsID(item);

                //outValue.push_back(ival);
                outValue.Add(ival);
            }
        }

        // ------------------------------------------------------------------------------------------------
        // read an array of int64_ts
        void ParseVectorDataArray(List<Int64> outValue, FBXElement el) {
            outValue.Clear(); // .resize(0);
            List<FBXToken> tok = el.Tokens();
            if (tok.Count == 0) {
                ParseError("unexpected empty element", el);
            }

            if (tok[0].IsBinary()) {
                var data = tok[0].begin(); var end = tok[0].end();

                byte type;
                UInt32 count;
                ReadBinaryDataArrayHead(data, end, out type, out count, el);

                if (count == 0) {
                    return;
                }

                if (type != 'l') {
                    ParseError("expected long array (binary)", el);
                }

                byte[] buff;
                ReadBinaryDataArray(type, count, data, end, out buff, el);

                System.Diagnostics.Debug.Assert(data == end);
                var dataToRead = (count) * 8;
                if (dataToRead != buff.Length) {
                    ParseError("Invalid read size (binary)", el);
                }

                outValue.Capacity = (int)count; // .reserve(count);

                fixed (byte* pBuff = buff) {
                    var ip = (Int64*)pBuff;
                    for (uint i = 0; i < count; ++i, ++ip) {
                        Int64 val = *ip;
                        //AI_SWAP8(val);
                        //outValue.push_back(val);
                        outValue.Add(val);
                    }
                }

                return;
            }

            int dim = ParseTokenAsDim(tok[0]);

            // see notes in ParseVectorDataArray()
            outValue.Capacity = dim;//.reserve(dim);

            FBXScope scope = GetRequiredScope(el);
            FBXElement a = GetRequiredElement(scope, "a", el);

            foreach (var item in a.Tokens()) {
                Int64 ival = ParseTokenAsInt64(item);

                //outValue.push_back(ival);
                outValue.Add(ival);
            }
        }
        // ------------------------------------------------------------------------------------------------
        // extract a required element from a scope, abort if the element cannot be found
        FBXElement GetRequiredElement(FBXScope sc, string index, FBXElement? element = null) {
            FBXElement? el = sc[index];
            if (el == null) {
                ParseError("did not find required element \"" + index + "\"", element);
            }
            return el;
        }

        // ------------------------------------------------------------------------------------------------
        // extract required compound scope
        FBXScope GetRequiredScope(FBXElement el) {
            FBXScope s = el.Compound();
            if (!s) {
                ParseError("expected compound scope", el);
            }

            return *s;
        }

        // ------------------------------------------------------------------------------------------------
        // get token at a particular index
        FBXToken &GetRequiredToken(FBXElement el, uint index) {
            List<FBXToken> t = el.Tokens();
            if (index >= t.Count) {
                ParseError(Formatter::format("missing token at index ") << index, el);
            }

            return *t[index];
        }

        // ------------------------------------------------------------------------------------------------
        // wrapper around ParseTokenAsID() with ParseError handling
        UInt64 ParseTokenAsID(FBXToken t) {
            byte* err;
            UInt64 i = ParseTokenAsID(t, err);
            if (err != null) {
                ParseError(err, t);
            }
            return i;
        }

        // ------------------------------------------------------------------------------------------------
        // wrapper around ParseTokenAsDim() with ParseError handling
        int ParseTokenAsDim(FBXToken t) {
            byte* err;
            int i = ParseTokenAsDim(t, err);
            if (err != null) {
                ParseError(err, t);
            }
            return i;
        }

        // ------------------------------------------------------------------------------------------------
        // wrapper around ParseTokenAsFloat() with ParseError handling
        float ParseTokenAsFloat(FBXToken t) {
            string err;
            float i = ParseTokenAsFloat(t, out err);
            if (err != "") {
                ParseError(err, t);
            }
            return i;
        }

        // ------------------------------------------------------------------------------------------------
        // wrapper around ParseTokenAsInt() with ParseError handling
        int ParseTokenAsInt(FBXToken t) {
            char* err;
            int i = ParseTokenAsInt(t, err);
            if (err) {
                ParseError(err, t);
            }
            return i;
        }

        // ------------------------------------------------------------------------------------------------
        // wrapper around ParseTokenAsInt64() with ParseError handling
        Int64 ParseTokenAsInt64(FBXToken t) {
            char* err;
            Int64 i = ParseTokenAsInt64(t, err);
            if (err) {
                ParseError(err, t);
            }
            return i;
        }
        // ------------------------------------------------------------------------------------------------
        int ParseTokenAsDim(FBXToken t, out string err_out) {
            // same as ID parsing, except there is a trailing asterisk
            err_out = "";

            if (t.Type() != TokenType.TokenType_DATA) {
                err_out = "expected TOK_DATA token";
                return 0;
            }

            if (t.IsBinary()) {
                var data = t.begin();
                if (data[0] != 'L') {
                    err_out = "failed to parse ID, unexpected data type, expected L(ong) (binary)";
                    return 0;
                }

                UInt64 id = Utility.SafeParse<UInt64>(data + 1, t.end());
                //AI_SWAP8(id);
                return (int)id;
            }

            if (*t.begin() != '*') {
                err_out = "expected asterisk before array dimension";
                return 0;
            }

            // XXX: should use int here
            uint length = (uint)(t.end() - t.begin());
            if (length == 0) {
                err_out = "expected valid integer number after asterisk";
                return 0;
            }

            char* outValue = null;
            int id = (int)(Import3D.Utility.strtoul10_64(t.begin() + 1, outValue, &length));
            if (outValue > t.end()) {
                err_out = "failed to parse ID";
                return 0;
            }

            return id;
        }

        // ------------------------------------------------------------------------------------------------
        float ParseTokenAsFloat(FBXToken t, out string err_out) {
            err_out = "";

            if (t.Type() != TokenType.TokenType_DATA) {
                err_out = "expected TOK_DATA token";
                return 0.0f;
            }

            if (t.IsBinary()) {
                var data = t.begin();
                if (data[0] != 'F' && data[0] != 'D') {
                    err_out = "failed to parse F(loat) or D(ouble), unexpected data type (binary)";
                    return 0.0f;
                }

                if (data[0] == 'F') {
                    return Utility.SafeParse<float>(data + 1, t.end());
                }
                else {
                    return (float)(Utility.SafeParse<double>(data + 1, t.end()));
                }
            }

            // need to copy the input string to a temporary buffer
            // first - next in the fbx token stream comes ',',
            // which fast_atof could interpret as decimal point.
#define MAX_FLOAT_LENGTH 31
            int length = static_cast<int>(t.end() - t.begin());
            if (length > MAX_FLOAT_LENGTH) {
                return 0.f;
            }

            char temp[MAX_FLOAT_LENGTH + 1];
            std::copy(t.begin(), t.end(), temp);
            temp[std::min(static_cast<int>(MAX_FLOAT_LENGTH), length)] = '\0';

            return fast_atof(temp);
        }

        // ------------------------------------------------------------------------------------------------
        int ParseTokenAsInt(FBXToken t, char*&err_out) {
            err_out = null;

            if (t.Type() != TokenType_DATA) {
                err_out = "expected TOK_DATA token";
                return 0;
            }

            if (t.IsBinary()) {
                char* data = t.begin();
                if (data[0] != 'I') {
                    err_out = "failed to parse I(nt), unexpected data type (binary)";
                    return 0;
                }

                int32_t ival = Utility.SafeParse<int32_t>(data + 1, t.end());
                //AI_SWAP4(ival);
                return static_cast<int>(ival);
            }

            System.Diagnostics.Debug.Assert(static_cast<int>(t.end() - t.begin()) > 0);

            char* outValue;
            int intval = strtol10(t.begin(), &outValue);
            if (outValue != t.end()) {
                err_out = "failed to parse ID";
                return 0;
            }

            return intval;
        }

        // ------------------------------------------------------------------------------------------------
        Int64 ParseTokenAsInt64(FBXToken t, char*&err_out) {
            err_out = null;

            if (t.Type() != TokenType_DATA) {
                err_out = "expected TOK_DATA token";
                return 0L;
            }

            if (t.IsBinary()) {
                char* data = t.begin();
                if (data[0] != 'L') {
                    err_out = "failed to parse Int64, unexpected data type";
                    return 0L;
                }

                Int64 id = Utility.SafeParse<Int64>(data + 1, t.end());
                //AI_SWAP8(id);
                return id;
            }

            // XXX: should use int here
            uint length = static_cast<uint>(t.end() - t.begin());
            System.Diagnostics.Debug.Assert(length > 0);

            char* outValue = null;
            Int64 id = strtol10_64(t.begin(), outValue, &length);
            if (outValue > t.end()) {
                err_out = "failed to parse Int64 (text)";
                return 0L;
            }

            return id;
        }

        // ------------------------------------------------------------------------------------------------
        string ParseTokenAsString(FBXToken t, char*&err_out) {
            err_out = null;

            if (t.Type() != TokenType_DATA) {
                err_out = "expected TOK_DATA token";
                return string();
            }

            if (t.IsBinary()) {
                char* data = t.begin();
                if (data[0] != 'S') {
                    err_out = "failed to parse S(tring), unexpected data type (binary)";
                    return string();
                }

                // read string length
                int32_t len = Utility.SafeParse<int32_t>(data + 1, t.end());
                //AI_SWAP4(len);

                System.Diagnostics.Debug.Assert(t.end() - data == 5 + len);
                return string(data + 5, len);
            }

            int length = static_cast<int>(t.end() - t.begin());
            if (length < 2) {
                err_out = "token is too short to hold a string";
                return string();
            }

            char* s = t.begin(), *e = t.end() - 1;
            if (*s != '\"' || *e != '\"') {
                err_out = "expected double quoted string";
                return string();
            }

            return string(s + 1, length - 2);
        }

    }
}
