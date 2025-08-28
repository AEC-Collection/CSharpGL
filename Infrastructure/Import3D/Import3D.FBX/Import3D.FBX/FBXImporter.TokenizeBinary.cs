using System.Diagnostics;

namespace Import3D.FBX {
	partial class FBXImporter {
		/** Tokenizer function for binary FBX files.
 *
 *  Emits a token list suitable for direct parsing.
 *
 * @param output_tokens Receives a list of all tokens in the input data.
 * @param input_buffer Binary input buffer to be processed.
 * @param length Length of input buffer, in bytes. There is no 0-terminal.
 * @throw DeadlyImportError if something goes wrong */

		private static unsafe List<FBXToken> TokenizeBinary(byte* input, int length) {
			if (length < 0x1b) {
				TokenizeError("file is too short", 0);
			}

			if (Import3D.Utility.strncmp(input, magicHeader) != 0) {
				TokenizeError("magic bytes not found", 0);
			}
			var cursor = input + 18;
			/*Result ignored*/
			ReadByte(input, ref cursor, input + length);
			/*Result ignored*/
			ReadByte(input, ref cursor, input + length);
			/*Result ignored*/
			ReadByte(input, ref cursor, input + length);
			/*Result ignored*/
			ReadByte(input, ref cursor, input + length);
			/*Result ignored*/
			ReadByte(input, ref cursor, input + length);
			UInt32 version = ReadWord(input, ref cursor, input + length);
			Log.WriteLine($"FBX version: {version}");
			bool is64bits = version >= 7500;
			var end = input + length;
			var output_tokens = new List<FBXToken>();
			try {
				while (cursor < end) {
					if (!ReadScope(output_tokens, input, ref cursor, input + length, is64bits)) {
						break;
					}
				}
			}
			catch (Exception e) {
				if (!is64bits && (length > UInt32.MaxValue)) {
					throw new Exception($"The FBX file is invalid. This may be because the content is too big for this older version ({version} ) of the FBX format. ({e})");
				}
				else { throw e; }
			}
			return output_tokens;
		}
		// ------------------------------------------------------------------------------------------------
		private static unsafe byte ReadByte(byte* input, ref byte* cursor, byte* end) {
			if (Offset(cursor, end) < sizeof(byte)) {
				TokenizeError("cannot ReadByte, out of bounds", input, cursor);
			}

			byte word = *cursor;/* = *reinterpret_cast<  byte* >( cursor )*/
			++cursor;

			return word;
		}
		// ------------------------------------------------------------------------------------------------
		private static unsafe UInt32 ReadWord(byte* input, ref byte* cursor, byte* end) {
			int k_to_read = sizeof(UInt32);
			if (Offset(cursor, end) < k_to_read) {
				TokenizeError("cannot ReadWord, out of bounds", input, cursor);
			}

			UInt32 word = *((UInt32*)cursor);
			////AI_SWAP4(word);

			cursor += k_to_read;

			return word;
		}

		// ------------------------------------------------------------------------------------------------
		private static unsafe UInt64 ReadDoubleWord(byte* input, ref byte* cursor, byte* end) {
			int k_to_read = sizeof(UInt64);
			if (Offset(cursor, end) < k_to_read) {
				TokenizeError("cannot ReadDoubleWord, out of bounds", input, cursor);
			}

			UInt64 dword = *((UInt64*)cursor); /*= *reinterpret_cast< UInt64*>(cursor)*/;
			////AI_SWAP8(dword);

			cursor += k_to_read;

			return dword;
		}

		// ------------------------------------------------------------------------------------------------
		private static unsafe uint ReadString(out byte* sbegin_out, out byte* send_out,
			byte* input, ref byte* cursor, byte* end,
			bool long_length = false, bool allow_null = false) {
			var len_len = long_length ? 4 : 1;
			if (Offset(cursor, end) < len_len) {
				TokenizeError("cannot ReadString, out of bounds reading length", input, cursor);
			}

			UInt32 length = long_length ? ReadWord(input, ref cursor, end) : ReadByte(input, ref cursor, end);

			if (Offset(cursor, end) < length) {
				TokenizeError("cannot ReadString, length is out of bounds", input, cursor);
			}

			sbegin_out = cursor;
			cursor += length;

			send_out = cursor;

			if (!allow_null) {
				for (var i = 0; i < length; ++i) {
					if (sbegin_out[i] == '\0') {
						TokenizeError("failed ReadString, unexpected NUL character in string", input, cursor);
					}
				}
			}

			return length;
		}

