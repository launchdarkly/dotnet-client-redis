using System;
using System.Collections.Generic;
using System.Net;
using Xunit;

namespace LaunchDarkly.Client.Redis.Tests
{
#pragma warning disable 0618
    public class RedisFeatureStoreBuilderTest
    {
        [Fact]
        public void DefaultConfigHasDefaultRedisHostAndPort()
        {
            RedisFeatureStoreBuilder builder = new RedisFeatureStoreBuilder();
            Assert.Collection(builder.RedisConfig.EndPoints,
                e => Assert.Equal(new DnsEndPoint("localhost", 6379), e));
        }

        [Fact]
        public void CanSetRedisEndPoint()
        {
            RedisFeatureStoreBuilder builder = new RedisFeatureStoreBuilder();
            DnsEndPoint ep = new DnsEndPoint("test", 9999);
            builder.WithRedisEndPoint(ep);
            Assert.Collection(builder.RedisConfig.EndPoints,
                e => Assert.Equal(ep, e));
        }

        [Fact]
        public void CanSetMultipleRedisEndPoints()
        {
            RedisFeatureStoreBuilder builder = new RedisFeatureStoreBuilder();
            DnsEndPoint ep1 = new DnsEndPoint("test", 9998);
            DnsEndPoint ep2 = new DnsEndPoint("test", 9999);
            builder.WithRedisEndPoints(new List<EndPoint> { ep1, ep2 });
            Assert.Collection(builder.RedisConfig.EndPoints,
                e => Assert.Equal(ep1, e),
                e => Assert.Equal(ep2, e));
        }

        [Fact]
        public void CanSetRedisHostAndPort()
        {
            RedisFeatureStoreBuilder builder = new RedisFeatureStoreBuilder();
            builder.WithRedisHostAndPort("test", 9999);
            Assert.Collection(builder.RedisConfig.EndPoints,
                e => Assert.Equal(new DnsEndPoint("test", 9999), e));
        }

        [Fact]
        public void CanSetMinimalRedisUrl()
        {
            RedisFeatureStoreBuilder builder = new RedisFeatureStoreBuilder();
            builder.WithRedisUri(new Uri("redis://test:9999"));
            Assert.Collection(builder.RedisConfig.EndPoints,
                e => Assert.Equal(new DnsEndPoint("test", 9999), e));
            Assert.Null(builder.RedisConfig.Password);
            Assert.Null(builder.RedisConfig.DefaultDatabase);
        }

        [Fact]
        public void CanSetRedisUrlWithPassword()
        {
            RedisFeatureStoreBuilder builder = new RedisFeatureStoreBuilder();
            builder.WithRedisUri(new Uri("redis://:secret@test:9999"));
            Assert.Collection(builder.RedisConfig.EndPoints,
                e => Assert.Equal(new DnsEndPoint("test", 9999), e));
            Assert.Equal("secret", builder.RedisConfig.Password);
            Assert.Null(builder.RedisConfig.DefaultDatabase);
        }

        [Fact]
        public void CanSetRedisUrlWithDatabase()
        {
            RedisFeatureStoreBuilder builder = new RedisFeatureStoreBuilder();
            builder.WithRedisUri(new Uri("redis://@test:9999/8"));
            Assert.Collection(builder.RedisConfig.EndPoints,
                e => Assert.Equal(new DnsEndPoint("test", 9999), e));
            Assert.Null(builder.RedisConfig.Password);
            Assert.Equal(8, builder.RedisConfig.DefaultDatabase);
        }

        [Fact]
        public void CanSetDatabase()
        {
            RedisFeatureStoreBuilder builder = new RedisFeatureStoreBuilder();
            builder.WithDatabaseIndex(8);
            Assert.Equal(8, builder.RedisConfig.DefaultDatabase);
        }

        [Fact]
        public void CanSetConnectTimeout()
        {
            RedisFeatureStoreBuilder builder = new RedisFeatureStoreBuilder();
            builder.WithConnectTimeout(TimeSpan.FromSeconds(8));
            Assert.Equal(8000, builder.RedisConfig.ConnectTimeout);
        }

        [Fact]
        public void CanSetOperationTimeout()
        {
            RedisFeatureStoreBuilder builder = new RedisFeatureStoreBuilder();
            builder.WithOperationTimeout(TimeSpan.FromSeconds(8));
            Assert.Equal(8000, builder.RedisConfig.SyncTimeout);
        }
    }
#pragma warning restore 0618
}
