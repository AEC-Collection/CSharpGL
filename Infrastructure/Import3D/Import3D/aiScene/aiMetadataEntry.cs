namespace Import3D {
    /**
  * Metadata entry
  *
  * The type field uniquely identifies the underlying type of the data field
  */

    public unsafe class aiMetadataEntry {
        public aiMetadataType mType;
        public object mData;

        //# ifdef __cplusplus
        public aiMetadataEntry()
            //:
            //mType(AI_META_MAX),
            //mData(null )
            {
            // empty
        }
        //#endif

    }
    /**
  * Enum used to distinguish data types
  */
    public enum aiMetadataType {
        AI_BOOL = 0,
        AI_INT32 = 1,
        AI_UINT64 = 2,
        AI_FLOAT = 3,
        AI_DOUBLE = 4,
        AI_AISTRING = 5,
        AI_AIVECTOR3D = 6,
        AI_AIMETADATA = 7,
        AI_INT64 = 8,
        AI_UINT32 = 9,
        AI_META_MAX = 10,

        //# ifndef SWIG
        //FORCE_32BIT = INT_MAX
        //#endif
    }
    //aiMetadataType;

}