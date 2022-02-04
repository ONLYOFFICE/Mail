using ASC.Mail.Models;

using System;
using System.Threading.Tasks;

namespace ASC.Mail.Aggregator.Service.Queue.Data
{
    public class TaskData : IDisposable
    {
        public MailBoxData Mailbox { get; private set; }

        public Task Task { get; private set; }

        public TaskData(MailBoxData mailBoxData, Task task)
        {
            Mailbox = mailBoxData;
            Task = task;
        }

        public void Dispose()
        {
            Task?.Dispose();
            Task = null;
            Mailbox = null;
        }
    }
}
