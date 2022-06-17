﻿
using FolderType = ASC.Mail.Enums.FolderType;
using UniqueId = MailKit.UniqueId;

namespace ASC.Mail.Core.Log;

internal static partial class MailClientLogger
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "MailClient: Constructor -> Certificate Permit: {certificatePermit}.")]
    public static partial void DebugMailClientCertificatePermit(this ILogger<MailClient> logger, bool certificatePermit);

    [LoggerMessage(Level = LogLevel.Debug, Message = "MailClient: Constructor -> Check Certificate Revocation: {checkCertificateRevocation}.")]
    public static partial void DebugMailClientCheckCertificateRevocation(this ILogger<MailClient> logger, bool checkCertificateRevocation);

    [LoggerMessage(Level = LogLevel.Debug, Message = "CertificateValidationCallback(). Certificate callback: {subject}.")]
    public static partial void DebugMailClientCertificateCallback(this ILogger<MailClient> logger, string subject);

    [LoggerMessage(Level = LogLevel.Debug, Message = "CertificateValidationCallback(). No Ssl policy errors...")]
    public static partial void DebugMailClientNoSslPolicyErrors(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "MailClient -> Cancel()")]
    public static partial void InfoMailClientCancel(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "MailClient -> Dispose()")]
    public static partial void InfoMailClientDispose(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Imap -> Disconnect()")]
    public static partial void DebugMailClientImapDisconnect(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pop -> Disconnect()")]
    public static partial void DebugMailClientPopDisconnect(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Smtp -> Disconnect()")]
    public static partial void DebugMailClientSmtpDisconnect(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "MailClient -> Dispose(MailboxId={mailboxId} MailboxAddres: '{address}')\r\nException: {error}\r\n")]
    public static partial void ErrorMailClientDispose(this ILogger<MailClient> logger, int mailboxId, string address, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Try login IMAP client (Tenant: {tenantId}, MailboxId: {mailboxId}, Address: '{address}')")]
    public static partial void DebugMailClientTryLoginIMAP(this ILogger<MailClient> logger, int tenantId, int mailboxId, string address);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Try login POP3 client (Tenant: {tenantId}, MailboxId: {mailboxId}, Address: '{address}')")]
    public static partial void DebugMailClientTryLoginPop(this ILogger<MailClient> logger, int tenantId, int mailboxId, string address);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Imap: Connect({server}:{port}, {options})")]
    public static partial void DebugMailClientImapConnect(this ILogger<MailClient> logger, string server, int port, string options);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pop: Connect({server}:{port}, {options})")]
    public static partial void DebugMailClientPopConnect(this ILogger<MailClient> logger, string server, int port, string options);

    [LoggerMessage(Level = LogLevel.Information, Message = "Try connect... to ({server}:{port}) timeout {timeout} miliseconds")]
    public static partial void InfoMailClientTryConnectTo(this ILogger<MailClient> logger, int timeout, string server, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "Imap: Failed connect: Timeout.")]
    public static partial void InfoMailClientImapConnectTimeout(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pop: Failed connect: Timeout.")]
    public static partial void InfoMailClientPopConnectTimeout(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Imap: Successfull connection. Working on!")]
    public static partial void DebugMailClientImapConnectSuccessfull(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pop: Successfull connection. Working on!")]
    public static partial void DebugMailClientPopConnectSuccessfull(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Imap: Authentication({account}).")]
    public static partial void DebugMailClientImapAuthentication(this ILogger<MailClient> logger, string account);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pop: Authentication({account}).")]
    public static partial void DebugMailClientPopAuthentication(this ILogger<MailClient> logger, string account);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Smtp: Authentication({account}).")]
    public static partial void DebugMailClientSmtpAuthentication(this ILogger<MailClient> logger, string account);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Imap: AuthenticationByOAuth({account}).")]
    public static partial void DebugMailClientImapAuthByOAuth(this ILogger<MailClient> logger, string account);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pop: AuthenticationByOAuth({account}).")]
    public static partial void DebugMailClientPopAuthByOAuth(this ILogger<MailClient> logger, string account);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Smtp: AuthenticationByOAuth({account}).")]
    public static partial void DebugMailClientSmtpAuthByOAuth(this ILogger<MailClient> logger, string account);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Imap: Failed authentication: Timeout.")]
    public static partial void DebugMailClientImapAuthTimeout(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pop: Failed authentication: Timeout.")]
    public static partial void DebugMailClientPopAuthTimeout(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Imap: Successfull authentication.")]
    public static partial void DebugMailClientImapAuthSuccessfull(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pop: Successfull authentication.")]
    public static partial void DebugMailClientPopAuthSuccessfull(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Imap: EnableUTF8().")]
    public static partial void DebugMailClientImapEnableUTF8(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pop: EnableUTF8().")]
    public static partial void DebugMailClientPopEnableUTF8(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Imap: Failed ENABLE_UTF8: Timeout.")]
    public static partial void DebugMailClientImapEnableUTF8Timeout(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pop: Failed ENABLE_UTF8: Timeout.")]
    public static partial void DebugMailClientPopEnableUTF8Timeout(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Imap: Successfull ENABLE_UTF8.")]
    public static partial void DebugMailClientImapEnableUTF8Successfull(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pop: Successfull ENABLE_UTF8.")]
    public static partial void DebugMailClientPopEnableUTF8Successfull(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "[folder] x '{folderName}' (skipped)")]
    public static partial void InfoMailClientFolderSkipped(this ILogger<MailClient> logger, string folderName);

    [LoggerMessage(Level = LogLevel.Information, Message = "[folder] >> '{folderName}' (fId={folderType}) {tags}")]
    public static partial void InfoMailClientFolder(this ILogger<MailClient> logger, string folderName, FolderType folderType, string tags);

    [LoggerMessage(Level = LogLevel.Error, Message = "Open faild: {folderName} Exception: {error}")]
    public static partial void ErrorMailClientOpenFolder(this ILogger<MailClient> logger, string folderName, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Limit of maximum number messages per session is exceeded!")]
    public static partial void DebugMailClientImapLimitMessages(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "GetImapFoldersAsync()")]
    public static partial void DebugMailClientGetImapFoldersAsync(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "GetImapSubFolders: {folderName} Exception: {error}")]
    public static partial void ErrorMailClientGetImapSubFolders(this ILogger<MailClient> logger, string folderName, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Folder '{folderName}' UIDVALIDITY changed - need refresh folder")]
    public static partial void DebugMailClientUidValidityChanged(this ILogger<MailClient> logger, string folderName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Folder '{folderName}' Save UIDVALIDITY = {uidValidity}")]
    public static partial void DebugMailClientUidValiditySave(this ILogger<MailClient> logger, string folderName, uint uidValidity);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Try get message {unicueId}. Size: {size}")]
    public static partial void DebugMailClientTryGetMessage(this ILogger<MailClient> logger, UniqueId unicueId, uint? size);

    [LoggerMessage(Level = LogLevel.Debug, Message = "BytesTransferred = {size}")]
    public static partial void DebugMailClientBytesTransferred(this ILogger<MailClient> logger, uint? size);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skip message (Date = {messageDate}) on BeginDate = {beginDate}")]
    public static partial void DebugMailClientSkipMessage(this ILogger<MailClient> logger, DateTimeOffset messageDate, DateTime beginDate);

    [LoggerMessage(Level = LogLevel.Error, Message = "ProcessMessages() Tenant={tenantId} User='{userId}' Account='{address}', MailboxId={mailboxId}, UID={unicueId} Exception:\r\n{error}\r\n")]
    public static partial void ErrorMailClientProcessMessages(this ILogger<MailClient> logger, int tenantId, string userId, string address, int mailboxId, UniqueId unicueId, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "ProcessMessages() Tenant={tenantId} User='{userId}' Account='{address}', MailboxId={mailboxId}, MessageIndex={key}, UIDL='{value}' Exception:\r\n{error}\r\n")]
    public static partial void ErrorMailClientProcessPopMessages(this ILogger<MailClient> logger, int tenantId, string userId, string address, int mailboxId, int key, string value, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GetFolderUids() Exception: {error}")]
    public static partial void WarnMailClientGetFolderUidsException(this ILogger<MailClient> logger, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "GetMessagesSummaryInfo() Exception: {error}")]
    public static partial void WarnMailClientGetMessagesSummaryInfoException(this ILogger<MailClient> logger, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "AppendCopyToSentFolder(Mailbox: '{address}', Tenant: {tenantId}, User: '{userId}') succeed! (uid:{uidId})")]
    public static partial void InfoMailClientAppendCopyToSentFolder(this ILogger<MailClient> logger, string address, int tenantId, string userId, uint uidId);

    [LoggerMessage(Level = LogLevel.Error, Message = "AppendCopyToSentFolder(Mailbox: '{address}', Tenant: {tenantId}, User: '{userId}') failed!")]
    public static partial void ErrorMailClientAppendCopyToSentFolder(this ILogger<MailClient> logger, string address, int tenantId, string userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "AppendCopyToSentFolder(Mailbox: '{address}', Tenant: {tenantId}, User: '{userId}'): Skip - sent-folder not found")]
    public static partial void DebugMailClientAppendCopyToSentFolder(this ILogger<MailClient> logger, string address, int tenantId, string userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "AppendCopyToSentFolder(Mailbox: '{address}', Tenant: {tenantId}, User: '{userId}'): Exception:\r\n{error}\r\n")]
    public static partial void ErrorMailClientAppendCopyToSentFolder(this ILogger<MailClient> logger, string address, int tenantId, string userId, string error);

    [LoggerMessage(Level = LogLevel.Debug, Message = "New messages not found.\r\n")]
    public static partial void DebugMailClientMsgsNotFound(this ILogger<MailClient> logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {count} new messages.\r\n")]
    public static partial void DebugMailClientFoundMsgs(this ILogger<MailClient> logger, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Processing new message\tUID: {key}\tUIDL: {value}\t")]
    public static partial void DebugMailClientProcessingMsgs(this ILogger<MailClient> logger, int key, string value);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Account '{address}' uids order is DESC")]
    public static partial void DebugMailClientUidsOrderDESC(this ILogger<MailClient> logger, string address);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Account '{address}' uids order is ASC")]
    public static partial void DebugMailClientUidsOrderASC(this ILogger<MailClient> logger, string address);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Calculating order skipped! Account '{address}' uids order is DESC")]
    public static partial void WarnMailClientCalculatingOrderSkipped(this ILogger<MailClient> logger, string address);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Smtp: Connect({server}:{port}, {options})")]
    public static partial void DebugMailClientSmtpConnect(this ILogger<MailClient> logger, string server, int port, string options);
}
