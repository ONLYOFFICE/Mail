namespace ASC.Mail.Core.Log;

internal static partial class CrmLinkEngineLogger
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Problem with adding history event to CRM. mailId={id}")]
    public static partial void WarnCrmLinkEngineAddingHistoryEvent(this ILogger<CrmLinkEngine> logger, int id, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "CrmLinkEngine -> AddRelationshipEvents(): message with id = {msgId} has been linked successfully to contact with id = {contactId}")]
    public static partial void InfoCrmLinkEngineAddRelationshipEvents(this ILogger<CrmLinkEngine> logger, int msgId, int contactId);
}