		// ------------------------------------------------------------------------------------------------
		private static unsafe void ReadData(out byte* sbegin_out, out byte* send_out,
			byte* input, ref byte* cursor, byte* end) {
			if (Offset(cursor, end) < 1) {
				TokenizeError("cannot ReadData, out of bounds reading length", input, cursor);
			}

			var type = *cursor;
			sbegin_out = cursor++;

			switch ((char)type) {
			// 16 bit int
			case 'Y':
			cursor += 2;
			break;

			// 1 bit bool flag (yes/no)
			case 'C':
			cursor += 1;
			break;

			// 32 bit int
			case 'I':
			// <- fall through

			// float
			case 'F':
			cursor += 4;
			break;

			// double
			case 'D':
			cursor += 8;
			break;

			// 64 bit int
			case 'L':
			cursor += 8;
			break;

			// note: do not write cursor += ReadWord(...cursor) as this would be UB

			// raw binary data
			case 'R': {
				UInt32 length = ReadWord(input, ref cursor, end);
				cursor += length;
				break;
			}

			case 'b':
			// TODO: what is the 'b' type code? Right now we just skip over it /
			// take the full range we could get
			cursor = end;
			break;

			// array of *
			case 'f':
			case 'd':
			case 'l':
			case 'i':
			case 'c': {
				UInt32 length = ReadWord(input, ref cursor, end);
				UInt32 encoding = ReadWord(input, ref cursor, end);

				UInt32 comp_len = ReadWord(input, ref cursor, end);

				// compute length based on type and check against the stored value
				if (encoding == 0) {
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

					case 'c':
					stride = 1;
					break;

					default:
					Debug.Assert(false);
					break;
					}
					;
					Debug.Assert(stride > 0);
					if (length * stride != comp_len) {
						TokenizeError("cannot ReadData, calculated data stride differs from what the file claims", input, cursor);
					}
				}
				// zip/deflate algorithm (encoding==1)? take given length. anything else? die
				else if (encoding != 1) {
					TokenizeError("cannot ReadData, unknown encoding", input, cursor);
				}
				cursor += comp_len;
				break;
			}

			// string
			case 'S': {
				byte* sb, se;
				// 0 characters can legally happen in such strings
				ReadString(out sb, out se, input, ref cursor, end, true, true);
				break;
			}
			default:
			TokenizeError($"cannot ReadData, unexpected type code: {(char)type}", input, cursor);
			break;
			}

			if (cursor > end) {
				TokenizeError($"cannot ReadData, the remaining size is too small for the data type: {(char)type}", input, cursor);
			}

			// the type code is contained in the returned range
			send_out = cursor;
		}


		// ------------------------------------------------------------------------------------------------
		private static unsafe bool ReadScope(List<FBXToken> output_tokens, byte* input, ref byte* cursor, byte* end, bool is64bits) {
			// the first word contains the offset at which this block ends
			UInt64 end_offset = is64bits ? ReadDoubleWord(input, ref cursor, end) : ReadWord(input, ref cursor, end);

			// we may get 0 if reading reached the end of the file -
			// fbx files have a mysterious extra footer which I don't know
			// how to extract any information from, but at least it always
			// starts with a 0.
			if (end_offset == 0) { return false; }

			if (end_offset > (ulong)Offset(input, end)) {
				TokenizeError("block offset is out of range", input, cursor);
			}
			else if (end_offset < (ulong)Offset(input, cursor)) {
				TokenizeError("block offset is negative out of range", input, cursor);
			}

			// the second data word contains the number of properties in the scope
			UInt64 prop_count = is64bits ? ReadDoubleWord(input, ref cursor, end) : ReadWord(input, ref cursor, end);

			// the third data word contains the length of the property list
			UInt64 prop_length = is64bits ? ReadDoubleWord(input, ref cursor, end) : ReadWord(input, ref cursor, end);

			// now comes the name of the scope/key
			byte* sbeg, send;
			ReadString(out sbeg, out send, input, ref cursor, end);

			{
				var token = new FBXToken(sbeg, send, TokenType.TokenType_KEY, Offset(input, cursor));
				output_tokens.Add(token);
			}

			// now come the individual properties
			var begin_cursor = cursor;

			if ((begin_cursor + prop_length) > end) {
				TokenizeError("property length out of bounds reading length ", input, cursor);
			}

			for (ulong i = 0; i < prop_count; ++i) {
				ReadData(out sbeg, out send, input, ref cursor, begin_cursor + prop_length);

				{
					var token = new FBXToken(sbeg, send, TokenType.TokenType_DATA, Offset(input, cursor));
					output_tokens.Add(token);
				}

				if (i != prop_count - 1) {
					var token = new FBXToken(cursor, cursor + 1, TokenType.TokenType_COMMA, Offset(input, cursor));
					output_tokens.Add(token);
				}
			}

			if ((ulong)Offset(begin_cursor, cursor) != prop_length) {
				TokenizeError("property length not reached, something is wrong", input, cursor);
			}

			// at the end of each nested block, there is a NUL record to indicate
			// that the sub-scope exists (i.e. to distinguish between P: and P : {})
			// this NUL record is 13 bytes long on 32 bit version and 25 bytes long on 64 bit.
			var sentinel_block_length = is64bits ? (sizeof(UInt64) * 3 + 1) : (sizeof(UInt32) * 3 + 1);

			if ((ulong)Offset(input, cursor) < end_offset) {
				if (end_offset - (ulong)Offset(input, cursor) < (ulong)sentinel_block_length) {
					TokenizeError("insufficient padding bytes at block end", input, cursor);
				}

				{
					var token = new FBXToken(cursor, cursor + 1, TokenType.TokenType_OPEN_BRACKET, Offset(input, cursor));
					output_tokens.Add(token);
				}

				// XXX this is vulnerable to stack overflowing ..
				while ((ulong)Offset(input, cursor) < (end_offset - (ulong)sentinel_block_length)) {
					ReadScope(output_tokens, input, ref cursor, input + end_offset - sentinel_block_length, is64bits);
				}
				{
					var token = new FBXToken(cursor, cursor + 1, TokenType.TokenType_CLOSE_BRACKET, Offset(input, cursor));
					output_tokens.Add(token);
				}

				for (var i = 0; i < sentinel_block_length; ++i) {
					if (cursor[i] != '\0') {
						TokenizeError("failed to read nested block sentinel, expected all bytes to be 0", input, cursor);
					}
				}
				cursor += sentinel_block_length;
			}

			if ((ulong)Offset(input, cursor) != end_offset) {
				TokenizeError("scope length not reached, something is wrong", input, cursor);
			}

			return true;
		}

		// ------------------------------------------------------------------------------------------------
		private static unsafe long Offset(byte* begin, byte* cursor) {
			Debug.Assert(begin <= cursor);

			return cursor - begin;
		}
	}
}
