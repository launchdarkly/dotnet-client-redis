using System;
using System.Collections.Generic;
using Common.Logging;
using LaunchDarkly.Client.Utils;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace LaunchDarkly.Client.Redis
{
    internal sealed class RedisFeatureStoreCore : IFeatureStoreCore
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(RedisFeatureStoreCore));
        
        private readonly ConnectionMultiplexer _redis;
        private readonly string _prefix;
        
        // This is used for unit testing only
        private Action _updateHook;

        internal RedisFeatureStoreCore(
            ConfigurationOptions redisConfig,
            string prefix,
            Action updateHook
            )
        {
            redisConfig = redisConfig.Clone();
            Log.InfoFormat("Creating Redis feature store using Redis server(s) at [{0}]",
                String.Join(", ", redisConfig.EndPoints));
            _redis = ConnectionMultiplexer.Connect(redisConfig);

            _prefix = prefix;
            _updateHook = updateHook;
        }
        
        public bool InitializedInternal()
        {
            IDatabase db = _redis.GetDatabase();
            return db.KeyExists(_prefix);
        }

        public void InitInternal(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> items)
        {
            IDatabase db = _redis.GetDatabase();
            ITransaction txn = db.CreateTransaction();
            foreach (KeyValuePair<IVersionedDataKind, IDictionary<string, IVersionedData>> collection in items)
            {
                string key = ItemsKey(collection.Key);
                txn.KeyDeleteAsync(key);
                foreach (KeyValuePair<string, IVersionedData> item in collection.Value)
                {
                    txn.HashSetAsync(key, item.Key, JsonConvert.SerializeObject(item.Value));
                    // Note, these methods are async because this Redis client treats all actions
                    // in a transaction as async - they are only sent to Redis when we execute the
                    // transaction. We don't need to await them.
                }
            }
            txn.StringSetAsync(_prefix, "");
            txn.Execute();
        }
        
        public IVersionedData GetInternal(IVersionedDataKind kind, string key)
        {
            IDatabase db = _redis.GetDatabase();
            string json = db.HashGet(ItemsKey(kind), key);
            if (json == null)
            {
                Log.DebugFormat("[get] Key: {0} not found in \"{1}\"", key, kind.GetNamespace());
                return null;
            }
            return FeatureStoreHelpers.UnmarshalJson(kind, json);
        }

        public IDictionary<string, IVersionedData> GetAllInternal(IVersionedDataKind kind)
        {
            IDatabase db = _redis.GetDatabase();
            HashEntry[] allEntries = db.HashGetAll(ItemsKey(kind));
            Dictionary<string, IVersionedData> result = new Dictionary<string, IVersionedData>();
            foreach (HashEntry entry in allEntries)
            {
                IVersionedData item = FeatureStoreHelpers.UnmarshalJson(kind, entry.Value);
                result[item.Key] = item;
            }
            return result;
        }

        public IVersionedData UpsertInternal(IVersionedDataKind kind, IVersionedData newItem)
        {
            IDatabase db = _redis.GetDatabase();
            string baseKey = ItemsKey(kind);
            while (true)
            {
                string oldJson;
                try
                {
                    oldJson = db.HashGet(baseKey, newItem.Key);
                }
                catch (RedisTimeoutException e)
                {
                    Log.ErrorFormat("Timeout in update when reading {0} from {1}: {2}", newItem.Key, baseKey, e.ToString());
                    throw;
                }
                IVersionedData oldItem = (oldJson == null) ? null : FeatureStoreHelpers.UnmarshalJson(kind, oldJson);
                int oldVersion = (oldJson == null) ? -1 : oldItem.Version;
                if (oldVersion >= newItem.Version)
                {
                    Log.DebugFormat("Attempted to {0} key: {1} version: {2} with a version that is" +
                        " the same or older: {3} in \"{4}\"",
                        newItem.Deleted ? "delete" : "update",
                        newItem.Key, oldVersion, newItem.Version, kind.GetNamespace());
                    return oldItem;
                }

                // This hook is used only in unit tests
                _updateHook?.Invoke();

                // Note that transactions work a bit differently in StackExchange.Redis than in other
                // Redis clients. The same Redis connection is shared across all threads, so it can't
                // set a WATCH at the moment we start the transaction. Instead, it saves up all of
                // the actions we send during the transaction, and replays them all within a MULTI
                // when the transaction. AddCondition() is this client's way of doing a WATCH, and it
                // can only refer to the whole value, not to a JSON property of the value; that's why
                // we kept track of the whole value in "oldJson".
                ITransaction txn = db.CreateTransaction();
                txn.AddCondition(oldJson == null ? Condition.HashNotExists(baseKey, newItem.Key) :
                    Condition.HashEqual(baseKey, newItem.Key, oldJson));

                txn.HashSetAsync(baseKey, newItem.Key, JsonConvert.SerializeObject(newItem));

                try
                {
                    bool success = txn.Execute();
                    if (!success)
                    {
                        // The watch was triggered, we should retry
                        Log.Debug("Concurrent modification detected, retrying");
                        continue;
                    }
                }
                catch (RedisTimeoutException e)
                {
                    Log.ErrorFormat("Timeout on update of {0} in {1}: {2}", newItem.Key, baseKey, e.ToString());
                    throw;
                }
                return newItem;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _redis.Dispose();
            }
        }
        
        private string ItemsKey(IVersionedDataKind kind)
        {
            return _prefix + ":" + kind.GetNamespace();
        }
    }
}
