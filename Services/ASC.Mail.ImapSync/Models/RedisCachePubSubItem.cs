namespace ASC.Mail.ImapSync.Models
{
    public class RedisCachePubSubItem<T>
    {
        public T Object { get; set; }

        public CacheNotifyAction Action { get; set; }
    }
}
