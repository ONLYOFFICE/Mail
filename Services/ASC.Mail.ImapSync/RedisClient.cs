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
    public class RedisClient
    {
        private readonly IRedisDatabase _redis;

        private readonly RedisChannel _channel;

        private ILog _log;

        public const string RedisClientQueuesKey = "asc:channel:insert:asc.mail.core.entities.cachedtenantusermailbox";


        public RedisClient(IOptionsMonitor<ILog> options, RedisConfiguration redisConfiguration)
        {
            _log = options.Get("ASC.Mail.RedisClient");

            _log.Info(redisConfiguration.ConnectionString);

            _channel = new RedisChannel(RedisClientQueuesKey, RedisChannel.PatternMode.Auto);

            var connectionPoolManager = new RedisCacheConnectionPoolManager(redisConfiguration);

            _redis = new RedisCacheClient(connectionPoolManager, new NewtonsoftSerializer(), redisConfiguration).GetDbFromConfiguration();
        }

        public Task<T> PopFromQueue<T>(string QueueName) where T : class => _redis.ListGetFromRightAsync<T>(QueueName);

        public Task SubscribeQueueKey<T>(Func<T, Task> onNewKey) => _redis.SubscribeAsync(_channel, onNewKey, CommandFlags.FireAndForget);
    }
}
