using ASC.Mail.Models;

using System.Threading.Tasks;

namespace ASC.Mail.Aggregator.Service.Queue.Data
{
    public class TaskData
    {
        public MailBoxData Mailbox { get; private set; }

        public Task Task { get; private set; }

        public TaskData(MailBoxData mailBoxData, Task task)
        {
            Mailbox = mailBoxData;
            Task = task;
        }
    }
}
