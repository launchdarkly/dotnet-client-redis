using System;
using System.Net;

namespace LaunchDarkly.Client.Redis
{
    /// <summary>
    /// Obsolete entry point for the Redis integration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is retained in version 1.2 of the library for backward compatibility. For the new
    /// preferred way to configure the Redis integration, see <see cref="LaunchDarkly.Client.Integrations.Redis"/>.
    /// Updating to the latter now will make it easier to adopt version 6.0 of the LaunchDarkly .NET SDK, since
    /// an identical API is used there (except for the base namespace).
    /// </para>
    /// </remarks>
    [Obsolete("Use LaunchDarkly.Client.Integrations.Redis")]
    public abstract class RedisComponents
    {
        /// <summary>
        /// The default location of the Redis server: <c>localhost:6379</c>
        /// </summary>
        public static readonly EndPoint DefaultRedisEndPoint = new DnsEndPoint("localhost", 6379);

        /// <summary>
        /// The default value for <see cref="RedisFeatureStoreBuilder.WithPrefix"/>.
        /// </summary>
        public static readonly string DefaultPrefix = "launchdarkly";

        /// <summary>
        /// The default value for <see cref="RedisFeatureStoreBuilder.WithCacheExpiration"/>.
        /// </summary>
        public static readonly TimeSpan DefaultCacheExpiration = TimeSpan.FromSeconds(15);

        /// <summary>
        /// The default value for <see cref="RedisFeatureStoreBuilder.WithConnectTimeout(TimeSpan)"/>.
        /// </summary>
        public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The default value for <see cref="RedisFeatureStoreBuilder.WithOperationTimeout(TimeSpan)"/>.
        /// </summary>
        public static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Creates a new <see cref="RedisFeatureStoreBuilder"/>.
        /// </summary>
        /// <returns></returns>
        public static RedisFeatureStoreBuilder RedisFeatureStore()
        {
            return new RedisFeatureStoreBuilder();
        }
    }
}
