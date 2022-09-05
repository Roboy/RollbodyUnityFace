// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.IO;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Implements file system for FMOD via file on regular FS
    /// </summary>
    public class DownloadFileSystemCachedFile : DownloadFileSystemBase
    {
        /// <summary>
        /// backing store
        /// </summary>
        FileStream fileStream = null;
        BinaryWriter writer = null;
        public override uint capacity
        {
            get
            {
                return (uint)this.writer.BaseStream.Length;
            }

            protected set
            {
                throw new System.NotImplementedException();
            }
        }
        public override uint available { get; protected set; }

        public DownloadFileSystemCachedFile(string filepath, uint _decoder_block_size)
            :base(_decoder_block_size)
        {
            this.fileStream = new FileStream(filepath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            this.writer = new BinaryWriter(this.fileStream);
        }
        public override void Write(byte[] bytes)
        {
            lock (this.bufferLock)
            {
                this.writer.Seek(0, SeekOrigin.End);
                this.writer.Write(bytes);
            }
        }

        public override byte[] Read(uint _offset, uint _toread)
        {
            lock (this.bufferLock)
            {
                // copy params for easier typing and to modify locally
                long toread = _toread;

                // warn on compelte underflow, try not to on near EOF reads for undefined length streams
                // otherwise return at least what's available

                long av = this.writer.BaseStream.Length - _offset;
                if (av < 1)
                {
                    if ((AudioStreamBase.INFINITE_LENGTH - _offset) > (this.decoder_block_size * 2))
                        Debug.LogWarningFormat("Read underflow | r offset: {0} length: {1} [{2}], available: {3}", _offset, toread, this.writer.BaseStream.Length, av);

                    this.available = 0;
                    return new byte[0];
                }

                this.available = (uint)av;

                if (this.available < toread)
                    toread = this.available;

                var result = new byte[toread];

                this.fileStream.Seek(_offset, SeekOrigin.Begin);

                this.fileStream.Read(result, 0, (int)toread);

                return result;
            }
        }
        public override void CloseStore()
        {
            lock (this.bufferLock)
            {
                this.writer.Close();
                // TODO:
                // Dispose is protected on 3.5 runtime..
                // this.writer.Dispose();
                this.writer = null;

                this.fileStream.Close();
                this.fileStream.Dispose();
                this.fileStream = null;
            }
        }
    }
}