namespace ASC.Mail.ImapSync;

[Singletone]
public class RedisClient
{
    private readonly IRedisClient _redis;

    private readonly RedisChannel _channel;

    public const string RedisClientQueuesKey = "asc:channel:insert:asc.mail.core.entities.cachedtenantusermailbox";

    public RedisClient(IRedisClient redisClient)
    {
        _channel = new RedisChannel(RedisClientQueuesKey, RedisChannel.PatternMode.Auto);

        _redis = redisClient;
    }

    public Task<T> PopFromQueue<T>(string QueueName) where T : class => _redis.Db0.ListGetFromRightAsync<T>(QueueName);

    public Task SubscribeQueueKey<T>(Func<T, Task> onNewKey) => _redis.Db0.SubscribeAsync(_channel, onNewKey, CommandFlags.FireAndForget);
}