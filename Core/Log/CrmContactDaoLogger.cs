namespace ASC.Mail.Core.Log
{
    internal static partial class CrmContactDaoLogger
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "GetCrmContactsId(tenandId='{tenant}', userId='{userId}', email='{email}') Exception:\r\n{error}\r\n")]
        public static partial void WarnCrmContactDaoGetCrmContacts(this ILogger logger, int tenant, string userId, string email, string error);
    }
}
