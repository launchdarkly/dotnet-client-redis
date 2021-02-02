using System;
using System.Collections.Generic;
using System.Net;
using LaunchDarkly.Client.Utils;
using StackExchange.Redis;

namespace LaunchDarkly.Client.Redis
{
    /// <summary>
    /// Obsolete builder for the Redis data store.
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
    public sealed class RedisFeatureStoreBuilder : IFeatureStoreFactory
    {
        internal ConfigurationOptions RedisConfig { get; private set; } = new ConfigurationOptions();
        internal string Prefix { get; private set; } = RedisComponents.DefaultPrefix;
        internal FeatureStoreCacheConfig Caching { get; private set; } =
            FeatureStoreCacheConfig.Enabled.WithTtl(RedisComponents.DefaultCacheExpiration);
        internal Action UpdateHook { get; private set; }

        internal RedisFeatureStoreBuilder()
        {
            RedisConfig.EndPoints.Add(RedisComponents.DefaultRedisEndPoint);
            RedisConfig.ConnectTimeout = (int)RedisComponents.DefaultConnectTimeout.TotalMilliseconds;
            RedisConfig.SyncTimeout = (int)RedisComponents.DefaultOperationTimeout.TotalMilliseconds;
        }

        /// <summary>
        /// Creates a new <see cref="RedisFeatureStoreBuilder"/> with default properties.
        /// </summary>
        /// <returns>a builder</returns>
        public static RedisFeatureStoreBuilder Default()
        {
            return new RedisFeatureStoreBuilder();
        }

        /// <summary>
        /// Creates a feature store instance based on the currently configured builder.
        /// </summary>
        /// <returns>the feature store</returns>
        public IFeatureStore CreateFeatureStore()
        {
            var core = new RedisFeatureStoreCore(RedisConfig, Prefix, UpdateHook);
            return CachingStoreWrapper.Builder(core).WithCaching(Caching).Build();
        }
        
        /// <summary>
        /// Specifies all Redis configuration options at once.
        /// </summary>
        /// <param name="config">a <see cref="ConfigurationOptions"/> instance</param>
        /// <returns>the same builder instance</returns>
        public RedisFeatureStoreBuilder WithRedisConfiguration(ConfigurationOptions config)
        {
            RedisConfig = config.Clone();
            return this;
        }

        /// <summary>
        /// Specifies a single Redis server by hostname and port.
        /// </summary>
        /// <param name="host">hostname of the Redis server</param>
        /// <param name="port">port of the Redis server</param>
        /// <returns>the same builder instance</returns>
        public RedisFeatureStoreBuilder WithRedisHostAndPort(string host, int port)
        {
            return WithRedisEndPoint(new DnsEndPoint(host, port));
        }

        /// <summary>
        /// Specifies a single Redis server as an EndPoint.
        /// </summary>
        /// <param name="endPoint">location of the Redis server</param>
        /// <returns>the same builder instance</returns>
        public RedisFeatureStoreBuilder WithRedisEndPoint(EndPoint endPoint)
        {
            return WithRedisEndPoints(new List<EndPoint> { endPoint });
        }

