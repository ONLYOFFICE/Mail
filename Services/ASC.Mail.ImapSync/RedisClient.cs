using ASC.Common;
using ASC.Common.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using StackExchange.Redis.Extensions.Core.Abstractions;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.Core.Implementations;
using StackExchange.Redis.Extensions.Newtonsoft;
using System;
using System.Threading.Tasks;

namespace ASC.Mail.ImapSync
{
    [Singletone]
    public class RedisFactory
    {
        private RedisCacheConnectionPoolManager _redisCacheConnectionPoolManager;

        private RedisConfiguration _redisConfiguration;

        public RedisFactory(RedisConfiguration redisConfiguration)
        {
            _redisCacheConnectionPoolManager = new RedisCacheConnectionPoolManager(redisConfiguration);

            _redisConfiguration=redisConfiguration;
        }

        public RedisClient GetRedisClient()=> new RedisClient(_redisConfiguration, _redisCacheConnectionPoolManager);
    }

    public class RedisClient
    {
        private readonly IRedisDatabase _redis;

        private readonly RedisChannel _channel;

        public const string RedisClientQueuesKey = "asc:channel:insert:asc.mail.core.entities.cachedtenantusermailbox";

        public RedisClient(RedisConfiguration redisConfiguration, RedisCacheConnectionPoolManager redisCacheConnectionPoolManager)
        {
            _channel = new RedisChannel(RedisClientQueuesKey, RedisChannel.PatternMode.Auto);

            _redis = new RedisCacheClient(redisCacheConnectionPoolManager, new NewtonsoftSerializer(), redisConfiguration).GetDbFromConfiguration();
        }

        public Task<T> PopFromQueue<T>(string QueueName) where T : class => _redis.ListGetFromRightAsync<T>(QueueName);

        public Task SubscribeQueueKey<T>(Func<T, Task> onNewKey) => _redis.SubscribeAsync(_channel, onNewKey, CommandFlags.FireAndForget);
    }
}
