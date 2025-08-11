namespace Import3D.STL {
    public unsafe partial class STLImporter {
        class AsciiContext {
            public readonly string filename;
            public readonly IReadOnlyList<string> lines;
            public readonly aiScene scene;

            //public string? NextToken() {
            //    if (this.nextTokenId < this.tokens.Length) {
            //        var token = this.tokens[this.nextTokenId];
            //        this.nextTokenId++;
            //        return token;
            //    }
            //    else { return null; }
            //}

            //public string? Peek() {
            //    if (this.nextTokenId < this.tokens.Length) {
            //        var token = this.tokens[this.nextTokenId];
            //        return token;
            //    }
            //    else { return null; }
            //}
            //private int nextTokenId = 0;

            public string? NextLine() {
                if (this.nextLineId < this.lines.Count) {
                    var line = this.lines[this.nextLineId];
                    this.nextLineId++;
                    return line;
                }
                else { return null; }
            }
            private int nextLineId = 0;

            public string? Peek() {
                if (this.nextLineId < this.lines.Count) {
                    var line = this.lines[this.nextLineId];
                    return line;
                }
                else { return null; }
            }

            public AsciiContext(string filename, aiScene scene) {
                this.filename = filename;
                var lines = new List<string>();
                using (var reader = new StreamReader(filename)) {
                    while (!reader.EndOfStream) {
                        var line = reader.ReadLine();
                        if (line != null) {
                            line = line.Trim();
                            lines.Add(line);
                        }
                    }
                }
                this.lines = lines;
                this.scene = scene;
                // allocate a single node
                scene.mRootNode = new aiNode("STL root node");
            }
        }
    }
}
