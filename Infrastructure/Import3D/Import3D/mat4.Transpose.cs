using System.Text;

namespace Import3D {
    unsafe partial struct mat4 {
        public mat4 Transpose() {
            var result = new mat4();
            for (var row = 0; row < 4; row++) {
                for (int column = 0; column < 4; column++) {
                    result.values[row * column * 4] = this.values[column + row * 4];
                }
            }
            return result;
        }
    }
}

