﻿using SmtpClient = MailKit.Net.Smtp.SmtpClient;
namespace ASC.Mail.Clients;

public sealed class DsnSmtpClient : SmtpClient
{
    public DeliveryStatusNotification? Dsn { get; set; }

    public DsnSmtpClient(IProtocolLogger protocolLogPath, DeliveryStatusNotification? dsn = null)
        : base(protocolLogPath)
    {
        Dsn = dsn;
    }

    protected override DeliveryStatusNotification? GetDeliveryStatusNotifications(MimeMessage message,
        MailboxAddress mailbox)
    {
        /*return DeliveryStatusNotification.Delay | 
               DeliveryStatusNotification.Failure | 
               DeliveryStatusNotification.Success;*/
        return Dsn;
    }
}