        /// <summary>
        /// Specifies a Redis server - and, optionally, other properties including
        /// credentials and database number - using a URI.
        /// </summary>
        /// <param name="uri">the Redis server URI</param>
        /// <returns>the same builder instance</returns>
        public RedisFeatureStoreBuilder WithRedisUri(Uri uri)
        {
            if (uri.Scheme.ToLower() != "redis")
            {
                throw new ArgumentException("URI scheme must be 'redis'");
            }
            WithRedisHostAndPort(uri.Host, uri.Port);
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':');
                if (parts.Length == 2)
                {
                    // Redis doesn't use the username
                    RedisConfig.Password = parts[1];
                }
                else
                {
                    throw new ArgumentException("Credentials must be in the format ':password'");
                }
            }
            if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
            {
                var path = uri.AbsolutePath;
                if (path.StartsWith("/"))
                {
                    path = path.Substring(1);
                }
                var dbIndex = Int32.Parse(path);
                RedisConfig.DefaultDatabase = dbIndex;
            }
            return this;
        }

        /// <summary>
        /// Specifies multiple Redis servers as a list of EndPoints.
        /// </summary>
        /// <param name="endPoints">locations of the Redis servers</param>
        /// <returns>the same builder instance</returns>
        public RedisFeatureStoreBuilder WithRedisEndPoints(IList<EndPoint> endPoints)
        {
            RedisConfig.EndPoints.Clear();
            foreach (var ep in endPoints)
            {
                RedisConfig.EndPoints.Add(ep);
            }
            return this;
        }

        /// <summary>
        /// Specifies which database to use within the Redis server. The default is 0.
        /// </summary>
        /// <param name="database">index of the database to use</param>
        /// <returns>the same builder instance</returns>
        public RedisFeatureStoreBuilder WithDatabaseIndex(int database)
        {
            RedisConfig.DefaultDatabase = database;
            return this;
        }

        /// <summary>
        /// Specifies the maximum time to wait for a connection to the Redis server.
        /// </summary>
        /// <param name="timeout">the timeout interval</param>
        /// <returns>the same builder instance</returns>
        public RedisFeatureStoreBuilder WithConnectTimeout(TimeSpan timeout)
        {
            RedisConfig.ConnectTimeout = (int)timeout.TotalMilliseconds;
            return this;
        }

        /// <summary>
        /// Specifies the maximum time to wait for each synchronous Redis operation to complete.
        /// If you are seeing timeout errors - which could result from either an overburdened
        /// Redis server, or an unusually large operation such as storing a very large feature
        /// flag - you may want to increase this setting.
        /// </summary>
        /// <param name="timeout">the timeout interval</param>
        /// <returns>the same builder instance</returns>
        public RedisFeatureStoreBuilder WithOperationTimeout(TimeSpan timeout)
        {
            RedisConfig.SyncTimeout = (int)timeout.TotalMilliseconds;
            return this;
        }

        /// <summary>
        /// Specifies the namespace prefix for all keys stored in Redis.
        /// </summary>
        /// <param name="prefix">the namespace prefix</param>
        /// <returns>the same builder instance</returns>
        public RedisFeatureStoreBuilder WithPrefix(string prefix)
        {
            Prefix = prefix ?? RedisComponents.DefaultPrefix;
            return this;
        }

        /// <summary>
        /// Specifies whether local caching should be enabled and if so, sets the cache properties. Local
        /// caching is enabled by default; see <see cref="FeatureStoreCacheConfig.Enabled"/>. To disable it, pass
        /// <see cref="FeatureStoreCacheConfig.Disabled"/> to this method.
        /// </summary>
        /// <param name="caching">a <see cref="FeatureStoreCacheConfig"/> object specifying caching parameters</param>
        /// <returns>the same builder instance</returns>
        public RedisFeatureStoreBuilder WithCaching(FeatureStoreCacheConfig caching)
        {
            Caching = caching;
            return this;
        }

        /// <summary>
        /// Equivalent to <code>WithCaching(FeatureStoreCacheConfig.Enabled.WithTtl(cacheExpiration))</code>.
        /// </summary>
        /// <param name="cacheExpiration">the length of time to cache locally</param>
        /// <returns>the same builder instance</returns>
        [Obsolete("Please use WithCaching instead.")]
        public RedisFeatureStoreBuilder WithCacheExpiration(TimeSpan cacheExpiration)
        {
            return WithCaching(cacheExpiration > TimeSpan.Zero ?
                FeatureStoreCacheConfig.Enabled.WithTtl(cacheExpiration) :
                FeatureStoreCacheConfig.Disabled);
        }

        /// <summary>
        /// Allows you to modify any of the configuration options supported by StackExchange.Redis
        /// directly. The current configuration will be passed to your Action, which can modify it
        /// in any way.
        /// </summary>
        /// <example>
        /// <code>
        ///     RedisComponents.RedisFeatureStore()
        ///         .WithRedisConfigChanges((config) => {
        ///             config.Ssl = true;
        ///         })
        /// </code>
        /// </example>
        /// <param name="modifyConfig"></param>
        /// <returns></returns>
        public RedisFeatureStoreBuilder WithRedisConfigChanges(Action<ConfigurationOptions> modifyConfig)
        {
            modifyConfig.Invoke(RedisConfig);
            return this;
        }
    }
}
