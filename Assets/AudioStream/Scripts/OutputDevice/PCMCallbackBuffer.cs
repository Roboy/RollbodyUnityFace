// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

namespace AudioStream
{
    /// <summary>
    /// An FMOD sound specific audio buffer for static PCM callbacks
    /// </summary>
    public class PCMCallbackBuffer
    {
        /// <summary>
        /// Length requested by FMOD in PCM callback
        /// </summary>
        public uint datalen;
        /// <summary>
        /// Max length encountered in callback
        /// </summary>
        public uint maxdatalen = 0;
        /// <summary>
        /// Min length encountered in callback
        /// </summary>
        public uint mindatalen = uint.MaxValue;
        /// <summary>
        /// Flag set in PCM callback indicating not enough data from OAFR
        /// </summary>
        public bool underflow = false;

        BasicBufferByte pcmReadCallback_Buffer = null;

        // lock on pcmReadCallbackBuffer - can be changed ( added to ) in OAFR thread leading to collisions
        readonly object pcmReadCallback_BufferLock = new object();

        // TODO: pass size computed from buffers sizes
        public PCMCallbackBuffer(int capacity)
        {
            this.pcmReadCallback_Buffer = new BasicBufferByte(capacity);
        }

        public void Enqueue(byte[] bytes)
        {
            lock (this.pcmReadCallback_BufferLock)
            {
                this.pcmReadCallback_Buffer.Write(bytes);
            }
        }

        public byte[] Dequeue(int requiredCount)
        {
            lock (this.pcmReadCallback_BufferLock)
            {
                var returnCount = System.Math.Min(requiredCount, this.pcmReadCallback_Buffer.Available());

                return this.pcmReadCallback_Buffer.Read(returnCount);
            }
        }

        public int Available
        {
            get { return this.pcmReadCallback_Buffer.Available(); }
        }

        public int Capacity
        {
            get { return this.pcmReadCallback_Buffer.Capacity(); }
        }
    }
}