namespace ASC.Mail.Core.Log
{
    internal static partial class CalendarEngineLogger
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "CalendarEngine->UploadIcsToCalendar() has been succeeded")]
        public static partial void InfoCalendarSucceededUploadIcs(this ILogger logger);

        [LoggerMessage(Level = LogLevel.Error, Message = "CalendarEngine->UploadIcsToCalendar with \r\n" +
                      "calendarId: {id}\r\n" +
                      "calendarEventUid: '{uid}'\r\n" +
                      "calendarIcs: '{ics}'\r\n" +
                      "calendarCharset: '{charset}'\r\n" +
                      "calendarContentType: '{contentType}'\r\n" +
                      "calendarEventReceiveEmail: '{receiveEmail}'\r\n" +
                      "Exception:\r\n{ex}\r\n")]
        public static partial void ErrorUploadIcsToCalendar(this ILogger logger, int id, string uid, string ics, string charset, string contentType, string receiveEmail, Exception ex);
    }
}
