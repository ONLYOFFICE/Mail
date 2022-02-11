using ASC.Common.Caching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ASC.Mail.ImapSync.Models
{
    public class RedisCachePubSubItem<T>
    {
        public T Object { get; set; }

        public CacheNotifyAction Action { get; set; }
    }
}
