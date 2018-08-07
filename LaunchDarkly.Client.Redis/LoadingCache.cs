﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LaunchDarkly.Client.Redis
{
    /// <summary>
    /// A concurrent in-memory cache with read-through behavior, an optional TTL, and the ability to
    /// explicitly set values. Expired entries are purged by a background task.
    /// 
    /// A cache hit requires only one read lock. A cache miss requires read and write locks on the
    /// cache, and then a write lock on the individual entry.
    /// 
    /// Null values are allowed.
    /// </summary>
    internal sealed class LoadingCache<K, V> : IDisposable where V : class
    {
        private static readonly TimeSpan DefaultPurgeInterval = TimeSpan.FromSeconds(30);

        private readonly Func<K, V> _computeFn;
        private readonly TimeSpan? _expiration;
        private readonly TimeSpan _purgeInterval;
        private readonly IDictionary<K, CacheEntry<K, V>> _entries = new Dictionary<K, CacheEntry<K, V>>();
        private readonly LinkedList<K> _keysInCreationOrder = new LinkedList<K>();
        private readonly ReaderWriterLockSlim _wholeCacheLock = new ReaderWriterLockSlim();
        private volatile bool _disposed = false;

        public LoadingCache(Func<K, V> computeFn, TimeSpan? expiration) : this(computeFn, expiration, DefaultPurgeInterval) { }

        public LoadingCache(Func<K, V> computeFn, TimeSpan? expiration, TimeSpan purgeInterval)
        {
            _computeFn = computeFn;
            _expiration = expiration;
            _purgeInterval = purgeInterval;
            if (expiration.HasValue)
            {
                Task.Run(() => PurgeExpiredEntriesAsync());
            }
        }
        
        /// <summary>
        /// Gets a value from the cache - computing and caching a new value if it did not already exist. If multiple
        /// threads request the same key and it does not yet exist in the cache, only one thread will call the
        /// compute function, and the others will wait for it.
        /// </summary>
        /// <param name="key">the cache key</param>
        /// <returns>the cached or computed value</returns>
        public V Get(K key)
        {
            _wholeCacheLock.EnterReadLock();
            bool entryExists;
            CacheEntry<K, V> entry;
            try
            {
                entryExists = _entries.TryGetValue(key, out entry);
            }
            finally
            {
                _wholeCacheLock.ExitReadLock();
            }
            if (entryExists)
            {
                // This key exists in the cache, but may or may not have a value yet. If the inited
                // flag is set then we can read the value without acquiring a lock, since the value
                // will never change for a CacheEntry once it's been set (and inited is not set until
                // value has been set).
                if (entry.inited)
                {
                    return entry.value;
                }
                return MaybeComputeValue(key, entry);
            }

            // The entry needs to be added to the cache. First add it without a value, so we can quickly release the
            // lock on the whole cache.
            _wholeCacheLock.EnterWriteLock();
            try
            {
                // Check for the entry again in case someone got in ahead of us
                if (!_entries.TryGetValue(key, out entry))
                {
                    DateTime? expTime = null;
                    if (_expiration.HasValue)
                    {
                        expTime = DateTime.Now.Add(_expiration.Value);
                    }
                    var node = new LinkedListNode<K>(key);
                    entry = new CacheEntry<K, V>(expTime, node);
                    _entries[key] = entry;
                    _keysInCreationOrder.AddLast(node);
                }
            }
            finally
            {
                _wholeCacheLock.ExitWriteLock();
            }
            // Now proceed as if the entry was already in the cache, computing its value if necessary
            return MaybeComputeValue(key, entry);
        }
        
        private V MaybeComputeValue(K key, CacheEntry<K, V> entry)
        {
            // At this point we have a cache entry with no value. Whichever thread acquires the
            // per-entry lock first will compute the value; the others will wait on it.
            lock(entry.entryLock)
            {
                // Check the inited flag again in case someone got in ahead of us
                if (!entry.inited)
                {
                    entry.value = _computeFn.Invoke(key);
                    entry.inited = true;
                }
            }
            return entry.value;
        }
        
        /// <summary>
        /// Stores a value in the cache, overwriting any previously cached value.
        /// </summary>
        /// <param name="key">the cache key</param>
        /// <param name="value">the value to store</param>
        public void Set(K key, V value)
        {
            _wholeCacheLock.EnterWriteLock();
            try
            {
                if (_entries.TryGetValue(key, out var oldEntry))
                {
                    _keysInCreationOrder.Remove(oldEntry.node);
                }
                DateTime? expTime = null;
                if (_expiration.HasValue)
                {
                    expTime = DateTime.Now.Add(_expiration.Value);
                }
                var node = new LinkedListNode<K>(key);
                var entry = new CacheEntry<K, V>(expTime, node);
                entry.value = value;
                entry.inited = true;
                _entries[key] = entry;
                _keysInCreationOrder.AddLast(node);
            }
            finally
            {
                _wholeCacheLock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            _disposed = true;
        }

        private void PurgeExpiredEntries()
        {
            _wholeCacheLock.EnterWriteLock();
            try
            {
                while (_keysInCreationOrder.Count > 0 &&
                       _entries[_keysInCreationOrder.First.Value].IsExpired())
                {
                    _entries.Remove(_keysInCreationOrder.First.Value);
                    _keysInCreationOrder.RemoveFirst();
                }
            }
            finally
            {
                _wholeCacheLock.ExitWriteLock();
            }
        }

        private async Task PurgeExpiredEntriesAsync()
        {
            while (!_disposed)
            {
                await Task.Delay(_purgeInterval);
                PurgeExpiredEntries();
            }
        }
    }

    internal class CacheEntry<K, V> where V : class
    {
        public readonly DateTime? expirationTime;
        public readonly object entryLock;
        public readonly LinkedListNode<K> node;
        public volatile V value;
        public volatile bool inited;

        public CacheEntry(DateTime? expirationTime, LinkedListNode<K> node)
        {
            this.expirationTime = expirationTime;
            this.node = node;
            entryLock = new object();
            inited = false;
        }

        public bool IsExpired()
        {
            return expirationTime.HasValue && expirationTime.Value.CompareTo(DateTime.Now) <= 0;
        }
    }
}
