namespace ASC.Mail.ImapSync;

[Singletone]
public class RedisClient
{
    private readonly IRedisDatabase _redis;

    private readonly RedisChannel _channel;

    public const string RedisClientQueuesKey = "asc:channel:insert:asc.mail.core.entities.cachedtenantusermailbox";

    public RedisClient(IRedisDatabase redis)
    {
        _channel = new RedisChannel(RedisClientQueuesKey, RedisChannel.PatternMode.Auto);

        _redis = redis;
    }

    public Task<T> PopFromQueue<T>(string QueueName) where T : class => _redis.ListGetFromRightAsync<T>(QueueName);

    public Task SubscribeQueueKey<T>(Func<T, Task> onNewKey) => _redis.SubscribeAsync(_channel, onNewKey, CommandFlags.FireAndForget);
}
