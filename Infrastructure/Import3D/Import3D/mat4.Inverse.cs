using System.Text;

namespace Import3D {
    unsafe partial struct mat4 {
        public mat4 Inverse() {
            float Coef00 = this.values[2 * 4 + 2] * this.values[3 * 4 + 3] - this.values[3 * 4 + 2] * this.values[2 * 4 + 3];
            float Coef02 = this.values[1 * 4 + 2] * this.values[3 * 4 + 3] - this.values[3 * 4 + 2] * this.values[1 * 4 + 3];
            float Coef03 = this.values[1 * 4 + 2] * this.values[2 * 4 + 3] - this.values[2 * 4 + 2] * this.values[1 * 4 + 3];

            float Coef04 = this.values[2 * 4 + 1] * this.values[3 * 4 + 3] - this.values[3 * 4 + 1] * this.values[2 * 4 + 3];
            float Coef06 = this.values[1 * 4 + 1] * this.values[3 * 4 + 3] - this.values[3 * 4 + 1] * this.values[1 * 4 + 3];
            float Coef07 = this.values[1 * 4 + 1] * this.values[2 * 4 + 3] - this.values[2 * 4 + 1] * this.values[1 * 4 + 3];

            float Coef08 = this.values[2 * 4 + 1] * this.values[3 * 4 + 2] - this.values[3 * 4 + 1] * this.values[2 * 4 + 2];
            float Coef10 = this.values[1 * 4 + 1] * this.values[3 * 4 + 2] - this.values[3 * 4 + 1] * this.values[1 * 4 + 2];
            float Coef11 = this.values[1 * 4 + 1] * this.values[2 * 4 + 2] - this.values[2 * 4 + 1] * this.values[1 * 4 + 2];

            float Coef12 = this.values[2 * 4 + 0] * this.values[3 * 4 + 3] - this.values[3 * 4 + 0] * this.values[2 * 4 + 3];
            float Coef14 = this.values[1 * 4 + 0] * this.values[3 * 4 + 3] - this.values[3 * 4 + 0] * this.values[1 * 4 + 3];
            float Coef15 = this.values[1 * 4 + 0] * this.values[2 * 4 + 3] - this.values[2 * 4 + 0] * this.values[1 * 4 + 3];

            float Coef16 = this.values[2 * 4 + 0] * this.values[3 * 4 + 2] - this.values[3 * 4 + 0] * this.values[2 * 4 + 2];
            float Coef18 = this.values[1 * 4 + 0] * this.values[3 * 4 + 2] - this.values[3 * 4 + 0] * this.values[1 * 4 + 2];
            float Coef19 = this.values[1 * 4 + 0] * this.values[2 * 4 + 2] - this.values[2 * 4 + 0] * this.values[1 * 4 + 2];

            float Coef20 = this.values[2 * 4 + 0] * this.values[3 * 4 + 1] - this.values[3 * 4 + 0] * this.values[2 * 4 + 1];
            float Coef22 = this.values[1 * 4 + 0] * this.values[3 * 4 + 1] - this.values[3 * 4 + 0] * this.values[1 * 4 + 1];
            float Coef23 = this.values[1 * 4 + 0] * this.values[2 * 4 + 1] - this.values[2 * 4 + 0] * this.values[1 * 4 + 1];

            vec4 Fac0 = new vec4(Coef00, Coef00, Coef02, Coef03);
            vec4 Fac1 = new vec4(Coef04, Coef04, Coef06, Coef07);
            vec4 Fac2 = new vec4(Coef08, Coef08, Coef10, Coef11);
            vec4 Fac3 = new vec4(Coef12, Coef12, Coef14, Coef15);
            vec4 Fac4 = new vec4(Coef16, Coef16, Coef18, Coef19);
            vec4 Fac5 = new vec4(Coef20, Coef20, Coef22, Coef23);

            vec4 Vec0 = new vec4(this.values[1 * 4 + 0], this.values[0 * 4 + 0], this.values[0 * 4 + 0], this.values[0 * 4 + 0]);
            vec4 Vec1 = new vec4(this.values[1 * 4 + 1], this.values[0 * 4 + 1], this.values[0 * 4 + 1], this.values[0 * 4 + 1]);
            vec4 Vec2 = new vec4(this.values[1 * 4 + 2], this.values[0 * 4 + 2], this.values[0 * 4 + 2], this.values[0 * 4 + 2]);
            vec4 Vec3 = new vec4(this.values[1 * 4 + 3], this.values[0 * 4 + 3], this.values[0 * 4 + 3], this.values[0 * 4 + 3]);

            vec4 Inv0 = Vec1 * Fac0 - Vec2 * Fac1 + Vec3 * Fac2;
            vec4 Inv1 = Vec0 * Fac0 - Vec2 * Fac3 + Vec3 * Fac4;
            vec4 Inv2 = Vec0 * Fac1 - Vec1 * Fac3 + Vec3 * Fac5;
            vec4 Inv3 = Vec0 * Fac2 - Vec1 * Fac4 + Vec2 * Fac5;

            vec4 SignA = new vec4(+1, -1, +1, -1);
            vec4 SignB = new vec4(-1, +1, -1, +1);
            mat4 Inverse = new mat4(Inv0 * SignA, Inv1 * SignB, Inv2 * SignA, Inv3 * SignB);

            vec4 Row0 = new vec4(Inverse.values[0 * 4 + 0], Inverse.values[1 * 4 + 0], Inverse.values[2 * 4 + 0], Inverse.values[3 * 4 + 0]);

            vec4 Dot0 = new vec4(
                this.values[0] * Row0.x, this.values[1] * Row0.y,
                this.values[2] * Row0.z, this.values[3] * Row0.w);
            float Dot1 = (Dot0.x + Dot0.y) + (Dot0.z + Dot0.w);

            //var result = new mat4();
            float OneOverDeterminant = (1f) / Dot1;
            for (int i = 0; i < 16; i++) {
                Inverse.values[i] = Inverse.values[i] * OneOverDeterminant;
            }

            return Inverse;
        }
    }
}

