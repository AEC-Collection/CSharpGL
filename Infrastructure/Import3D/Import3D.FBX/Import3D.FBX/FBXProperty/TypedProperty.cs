namespace Import3D.FBX {
    public unsafe class TypedProperty<T> : FBXProperty {
        public TypedProperty(T value) { this.value = value; }

        T Value() {
            return value;
        }

        T value;
    }

}
