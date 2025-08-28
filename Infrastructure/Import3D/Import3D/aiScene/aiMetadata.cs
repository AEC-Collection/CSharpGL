using System;

namespace Import3D {

    /**
  * Container for holding metadata.
  *
  * Metadata is a key-value store using string keys and values.
  */

    public unsafe class aiMetadata {
        /** Length of the mKeys and mValues arrays, respectively */
        public uint mNumProperties;

        /** Arrays of keys, may not be NULL. Entries in this array may not be NULL as well. */
        public string[] mKeys;

        /** Arrays of values, may not be NULL. Entries in this array may be NULL if the
          * corresponding property key has no assigned value. */
        public aiMetadataEntry[] mValues;

        //# ifdef __cplusplus

        /**
         *  @brief  The default constructor, set all members to zero by default.
         */
        public aiMetadata()
            //: mNumProperties(0),
            //  mKeys(null),
            //  mValues(null)
            {
            // empty
        }

        public aiMetadata(aiMetadata rhs)
            //:
            //mNumProperties(rhs.mNumProperties), mKeys(null), mValues(null) 
            {
            this.mNumProperties = rhs.mNumProperties;

            throw new NotImplementedException();
            //mKeys = new string[mNumProperties];
            //for (int i = 0; i < mNumProperties; ++i) {
            //    mKeys[i] = rhs.mKeys[i];
            //}
            //mValues = new aiMetadataEntry[mNumProperties];
            //for (int i = 0; i < mNumProperties; ++i) {
            //    mValues[i].mType = rhs.mValues[i].mType;
            //    switch (rhs.mValues[i].mType) {
            //    case aiMetadataType.AI_BOOL:
            //    mValues[i].mData = new bool;
            //    //todo: ::memcpy(mValues[i].mData, rhs.mValues[i].mData, sizeof(bool));
            //    break;
            //    case aiMetadataType.AI_INT32: {
            //        Int32 v = 0;
            //        //todo: ::memcpy(&v, rhs.mValues[i].mData, sizeof(Int32));
            //        mValues[i].mData = v;
            //    }
            //    break;
            //    case aiMetadataType.AI_UINT64: {
            //        UInt64 v = 0;
            //        //todo: ::memcpy(&v, rhs.mValues[i].mData, sizeof(UInt64));
            //        mValues[i].mData = v;
            //    }
            //    break;
            //    case aiMetadataType.AI_FLOAT: {
            //        float v = 0;
            //        //todo: ::memcpy(&v, rhs.mValues[i].mData, sizeof(float));
            //        mValues[i].mData = v;
            //    }
            //    break;
            //    case aiMetadataType.AI_DOUBLE: {
            //        double v = 0;
            //        //todo: ::memcpy(&v, rhs.mValues[i].mData, sizeof(double));
            //        mValues[i].mData = v;
            //    }
            //    break;
            //    case aiMetadataType.AI_AISTRING: {
            //        string v = "";
            //        rhs.Get<string>(i, v);
            //        mValues[i].mData = v;
            //    }
            //    break;
            //    case aiMetadataType.AI_AIVECTOR3D: {
            //        vec3 v;
            //        rhs.Get<vec3>(i, v);
            //        mValues[i].mData = v;
            //    }
            //    break;
            //    case aiMetadataType.AI_AIMETADATA: {
            //        aiMetadata v = null;
            //        rhs.Get<aiMetadata>(i, v);
            //        mValues[i].mData = v;
            //    }
            //    break;
            //    case aiMetadataType.AI_INT64: {
            //        Int64 v = 0;
            //        //todo: ::memcpy(&v, rhs.mValues[i].mData, sizeof(Int64));
            //        mValues[i].mData = v;
            //    }
            //    break;
            //    case aiMetadataType.AI_UINT32: {
            //        UInt32 v0;
            //        //todo: ::memcpy(&v, rhs.mValues[i].mData, sizeof(UInt32));
            //        mValues[i].mData = v;
            //    }
            //    break;
            //    //# ifndef SWIG
            //    //case aiMetadataType.FORCE_32BIT:
            //    //break;
            //    //#endif
            //    default:
            //    break;
            //    }
            //}
        }

        //aiMetadata &operator=(aiMetadata rhs) {
        //swap(mNumProperties, rhs.mNumProperties);
        //swap(mKeys, rhs.mKeys);
        //swap(mValues, rhs.mValues);

        //return *this;
        //}

