//
// LruCache.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using Sharpen;

namespace Couchbase.Lite.Util
{
    //LruCache implementation (null values disallowed)
    internal class LruCache<TKey, TValue> 
    where TKey: class 
    where TValue: class
    {

        #region Variables

        private readonly Dictionary<TKey, TValue> _hashmap = new Dictionary<TKey, TValue>();
        private readonly LinkedList<TKey> _nodes = new LinkedList<TKey>();
        private readonly Object _locker = new Object ();

        #endregion

        #region Properties

        public int Size { get; private set; }
        public int MaxSize { get; private set; }
        public int PutCount { get; private set; }
        public int CreateCount { get; private set; }
        public int EvictionCount { get; private set; }
        public int HitCount { get; private set; }
        public int MissCount { get; private set; }

        #endregion

        #region Constructors

        public LruCache(int maxSize)
        {
            if (maxSize <= 0) {
                throw new ArgumentException("maxSize <= 0");
            }

            MaxSize = maxSize;
        }

        #endregion

        #region Public Methods

        public virtual void Resize(int maxSize)
        {
            if (maxSize <= 0) {
                throw new ArgumentException("maxSize <= 0");
            }

            lock (_locker) {
                MaxSize = maxSize;
            }

            Trim();
        }

        public TValue Get(TKey key)
        {
            if (key == null) {
                throw new ArgumentNullException("key");
            }

            TValue mapValue;
            lock (_locker) {
                mapValue = _hashmap.Get(key);
                if (mapValue != null) {
                    HitCount++;
                    _nodes.Remove(key);
                    _nodes.AddFirst(key);
                    return mapValue;
                }
                MissCount++;
            }

            TValue createdValue = Create(key);
            if (createdValue == null) {
                return default(TValue);
            }

            lock (_locker) {
                CreateCount++;
                mapValue = _hashmap.Put(key, createdValue);
                _nodes.Remove(key);
                _nodes.AddFirst(key);
                if (mapValue != null) {
                    // There was a conflict so undo that last put
                    _hashmap[key] = mapValue;
                }
                else {
                    Size += SafeSizeOf(key, createdValue);
                }
            }

            if (mapValue != null) {
                EntryRemoved(false, key, createdValue, mapValue);
                return mapValue;
            } else {
                Trim();
                return createdValue;
            }
        }

        public TValue this[TKey key] {
            get { return Get (key); }
            set { Put (key, value); }
        }
            
        public TValue Put(TKey key, TValue value)
        {
            if (key == null || value == null) {
                throw new ArgumentNullException("key == null || value == null");
            }

            TValue previous;
            lock (_locker) {
                PutCount++;
                Size += SafeSizeOf(key, value);
                previous = _hashmap.Put(key, value);
                if (previous != null) {
                    Size -= SafeSizeOf(key, previous);
                }
                else {
                    _nodes.AddFirst(key);
                }
            }

            if (previous != null) {
                EntryRemoved(false, key, previous, value);
            }

            Trim();
            return previous;
        }
            
        private void Trim()
        {
            while (true) {
                TKey key;
                TValue value;
                lock (_locker) {
                    if (Size < 0 || _hashmap.Count != Size || _nodes.Count != Size) {
                        throw new InvalidOperationException(GetType().FullName + ".sizeOf() is reporting inconsistent results!");
                    }

                    if (Size <= MaxSize || Size == 0) {
                        break;
                    }
                        
                    key = _nodes.Last.Value;
                    value = _hashmap[key];
                    _hashmap.Remove(key);
                    _nodes.RemoveLast();
                    Size -= SafeSizeOf(key, value);
                    EvictionCount++;
                }

                EntryRemoved(true, key, value, default(TValue));
            }
        }
            
        public TValue Remove(TKey key)
        {
            if (key == null) {
                throw new ArgumentNullException("key == null");
            }

            TValue previous;
            lock (_locker) {
                previous = _hashmap.Get(key);
                _hashmap.Remove(key);
                if (previous != null) {
                    Size -= SafeSizeOf(key, previous);
                    _nodes.Remove(key);
                }
            }

            if (previous != null) {
                EntryRemoved(false, key, previous, default(TValue));
            }

            return previous;
        }
            
        protected internal virtual void EntryRemoved(bool evicted, TKey key, TValue oldValue, TValue newValue)
        {
            //Used by subclasses for logic when an entry has been removed from the cache via
            //ejection, or calls to put/remove
        }
            
        protected internal virtual TValue Create(TKey key)
        {
            //Return the compiler default value for a new TValue, but can be overriden in subclasses
            return default(TValue);
        }

        private int SafeSizeOf(TKey key, TValue value)
        {
            int result = SizeOf(key, value);
            if (result < 0)
            {
                throw new InvalidOperationException("Negative size: " + key + "=" + value);
            }
            return result;
        }
            
        protected internal virtual int SizeOf(TKey key, TValue value)
        {
            //The size of an entry in user defined units.  This must not change
            //for any given entry while it is in the cache
            return 1;
        }
            
        public void EvictAll()
        {
            lock(_locker) {
                int oldMax = MaxSize;
                MaxSize = 0;
                Trim();
                MaxSize = oldMax;
            }
        }
            
        public IDictionary<TKey, TValue> Snapshot()
        {
            lock (_locker) {
                return new Dictionary<TKey, TValue>(_hashmap);
            }
        }

        #endregion

        #region Overrides

        public sealed override string ToString()
        {
            lock (_locker) {
                int accesses = HitCount + MissCount;
                int hitPercent = accesses != 0 ? (100 * HitCount / accesses) : 0;
                return string.Format ("LruCache[maxSize={0},hits={1},misses={2},hitRate={3:P}%]", MaxSize, HitCount, MissCount, hitPercent);
            }
        }

        #endregion

    }
}
