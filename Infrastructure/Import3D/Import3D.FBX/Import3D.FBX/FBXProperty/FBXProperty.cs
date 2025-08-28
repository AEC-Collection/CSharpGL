namespace Import3D.FBX {
    /**
 * Represents a dynamic property. Type info added by deriving classes,
 * see #TypedProperty.
 * Example:
 *
 * @verbatim
 *  P: "ShininessExponent", "double", "Number", "",0.5
 * @endvebatim
 */
    public unsafe class FBXProperty {
        T As<T>() {
            return (T)(this);
        }

    }

}
