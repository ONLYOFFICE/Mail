namespace ASC.Mail.Core.Core.Enums
{
    public enum MailNotificationState
    {
        SendReceiptError = -2,
        SendMessageError,
        SentMessageSuccess,
        SentIcalRequest,
        SentIcalResponse,
        SentIcalCancel,
        ReadingConfirmed
    }
}
