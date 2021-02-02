using System;
using LaunchDarkly.Client.SharedTests.FeatureStore;
using StackExchange.Redis;

namespace LaunchDarkly.Client.Integrations
{
    public class RedisFeatureStoreTest : FeatureStoreBaseTests
    {
        override protected IFeatureStore CreateStoreImpl(FeatureStoreCacheConfig caching)
        {
            return Components.PersistentDataStore(
                    Redis.DataStore()
                )
                .CacheTime(caching.Ttl)
                .CreateFeatureStore();
        }

        override protected IFeatureStore CreateStoreImplWithPrefix(string prefix)
        {
            return Components.PersistentDataStore(
                    Redis.DataStore().Prefix(prefix)
                )
                .NoCaching()
                .CreateFeatureStore();
        }

        protected override IFeatureStore CreateStoreImplWithUpdateHook(Action hook)
        {
            return base.CreateStoreImplWithUpdateHook(hook);
        }

        override protected void ClearAllData()
        {
            using (var cxn = ConnectionMultiplexer.Connect("localhost:6379,allowAdmin=true"))
            {
                cxn.GetServer("localhost:6379").FlushDatabase();
            }
        }
    }
}
