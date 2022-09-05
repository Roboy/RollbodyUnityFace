// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

namespace AudioStream
{
    /// <summary>
    /// Implementation of a filesystem for FMOD
    /// </summary>
    public abstract class DownloadFileSystemBase
    {
        // TODO: probably implement proper logging with logging level

        /// <summary>
        /// Currentl allocated
        /// </summary>
        public abstract uint capacity { get; protected set; }
        /// <summary>
        /// Available for FMOD reads (for playback)
        /// </summary>
        public abstract uint available { get; protected set; }
        /// <summary>
        /// just for max EOF warning atm
        /// </summary>
        protected uint decoder_block_size;
        /// <summary>
        /// read/write lock
        /// </summary>
        protected readonly object bufferLock = new object();
        public DownloadFileSystemBase(uint _decoder_block_size)
        {
            this.decoder_block_size = _decoder_block_size;
        }
        /// <summary>
        /// Write content (via download handler)
        /// </summary>
        /// <param name="bytes"></param>
        public abstract void Write(byte[] bytes);
        /// <summary>
        /// Called by FMOD
        /// </summary>
        /// <param name="_toread"></param>
        /// <param name="_offset"></param>
        /// <returns></returns>
        public abstract byte[] Read(uint _offset, uint _toread);
        /// <summary>
        /// Free allocated backing store
        /// </summary>
        public abstract void CloseStore();
    }
}