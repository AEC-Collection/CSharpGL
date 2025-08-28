using System.Diagnostics;

namespace Import3D.FBX {
    partial class FBXImporter {
        /** Main FBX tokenizer function. Transform input buffer into a list of preprocessed tokens.
         *
         *  Skips over comments and generates line and column numbers.
         *
         * @param output_tokens Receives a list of all tokens in the input data.
         * @param input_buffer Textual input buffer to be processed, 0-terminated.
         * @throw DeadlyImportError if something goes wrong */

        private static unsafe List<FBXToken> Tokenize(byte* input, int length) {
            Debug.Assert(input != null);
            Log.WriteLine("Tokenizing ASCII FBX file");

            // line and column numbers numbers are one-based
            int line = 1;
            int column = 1;

            bool comment = false;
            bool in_double_quotes = false;
            bool pending_data_token = false;

            byte* token_begin = null, token_end = null;
            var output_tokens = new List<FBXToken>();
            for (var cur = input; *cur != '\0'; column += (*cur == '\t' ? /*ASSIMP_FBX_TAB_WIDTH*/4 : 1), ++cur) {
                var c = (char)(*cur);
                if (IsLineEnd(c)) {
                    comment = false;

                    column = 0;
                    ++line;
                }

                if (comment) {
                    continue;
                }

                if (in_double_quotes) {
                    if (c == '\"') {
                        in_double_quotes = false;
                        token_end = cur;

                        ProcessDataToken(output_tokens, ref token_begin, ref token_end, line, column);
                        pending_data_token = false;
                    }
                    continue;
                }

                switch (c) {
                case '\"':
                if (token_begin != null) {
                    TokenizeError("unexpected double-quote", line, column);
                }
                token_begin = cur;
                in_double_quotes = true;
                continue;

                case ';':
                ProcessDataToken(output_tokens, ref token_begin, ref token_end, line, column);
                comment = true;
                continue;

                case '{':
                ProcessDataToken(output_tokens, ref token_begin, ref token_end, line, column); {
                    var token = new FBXToken(cur, cur + 1, TokenType.TokenType_OPEN_BRACKET, line, (uint)column);
                    output_tokens.Add(token);
                }
                continue;

                case '}':
                ProcessDataToken(output_tokens, ref token_begin, ref token_end, line, column); {
                    var token = new FBXToken(cur, cur + 1, TokenType.TokenType_CLOSE_BRACKET, line, (uint)column);
                    output_tokens.Add(token);
                }
                continue;

                case ',':
                if (pending_data_token) {
                    ProcessDataToken(output_tokens, ref token_begin, ref token_end, line, column, TokenType.TokenType_DATA, true);
                } {
                    var token = new FBXToken(cur, cur + 1, TokenType.TokenType_COMMA, line, (uint)column);
                    output_tokens.Add(token);
                }
                continue;

                case ':':
                if (pending_data_token) {
                    ProcessDataToken(output_tokens, ref token_begin, ref token_end, line, column, TokenType.TokenType_KEY, true);
                }
                else {
                    TokenizeError("unexpected colon", line, column);
                }
                continue;
                }

                if (IsSpaceOrNewLine(c)) {
                    if (token_begin != null) {
                        // peek ahead and check if the next token is a colon in which
                        // case this counts as KEY token.
                        var type = TokenType.TokenType_DATA;
                        for (var peek = cur; *peek != '\0' && IsSpaceOrNewLine((char)(*peek)); ++peek) {
                            if (*peek == ':') {
                                type = TokenType.TokenType_KEY;
                                cur = peek;
                                break;
                            }
                        }

                        ProcessDataToken(output_tokens, ref token_begin, ref token_end, line, column, type);
                    }



                    pending_data_token = false;
                }
                else {
                    token_end = cur;
                    if (token_begin == null) {
                        token_begin = cur;
                    }

                    pending_data_token = true;
                }
            }

            return output_tokens;
        }

        private static unsafe void ProcessDataToken(
            List<FBXToken> output_tokens,
            ref byte* start, ref byte* end,
            int line, int column,
            TokenType type = TokenType.TokenType_DATA,
            bool must_have_token = false) {
            if (start != null && end != null) {
                // sanity check:
                // tokens should have no whitespace outside quoted text and [start,end] should
                // properly delimit the valid range.
                bool in_double_quotes = false;
                for (var c = start; c != end + 1; ++c) {
                    if (*c == '\"') {
                        in_double_quotes = !in_double_quotes;
                    }

                    if (!in_double_quotes && IsSpaceOrNewLine((char)(*c))) {
                        TokenizeError("unexpected whitespace in token", line, column);
                    }
                }

                if (in_double_quotes) {
                    TokenizeError("non-terminated double quotes", line, column);
                }

                {
                    var token = new FBXToken(start, end + 1, type, line, (uint)column);
                    output_tokens.Add(token);
                }
            }
            else if (must_have_token) {
                TokenizeError("unexpected character, expected data token", line, column);
            }

            start = null; end = null;

        }
    }
}
