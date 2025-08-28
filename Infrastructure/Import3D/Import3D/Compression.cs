using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using static Import3D.Compression;

namespace Import3D {

    /// @brief This class provides the decompression of zlib-compressed data.
    public unsafe class Compression {
        public int MaxWBits = /*MAX_WBITS*/15;
        public struct impl {
            public bool mOpen;
            public z_stream mZSstream;
            public FlushMode mFlushMode;

            public impl() {
                this.mFlushMode = FlushMode.NoFlush;
                // empty
            }
        };

        public impl* mImpl;

        /// @brief Describes the format data type
        public enum Format {
            InvalidFormat = -1, ///< Invalid enum type.
            Binary = 0,         ///< Binary format.
            ASCII,              ///< ASCII format.

            NumFormats          ///< The number of supported formats.
        };

        /// @brief The supported flush mode, used for blocked access.
        public enum FlushMode {
            InvalidFormat = -1, ///< Invalid enum type.
            NoFlush = 0,        ///< No flush, will be done on inflate end.
            Block,              ///< Assists in combination of compress.
            Tree,               ///< Assists in combination of compress and returns if stream is finish.
            SyncFlush,          ///< Synced flush mode.
            Finish,             ///< Finish mode, all in once, no block access.

            NumModes            ///< The number of supported modes.
        };

        /// @brief  Will open the access to the compression.
        /// @param[in] format       The format type
        /// @param[in] flush        The flush mode.
        /// @param[in] windowBits   The windows history working size, shall be between 8 and 15.
        /// @return true if close was successful, false if not.
        public bool open(Format format, FlushMode flush, int windowBits) {
            System.Diagnostics.Debug.Assert(mImpl != null);

            if (mImpl->mOpen) {
                return false;
            }

            // build a zlib stream
            mImpl->mZSstream.opaque = Z_NULL;
            mImpl->mZSstream.zalloc = Z_NULL;
            mImpl->mZSstream.zfree = Z_NULL;
            mImpl->mFlushMode = flush;
            if (format == Format::Binary) {
                mImpl->mZSstream.data_type = Z_BINARY;
            }
            else {
                mImpl->mZSstream.data_type = Z_ASCII;
            }

            // raw decompression without a zlib or gzip header
            if (windowBits == 0) {
                inflateInit(&mImpl->mZSstream);
            }
            else {
                inflateInit2(&mImpl->mZSstream, windowBits);
            }
            mImpl->mOpen = true;

            return mImpl->mOpen;

        }

        /// @brief  Will return the open state.
        /// @return true if the access is opened, false if not.
        public bool isOpen();

        /// @brief  Will close the decompress access.
        /// @return true if close was successful, false if not.
        public bool close();

        /// @brief Will decompress the data buffer in one step.
        /// @param[in] data         The data to decompress
        /// @param[in] in           The size of the data.
        /// @param[out uncompressed A std::vector containing the decompressed data.
        public int decompress(byte* data, int inValue, byte[] uncompressed) {
            System.Diagnostics.Debug.Assert(mImpl != null);
            if (data == null || inValue == 0) {
                return 0;
            }

            mImpl->mZSstream.next_in = (byte*)(data);
            mImpl->mZSstream.avail_in = (uint)inValue;

            int ret = 0;
            int total = 0;
            int flushMode = getFlushMode(mImpl->mFlushMode);
            if (flushMode == Z_FINISH) {
                mImpl->mZSstream.avail_out = (uint)(uncompressed.size());
                mImpl->mZSstream.next_out = (byte*)(uncompressed.begin());
                ret = inflate(&mImpl->mZSstream, Z_FINISH);

                if (ret != Z_STREAM_END && ret != Z_OK) {
                    throw new Exception("Compression", "Failure decompressing this file using gzip.");
                }
                total = mImpl->mZSstream.avail_out;
            }
            else {
                do {
                    byte[] block = new byte[MYBLOCK];
                    mImpl->mZSstream.avail_out = MYBLOCK;
                    mImpl->mZSstream.next_out = block;
                    ret = inflate(&mImpl->mZSstream, flushMode);

                    if (ret != Z_STREAM_END && ret != Z_OK) {
                        throw DeadlyImportError("Compression", "Failure decompressing this file using gzip.");
                    }
                    int have = MYBLOCK - mImpl->mZSstream.avail_out;
                    total += have;
                    uncompressed.resize(total);
                    memcpy(uncompressed.data() + total - have, block, have);
                } while (ret != Z_STREAM_END);
            }

            return total;

        }

        /// @brief Will decompress the data buffer block-wise.
        /// @param[in]  data         The compressed data
        /// @param[in]  in           The size of the data buffer
        /// @param[out] out          The output buffer
        /// @param[out] availableOut The upper limit of the output buffer.
        /// @return The size of the decompressed data buffer.
        public int decompressBlock(void* data, int in, char*out, int availableOut) {
            System.Diagnostics.Debug.Assert(mImpl != null);
            if (data == null || in == 0 || out == null || availableOut == 0) {
                return 0l;
            }

            // push data to the stream
            mImpl->mZSstream.next_in = (byte*)data;
            mImpl->mZSstream.avail_in = (uInt)in;
            mImpl->mZSstream.next_out = (byte*)out;
            mImpl->mZSstream.avail_out = (uInt)availableOut;

            // and decompress the data ....
            int ret = ::inflate(&mImpl->mZSstream, Z_SYNC_FLUSH);
            if (ret != Z_OK && ret != Z_STREAM_END) {
                throw DeadlyImportError("X: Failed to decompress MSZIP-compressed data");
            }

            inflateReset(&mImpl->mZSstream);
            inflateSetDictionary(&mImpl->mZSstream, (byte*)out, (uInt)availableOut - mImpl->mZSstream.avail_out);

            return availableOut - (int)mImpl->mZSstream.avail_out;

        }

    }
}
