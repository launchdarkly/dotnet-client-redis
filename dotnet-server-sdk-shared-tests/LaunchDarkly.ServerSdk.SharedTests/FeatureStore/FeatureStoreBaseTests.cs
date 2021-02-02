using System;
using System.Collections.Generic;
using Xunit;

namespace LaunchDarkly.Client.SharedTests.FeatureStore
{
    /// <summary>
    /// Base class for tests of an IFeatureStore implementation. This test suite should be run on
    /// every implementation, by subclassing it in the tests for that project.
    /// 
    /// You must override CreateStoreImpl and ClearAllData, and may also want to override
    /// InstancesShareSameData and CreateStoreImplWithPrefix depending on the behavior of your
    /// implementation.
    /// 
    /// The tests assume that all feature store implementations have a mechanism for local
    /// caching, and most of the tests are run twice, once with caching enabled and once with
    /// caching disabled. If an implementation doesn't support caching (which it should), just
    /// ignore the parameter to CreateStoreImpl and we'll be running the same tests twice.
    /// </summary>
    public abstract class FeatureStoreBaseTests : IDisposable
    {
        /// <summary>
        /// Override this method to create a new instance of the appropriate feature store class.
        /// The properties of every instance should be the same except for the caching configuration.
        /// </summary>
        /// <param name="caching">the caching configuration</param>
        /// <returns>a store instance</returns>
        protected abstract IFeatureStore CreateStoreImpl(FeatureStoreCacheConfig caching);

        /// <summary>
        /// Override this method to ensure that any existing data is removed from the underlying data store
        /// so that all feature store instances will be in an uninitialized state. If InstancesShareSameData
        /// is not true then this method does not need to do anything.
        /// </summary>
        protected abstract void ClearAllData();

        /// <summary>
        /// Override this to return false if two StoreT instances returned by CreateStore do *not*
        /// share the same underlying data, i.e. a change made in one cannot be read by the other.
        /// </summary>
        protected virtual bool InstancesShareSameData
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Override this method if the feature store supports a prefix string for allowing multiple
        /// independent clients to share the same data space. This is only used if InstancesShareSameData
        /// is true. The store should not have caching enabled.
        /// </summary>
        /// <param name="prefix">the prefix string</param>
        /// <returns>a store instance, or null if prefixes are not supported</returns>
        protected virtual IFeatureStore CreateStoreImplWithPrefix(string prefix)
        {
            return null;
        }

        /// <summary>
        /// Override this method if the feature store has a non-atomic update mechanism that needs to
        /// be tested for race conditions. The returned store instance should invoke the specified
        /// Action within every Upsert, after reading the current value but before writing the
        /// updated value. The test will use this to modify the value during that interval.
        /// </summary>
        /// <param name="hook">an Action to be executed during Upserts</param>
        /// <returns>a store instance, or null if this mechanism is not supported</returns>
        protected virtual IFeatureStore CreateStoreImplWithUpdateHook(Action hook)
        {
            return null;
        }

        private readonly TestEntity item1 = new TestEntity("foo", 5);
        private readonly TestEntity item2 = new TestEntity("bar", 5);
        private readonly OtherTestEntity other1 = new OtherTestEntity("baz", 5);
        private readonly string unusedKey = "whatever";

        private readonly List<IFeatureStore> storesCreated = new List<IFeatureStore>();

        public void Dispose()
        {
            foreach (var store in storesCreated)
            {
                store.Dispose();
            }
        }

        public static readonly IEnumerable<object[]> Modes = new List<object[]> {
            new object[] { TestMode.CACHED },
            new object[] { TestMode.UNCACHED } };

