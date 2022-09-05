// (c) 2016-2020 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections.Generic;

namespace AudioStream
{
    public class ThreadSafeQueue<T>
    {
        readonly object @lock = new object();
        Queue<T> queue = new Queue<T>();

        /// <summary>
        /// Max mark after which incoming queue data will get dropped
        /// </summary>
        int maxCapacity = 100;

        public ThreadSafeQueue(int capacity)
        {
            this.maxCapacity = capacity;
        }

        public T Dequeue()
        {
            lock (this.@lock)
            {
                if (this.queue.Count > 0)
                    return this.queue.Dequeue();
                else
                    return default(T);
            }
        }

        public void Enqueue(T value)
        {
            // start dropping packets after max capacity limit is reached
            if (this.queue.Count > maxCapacity)
                return;

            lock (this.@lock)
            {
                this.queue.Enqueue(value);
            }
        }
        /// <summary>
        /// Queue size
        /// </summary>
        /// <returns></returns>
        public int Size()
        {
            lock (this.@lock)
            {
                return this.queue.Count;
            }
        }
    }
}