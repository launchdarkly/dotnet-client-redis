using System;
using Newtonsoft.Json;

namespace LaunchDarkly.Client.SharedTests.FeatureStore
{
    /// <summary>
    /// Simple implementation of IVersionedData that feature store tests can use instead
    /// of LaunchDarkly.Client's real model classes. Feature stores should be able to
    /// persist anything that implements IVersionedData and is JSON-able.
    /// </summary>
    public class TestEntity : IVersionedData
    {
        public static readonly TestEntityKind Kind = new TestEntityKind();

        [JsonProperty(PropertyName = "key")]
        public string Key { get; set; }
        [JsonProperty(PropertyName = "version")]
        public int Version { get; set; }
        [JsonProperty(PropertyName = "deleted")]
        public bool Deleted { get; set; }
        [JsonProperty(PropertyName = "value")]
        public string Value { get; set; }

        public TestEntity() { }

        public TestEntity(string key, int version, bool deleted = false, string value = null)
        {
            Key = key;
            Version = version;
            Deleted = deleted;
            Value = value;
        }

        public TestEntity WithVersion(int newVersion)
        {
            return new TestEntity(Key, newVersion, Deleted, Value);
        }

        public TestEntity WithValue(string newValue)
        {
            return new TestEntity(Key, Version, Deleted, newValue);
        }

        public TestEntity NextVersion()
        {
            return WithVersion(Version + 1);
        }

        public override bool Equals(object obj)
        {
            if (obj is TestEntity o)
            {
                return Key == o.Key && Version == o.Version && Deleted == o.Deleted && Value == o.Value;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return new { Key, Version, Deleted, Value }.GetHashCode();
        }
    }

    /// <summary>
    /// Another one like TestEntity, for tests that need more than one kind of thing.
    /// </summary>
    public class OtherTestEntity : IVersionedData
    {
        public static readonly OtherTestEntityKind Kind = new OtherTestEntityKind();

        [JsonProperty(PropertyName = "key")]
        public string Key { get; set; }
        [JsonProperty(PropertyName = "version")]
        public int Version { get; set; }
        [JsonProperty(PropertyName = "deleted")]
        public bool Deleted { get; set; }

        public OtherTestEntity() { }

        public OtherTestEntity(string key, int version, bool deleted = false)
        {
            Key = key;
            Version = version;
            Deleted = deleted;
        }

        public override bool Equals(object obj)
        {
            if (obj is OtherTestEntity o)
            {
                return Key == o.Key && Version == o.Version && Deleted == o.Deleted;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return new { Key, Version, Deleted }.GetHashCode();
        }
    }

    public class TestEntityKind : VersionedDataKind<TestEntity>
    {
        public override string GetNamespace()
        {
            return "test";
        }

        public override TestEntity MakeDeletedItem(string key, int version)
        {
            return new TestEntity(key, version, true);
        }

        public override Type GetItemType()
        {
            return typeof(TestEntity);
        }

        public override string GetStreamApiPath()
        {
            throw new NotImplementedException();
        }
    }

    public class OtherTestEntityKind : VersionedDataKind<OtherTestEntity>
    {
        public override string GetNamespace()
        {
            return "other";
        }

        public override OtherTestEntity MakeDeletedItem(string key, int version)
        {
            return new OtherTestEntity(key, version, true);
        }

        public override Type GetItemType()
        {
            return typeof(OtherTestEntity);
        }

        public override string GetStreamApiPath()
        {
            throw new NotImplementedException();
        }
    }
}