        private IFeatureStore MakeStore(TestMode mode)
        {
            var store = CreateStoreImpl(mode == TestMode.CACHED ?
                FeatureStoreCacheConfig.Enabled : FeatureStoreCacheConfig.Disabled);
            storesCreated.Add(store);
            return store;
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void StoreNotInitializedBeforeInit(TestMode mode)
        {
            ClearAllData();
            var store = MakeStore(mode);
            Assert.False(store.Initialized());
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void OneInstanceCanDetectIfAnotherInstanceHasInitializedStore(TestMode mode)
        {
            if (!InstancesShareSameData)
            {
                return; // XUnit has no way to mark a test as "skipped"
            }

            ClearAllData();
            var store1 = MakeStore(mode);
            store1.Init(new DataBuilder().Add(TestEntity.Kind, item1).Build());

            var store2 = MakeStore(mode);
            Assert.True(store2.Initialized());
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void StoreInitializedAfterInit(TestMode mode)
        {
            ClearAllData();
            var store = MakeStore(mode);
            store.Init(new DataBuilder().Build());
            Assert.True(store.Initialized());
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void InitCompletelyReplacesExistingData(TestMode mode)
        {
            ClearAllData();
            var store = MakeStore(mode);

            var allData = new DataBuilder()
                .Add(TestEntity.Kind, item1, item2)
                .Add(OtherTestEntity.Kind, other1)
                .Build();
            store.Init(allData);

            var item2v2 = item2.NextVersion();
            var data2 = new DataBuilder()
                .Add(TestEntity.Kind, item2v2)
                .Add(OtherTestEntity.Kind)
                .Build();
            store.Init(data2);

            Assert.Null(store.Get(TestEntity.Kind, item1.Key));
            Assert.Equal(item2v2, store.Get(TestEntity.Kind, item2.Key));
            Assert.Null(store.Get(OtherTestEntity.Kind, other1.Key));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void GetExistingItem(TestMode mode)
        {
            var store = MakeStore(mode);
            store.Init(new DataBuilder().Add(TestEntity.Kind, item1, item2).Build());
            Assert.Equal(item1, store.Get(TestEntity.Kind, item1.Key));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void GetNonexistingItem(TestMode mode)
        {
            var store = MakeStore(mode);
            store.Init(new DataBuilder().Add(TestEntity.Kind, item1, item2).Build());
            Assert.Null(store.Get(TestEntity.Kind, unusedKey));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void GetAllItems(TestMode mode)
        {
            var store = MakeStore(mode);
            store.Init(new DataBuilder().Add(TestEntity.Kind, item1, item2)
                .Add(OtherTestEntity.Kind, other1).Build());
            var result = store.All(TestEntity.Kind);
            Assert.Equal(2, result.Count);
            Assert.Equal(item1, result[item1.Key]);
            Assert.Equal(item2, result[item2.Key]);
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void GetAllWithDeletedItem(TestMode mode)
        {
            var store = MakeStore(mode);
            var deletedItem = new TestEntity(unusedKey, 1, true);
            store.Init(new DataBuilder().Add(TestEntity.Kind, item1, item2, deletedItem)
                .Add(OtherTestEntity.Kind, other1).Build());
            var result = store.All(TestEntity.Kind);
            Assert.Equal(2, result.Count);
            Assert.Equal(item1, result[item1.Key]);
            Assert.Equal(item2, result[item2.Key]);
            Assert.False(result.ContainsKey(deletedItem.Key));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void UpsertWithNewerVersion(TestMode mode)
        {
            var store = MakeStore(mode);
            store.Init(new DataBuilder().Add(TestEntity.Kind, item1, item2).Build());
            var newer = item1.NextVersion();
            store.Upsert(TestEntity.Kind, newer);
            Assert.Equal(newer, store.Get(TestEntity.Kind, item1.Key));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void UpsertWithSameVersion(TestMode mode)
        {
            var store = MakeStore(mode);
            store.Init(new DataBuilder().Add(TestEntity.Kind, item1, item2).Build());
            var sameVersionDifferentValue = item1.WithValue("modified");
            store.Upsert(TestEntity.Kind, sameVersionDifferentValue);
            Assert.Equal(item1, store.Get(TestEntity.Kind, item1.Key));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void UpsertWithOlderVersion(TestMode mode)
        {
            var store = MakeStore(mode);
            store.Init(new DataBuilder().Add(TestEntity.Kind, item1, item2).Build());
            var older = item1.WithVersion(item1.Version - 1);
            store.Upsert(TestEntity.Kind, older);
            Assert.Equal(item1, store.Get(TestEntity.Kind, item1.Key));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void UpsertNewItem(TestMode mode)
        {
            var store = MakeStore(mode);
            store.Init(new DataBuilder().Add(TestEntity.Kind, item1, item2).Build());
            var newItem = new TestEntity(unusedKey, 1);
            store.Upsert(TestEntity.Kind, newItem);
            Assert.Equal(newItem, store.Get(TestEntity.Kind, newItem.Key));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void DeleteWithNewerVersion(TestMode mode)
        {
            var store = MakeStore(mode);
            store.Init(new DataBuilder().Add(TestEntity.Kind, item1, item2).Build());
            store.Delete(TestEntity.Kind, item1.Key, item1.Version + 1);
            Assert.Null(store.Get(TestEntity.Kind, item1.Key));
            Assert.Equal(item2, store.Get(TestEntity.Kind, item2.Key));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void DeleteWithSameVersion(TestMode mode)
        {
            var store = MakeStore(mode);
            store.Init(new DataBuilder().Add(TestEntity.Kind, item1, item2).Build());
            store.Delete(TestEntity.Kind, item1.Key, item1.Version);
            Assert.Equal(item1, store.Get(TestEntity.Kind, item1.Key));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void DeleteWithOlderVersion(TestMode mode)
        {
            var store = MakeStore(mode);
            store.Init(new DataBuilder().Add(TestEntity.Kind, item1, item2).Build());
            store.Delete(TestEntity.Kind, item1.Key, item1.Version - 1);
            Assert.Equal(item1, store.Get(TestEntity.Kind, item1.Key));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void DeleteUnknownItem(TestMode mode)
        {
            var store = MakeStore(mode);
            store.Init(new DataBuilder().Add(TestEntity.Kind, item1).Build());
            store.Delete(TestEntity.Kind, unusedKey, 99);
            Assert.Null(store.Get(TestEntity.Kind, unusedKey));
        }

        [Theory]
        [MemberData(nameof(Modes))]
        public void UpsertOlderVersionAfterDelete(TestMode mode)
        {
            var store = MakeStore(mode);
            store.Init(new DataBuilder().Add(TestEntity.Kind, item1, item2).Build());
            store.Delete(TestEntity.Kind, item1.Key, item1.Version + 1);
            store.Upsert(TestEntity.Kind, item1);
            Assert.Null(store.Get(TestEntity.Kind, item1.Key));
        }

        [Fact]
        public void StoresWithDifferentPrefixAreIndependent()
        {
            // The prefix parameter, if supported, is a namespace for all of a store's data,
            // so that it won't interfere with data from some other instance with a different
            // prefix. This test verifies that Init, Get, All, and Upsert are all respecting
            // the prefix.

            if (!InstancesShareSameData)
            {
                return;
            }
            ClearAllData();
            var store1 = CreateStoreImplWithPrefix("aaa");
            if (store1 == null)
            {
                // This implementation doesn't support prefixes
                return;
            }
            storesCreated.Add(store1);
            var store2 = CreateStoreImplWithPrefix("bbb");
            storesCreated.Add(store2);

            Assert.False(store1.Initialized());
            Assert.False(store2.Initialized());
            
            var store1Item1 = new TestEntity("a", 1);
            var store1Item2 = new TestEntity("b", 1);
            var store1Item3 = new TestEntity("c", 1);
            var store2Item1 = new TestEntity("a", 99);
            var store2Item2 = new TestEntity("d", 1); // skipping "b" validates that store2.Init doesn't delete store1's "b" key
            var store2Item3 = new TestEntity("c", 2);
            store1.Init(new DataBuilder().Add(TestEntity.Kind, store1Item1, store1Item2).Build());
            store2.Init(new DataBuilder().Add(TestEntity.Kind, store2Item1, store2Item2).Build());
            store1.Upsert(TestEntity.Kind, store1Item3);
            store2.Upsert(TestEntity.Kind, store2Item3);

            var items1 = store1.All(TestEntity.Kind);
            Assert.Equal(3, items1.Count);
            Assert.Equal(store1Item1, items1[store1Item1.Key]);
            Assert.Equal(store1Item2, items1[store1Item2.Key]);
            Assert.Equal(store1Item3, items1[store1Item3.Key]);
            Assert.Equal(store1Item1, store1.Get(TestEntity.Kind, store1Item1.Key));
            Assert.Equal(store1Item2, store1.Get(TestEntity.Kind, store1Item2.Key));
            Assert.Equal(store1Item3, store1.Get(TestEntity.Kind, store1Item3.Key));
            var items2 = store2.All(TestEntity.Kind);
            Assert.Equal(3, items2.Count);
            Assert.Equal(store2Item1, items2[store2Item1.Key]);
            Assert.Equal(store2Item2, items2[store2Item2.Key]);
            Assert.Equal(store2Item3, items2[store2Item3.Key]);
            Assert.Equal(store2Item1, store2.Get(TestEntity.Kind, store2Item1.Key));
            Assert.Equal(store2Item2, store2.Get(TestEntity.Kind, store2Item2.Key));
            Assert.Equal(store2Item3, store2.Get(TestEntity.Kind, store2Item3.Key));
        }

        [Fact]
        public void UpsertRaceConditionAgainstOtherClientWithLowerVersion()
        {
            var key = "key";
            var item1 = new TestEntity(key, 1);
            using (var store2 = MakeStore(TestMode.UNCACHED))
            {
                var action = MakeConcurrentModifier(store2, key, 2, 3, 4);
                var store1 = CreateStoreImplWithUpdateHook(action);
                if (store1 == null)
                {
                    // This feature store implementation doesn't support this test
                    return;
                }
                store1.Init(new DataBuilder().Add(TestEntity.Kind, item1).Build());

                var item10 = item1.WithVersion(10);
                store1.Upsert(TestEntity.Kind, item10);

                Assert.Equal(item10, store1.Get(TestEntity.Kind, key));
            }
        }
        
        [Fact]
        public void UpsertRaceConditionAgainstOtherClientWithHigherVersion()
        {
            var key = "key";
            var item1 = new TestEntity(key, 1);
            using (var store2 = MakeStore(TestMode.UNCACHED))
            {
                var action = MakeConcurrentModifier(store2, key, 3, 4, 5);
                var store1 = CreateStoreImplWithUpdateHook(action);
                if (store1 == null)
                {
                    // This feature store implementation doesn't support this test
                    return;
                }
                store1.Init(new DataBuilder().Add(TestEntity.Kind, item1).Build());

                var item2 = item1.WithVersion(2);
                store1.Upsert(TestEntity.Kind, item2);

                var item5 = item1.WithVersion(5);
                Assert.Equal(item5, store1.Get(TestEntity.Kind, key));
            }
        }

        private Action MakeConcurrentModifier(IFeatureStore store, string key, params int[] versionsToWrite)
        {
            var i = 0;
            return () =>
            {
                if (i < versionsToWrite.Length)
                {
                    store.Upsert(TestEntity.Kind, new TestEntity(key, versionsToWrite[i]));
                    i++;
                }
            };
        }
    }

    public enum TestMode
    {
        CACHED,
        UNCACHED
    };
}
