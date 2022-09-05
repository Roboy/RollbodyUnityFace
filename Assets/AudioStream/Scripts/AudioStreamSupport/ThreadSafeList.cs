// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections.Generic;
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// ThreadSafeList strongly typed to the float data.
    /// This should possibly have better performance in the CLR because it avoids the boxed arrays used in the generic buffer class
    /// </summary>
    public class ThreadSafeListFloat
    {
        List<float> list;
        readonly object @lock = new object();

        public ThreadSafeListFloat(int initialCapacity)
        {
            this.list = new List<float>(initialCapacity);
        }

        public void Write(float[] write)
        {
            lock (this.@lock)
                this.list.AddRange(write);
        }

        public float[] Read(int count)
        {
            lock (this.@lock)
            {
                var toread = Mathf.Min(count, this.list.Count);

                var result = this.list.GetRange(0, toread);

                this.list.RemoveRange(0, toread);

                return result.ToArray();
            }
        }

        public int Available()
        {
            lock (this.@lock)
                return this.list.Count;
        }

        public int Capacity()
        {
            lock (this.@lock)
                return this.list.Capacity;
        }
    }

    public class ThreadSafeListByte
    {
        List<byte> list;
        readonly object @lock = new object();

        public ThreadSafeListByte(int initialCapacity)
        {
            this.list = new List<byte>(initialCapacity);
        }

        public void Write(byte[] write)
        {
            lock (this.@lock)
                this.list.AddRange(write);
        }

        public byte[] Read(int count)
        {
            lock (this.@lock)
            {
                var toread = Mathf.Min(count, this.list.Count);

                var result = this.list.GetRange(0, toread);

                this.list.RemoveRange(0, toread);

                return result.ToArray();
            }
        }

        public int Available()
        {
            lock (this.@lock)
                return this.list.Count;
        }

        public int Capacity()
        {
            lock (this.@lock)
                return this.list.Capacity;
        }
    }
}