        //    /**
        //     *  @brief The destructor.
        //     */
        //    ~aiMetadata() {
        //            delete[] mKeys;
        //            mKeys = null;
        //            if (mValues) {
        //                // Delete each metadata entry
        //                for (unsigned i = 0; i < mNumProperties; ++i) {
        //                    void* data = mValues[i].mData;
        //                    switch (mValues[i].mType) {
        //                    case AI_BOOL:
        //                    delete static_cast<bool*> (data);
        //                    break;
        //                    case AI_INT32:
        //                    delete static_cast<Int32 *> (data);
        //                    break;
        //                    case AI_UINT64:
        //                    delete static_cast<UInt64 *> (data);
        //                    break;
        //                    case AI_FLOAT:
        //                    delete static_cast<float*> (data);
        //                    break;
        //                    case AI_DOUBLE:
        //                    delete static_cast<double*> (data);
        //                    break;
        //                    case AI_AISTRING:
        //                    delete static_cast<string*> (data);
        //                    break;
        //                    case AI_AIVECTOR3D:
        //                    delete static_cast<vec3 *> (data);
        //                    break;
        //                    case AI_AIMETADATA:
        //                    delete static_cast<aiMetadata *> (data);
        //                    break;
        //                    case AI_INT64:
        //                    delete static_cast<Int64 *> (data);
        //                    break;
        //                    case AI_UINT32:
        //                    delete static_cast<UInt32 *> (data);
        //                    break;
        //# ifndef SWIG
        //                    case FORCE_32BIT:
        //#endif
        //                    default:
        //                    break;
        //                    }
        //                }

        //                // Delete the metadata array
        //                delete[] mValues;
        //                mValues = null;
        //            }
        //        }

        /**
         *  @brief Allocates property fields + keys.
         *  @param  numProperties   Number of requested properties.
         */
        static aiMetadata Alloc(uint numProperties) {
            if (0 == numProperties) {
                return null;
            }

            var data = new aiMetadata();
            data.mNumProperties = numProperties;
            data.mKeys = new string[data.mNumProperties];
            data.mValues = new aiMetadataEntry[data.mNumProperties];

            return data;
        }

        ///**
        // *  @brief Deallocates property fields + keys.
        // */
        //static void Dealloc(aiMetadata metadata) {
        //    delete metadata;
        //}

        void Add<T>(string key, T value) {
            var new_keys = new string[mNumProperties + 1];
            var new_values = new aiMetadataEntry[mNumProperties + 1];

            for (var i = 0; i < mNumProperties; ++i) {
                new_keys[i] = mKeys[i];
                new_values[i] = mValues[i];
            }

            //delete[] mKeys;
            //delete[] mValues;

            mKeys = new_keys;
            mValues = new_values;

            mNumProperties++;

            Set(mNumProperties - 1, key, value);
        }

        //template<typename T>
        bool Set<T>(uint index, string key, T value) {
            throw new NotImplementedException();
            //// In range assertion
            //if (index >= mNumProperties) {
            //    return false;
            //}

            //// Ensure that we have a valid key.
            //if (string.IsNullOrEmpty(key)) {
            //    return false;
            //}

            //// Set metadata key
            //mKeys[index] = key;

            //// Set metadata type
            //mValues[index].mType = GetAiType(value);

            //// Copy the given value to the dynamic storage
            //if (null != mValues[index].mData && AI_AIMETADATA != mValues[index].mType) {
            //    //todo: ::memcpy(mValues[index].mData, &value, sizeof(T));
            //}
            //else if (null != mValues[index].mData && AI_AIMETADATA == mValues[index].mType) {
            //    *static_cast<T*>(mValues[index].mData) = value;
            //}
            //else {
            //    if (null != mValues[index].mData) {
            //        delete static_cast<T *> (mValues[index].mData);
            //        mValues[index].mData = null;
            //    }
            //    mValues[index].mData = new T(value);
            //}

            //return true;
        }

        bool Set<T>(string key, T value) {
            throw new NotImplementedException();
            //if (key.empty()) {
            //    return false;
            //}

            //bool result = false;
            //for (uint i = 0; i < mNumProperties; ++i) {
            //    if (key == mKeys[i].C_Str()) {
            //        Set(i, key, value);
            //        result = true;
            //        break;
            //    }
            //}

            //return result;
        }

        bool Get<T>(uint index, T value) {
            throw new NotImplementedException();
            //// In range assertion
            //if (index >= mNumProperties) {
            //    return false;
            //}

            //// Return false if the output data type does
            //// not match the found value's data type
            //if (GetAiType(value) != mValues[index].mType) {
            //    return false;
            //}

            //// Otherwise, output the found value and
            //// return true
            //value = *static_cast<T*>(mValues[index].mData);

            //return true;
        }

