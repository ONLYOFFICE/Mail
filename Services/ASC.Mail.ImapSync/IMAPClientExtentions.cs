namespace ASC.Mail.ImapSync
{
    static class ImapClientExtentions
    {
        public static bool Authenticate(this ImapClient imapClient,
            MailBoxData account,
            ILogger _log,
            CancellationToken cancellationToken,
            bool enableUtf8 = true)
        {
            if (imapClient.IsAuthenticated) return true;

            _log.InfoSimpleImapClientAuth(account.Name);

            var secureSocketOptions = SecureSocketOptions.Auto;
            var sslProtocols = SslProtocols.None;

            switch (account.Encryption)
            {
                case EncryptionType.StartTLS:
                    secureSocketOptions = SecureSocketOptions.StartTlsWhenAvailable;
                    sslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;
                    break;
                case EncryptionType.SSL:
                    secureSocketOptions = SecureSocketOptions.SslOnConnect;
                    sslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12 | SslProtocols.Tls13;
                    break;
                case EncryptionType.None:
                    secureSocketOptions = SecureSocketOptions.None;
                    sslProtocols = SslProtocols.None;
                    break;
            }

            _log.DebugSimpleImapConnectTo(account.Server, account.Port, secureSocketOptions.ToString());

            imapClient.SslProtocols = sslProtocols;

            if (!imapClient.IsConnected)
            {
                imapClient.Connect(account.Server, account.Port, secureSocketOptions, cancellationToken);
            }

            try
            {
                if (enableUtf8 && (imapClient.Capabilities & ImapCapabilities.UTF8Accept) != ImapCapabilities.None)
                {
                    _log.DebugSimpleImapEnableUTF8();

                    imapClient.EnableUTF8(cancellationToken);
                }

                if (string.IsNullOrEmpty(account.OAuthToken))
                {
                    _log.DebugSimpleImapAuth(account.Account);

                    imapClient.Authenticate(account.Account, account.Password, cancellationToken);
                }
                else
                {
                    _log.DebugSimpleImapAuthByOAuth(account.Account);

                    var oauth2 = new SaslMechanismOAuth2(account.Account, account.AccessToken);

                    imapClient.Authenticate(oauth2, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"Authentication error: {ex}", true);

                return false;
            }

            _log.DebugSimpleImapLoggedIn();

            return true;
        }
    }
}
