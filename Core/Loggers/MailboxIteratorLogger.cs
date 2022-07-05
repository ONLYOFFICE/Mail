namespace ASC.Mail.Core.Loggers;

internal static partial class MailboxIteratorLogger
{
    [LoggerMessage(Level = LogLevel.Error, Message = "MailboxEngine.GetNextMailboxData(Mailbox id = {id}) failed. Skip it.")]
    public static partial void ErrorMailboxIteratorGetNextMailboxDataFailedSkip(this ILogger logger, int id);

    [LoggerMessage(Level = LogLevel.Error, Message = "MailboxEngine.GetNextMailboxData(Mailbox id = {id}) failed. End seek next.")]
    public static partial void ErrorMailboxIteratorGetNextMailboxDataFailedEndSeekNext(this ILogger logger, int id);
}