        bool Get<T>(string key, T value) {
            throw new NotImplementedException();
            //// Search for the given key
            //for (uint i = 0; i < mNumProperties; ++i) {
            //    if (mKeys[i] == key) {
            //        return Get(i, value);
            //    }
            //}
            //return false;
        }

        //bool Get<T>(string key, T value) {
        //    return Get(key, value);
        //}

        /// Return metadata entry for analyzing it by user.
        /// \param [in] pIndex - index of the entry.
        /// \param [out] pKey - pointer to the key value.
        /// \param [out] pEntry - pointer to the entry: type and value.
        /// \return false - if pIndex is out of range, else - true.
        bool Get(int index, ref string key, ref aiMetadataEntry entry) {
            throw new NotImplementedException();
            //if (index >= mNumProperties) {
            //    return false;
            //}

            //key = &mKeys[index];
            //entry = &mValues[index];

            //return true;
        }

        /// Check whether there is a metadata entry for the given key.
        /// \param [in] Key - the key value value to check for.
        bool HasKey(char* key) {
            throw new NotImplementedException();
            //if (null == key) {
            //    return false;
            //}

            //// Search for the given key
            //for (uint i = 0; i < mNumProperties; ++i) {
            //    if (0 == strncmp(mKeys[i].C_Str(), key, mKeys[i].length)) {
            //        return true;
            //    }
            //}
            //return false;
        }

        bool CompareKeys(aiMetadata lhs, aiMetadata rhs) {
            if (lhs.mNumProperties != rhs.mNumProperties) {
                return false;
            }

            for (uint i = 0; i < lhs.mNumProperties; ++i) {
                if (lhs.mKeys[i] != rhs.mKeys[i]) {
                    return false;
                }
            }
            return true;
        }

        bool CompareValues(aiMetadata lhs, aiMetadata rhs) {
            if (lhs.mNumProperties != rhs.mNumProperties) {
                return false;
            }

            for (uint i = 0; i < lhs.mNumProperties; ++i) {
                if (lhs.mValues[i].mType != rhs.mValues[i].mType) {
                    return false;
                }

                switch (lhs.mValues[i].mType) {
                case aiMetadataType.AI_BOOL: {
                    if ((bool)(lhs.mValues[i].mData) != (bool)(rhs.mValues[i].mData)) {
                        return false;
                    }
                }
                break;
                case aiMetadataType.AI_INT32: {
                    if ((Int32)(lhs.mValues[i].mData) != (Int32)(rhs.mValues[i].mData)) {
                        return false;
                    }
                }
                break;
                case aiMetadataType.AI_UINT64: {
                    if ((UInt64)(lhs.mValues[i].mData) != (UInt64)(rhs.mValues[i].mData)) {
                        return false;
                    }
                }
                break;
                case aiMetadataType.AI_FLOAT: {
                    if ((float)(lhs.mValues[i].mData) != (float)(rhs.mValues[i].mData)) {
                        return false;
                    }
                }
                break;
                case aiMetadataType.AI_DOUBLE: {
                    if ((double)(lhs.mValues[i].mData) != (double)(rhs.mValues[i].mData)) {
                        return false;
                    }
                }
                break;
                case aiMetadataType.AI_AISTRING: {
                    if ((string)(lhs.mValues[i].mData) != (string)(rhs.mValues[i].mData)) {
                        return false;
                    }
                }
                break;
                case aiMetadataType.AI_AIVECTOR3D: {
                    if ((vec3)(lhs.mValues[i].mData) != (vec3)(rhs.mValues[i].mData)) {
                        return false;
                    }
                }
                break;
                case aiMetadataType.AI_AIMETADATA: {
                    if ((aiMetadata)(lhs.mValues[i].mData) != (aiMetadata)(rhs.mValues[i].mData)) {
                        return false;
                    }
                }
                break;
                case aiMetadataType.AI_INT64: {
                    if ((Int64)(lhs.mValues[i].mData) != (Int64)(rhs.mValues[i].mData)) {
                        return false;
                    }
                }
                break;
                case aiMetadataType.AI_UINT32: {
                    if ((UInt32)(lhs.mValues[i].mData) != (UInt32)(rhs.mValues[i].mData)) {
                        return false;
                    }
                }
                break;
                //# ifndef SWIG
                //case aiMetadataType.FORCE_32BIT:
                //break;
                //#endif
                default:
                break;
                }
            }

            return true;
        }

        //friend bool operator ==(aiMetadata &lhs, aiMetadata &rhs) {
        //    return CompareKeys(lhs, rhs) && CompareValues(lhs, rhs);
        //}

        //friend bool operator !=(aiMetadata &lhs, aiMetadata &rhs) {
        //    return !(lhs == rhs);
        //}

        //#endif // __cplusplus

    }
}