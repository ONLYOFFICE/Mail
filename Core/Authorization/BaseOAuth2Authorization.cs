/*
 *
 * (c) Copyright Ascensio System Limited 2010-2018
 *
 * This program is freeware. You can redistribute it and/or modify it under the terms of the GNU 
 * General Public License (GPL) version 3 as published by the Free Software Foundation (https://www.gnu.org/copyleft/gpl.html). 
 * In accordance with Section 7(a) of the GNU GPL its Section 15 shall be amended to the effect that 
 * Ascensio System SIA expressly excludes the warranty of non-infringement of any third-party rights.
 *
 * THIS PROGRAM IS DISTRIBUTED WITHOUT ANY WARRANTY; WITHOUT EVEN THE IMPLIED WARRANTY OF MERCHANTABILITY OR
 * FITNESS FOR A PARTICULAR PURPOSE. For more details, see GNU GPL at https://www.gnu.org/copyleft/gpl.html
 *
 * You can contact Ascensio System SIA by email at sales@onlyoffice.com
 *
 * The interactive user interfaces in modified source and object code versions of ONLYOFFICE must display 
 * Appropriate Legal Notices, as required under Section 5 of the GNU GPL version 3.
 *
 * Pursuant to Section 7 § 3(b) of the GNU GPL you must retain the original ONLYOFFICE logo which contains 
 * relevant author attributions when distributing the software. If the display of the logo in its graphic 
 * form is not reasonably feasible for technical reasons, you must include the words "Powered by ONLYOFFICE" 
 * in every copy of the program you distribute. 
 * Pursuant to Section 7 § 3(e) we decline to grant you any rights under trademark law for use of our trademarks.
 *
*/



namespace ASC.Mail.Authorization;

[Scope]
public class BaseOAuth2Authorization<T> where T : Consumer, ILoginProvider, new()
{
    private readonly ILogger _log;

    private readonly T _loginProvider;
    private readonly OAuth20TokenHelper _oAuth20TokenHelper;
    private readonly ConsumerFactory ConsumerFactory;

    public string ClientId => _loginProvider.ClientID;

    public string ClientSecret => _loginProvider.ClientSecret;

    public string RedirectUrl => _loginProvider.RedirectUri;

    public string RefreshUrl => _loginProvider.AccessTokenUrl;

    public BaseOAuth2Authorization(
        ILogger log,
        ConsumerFactory consumerFactory,
        OAuth20TokenHelper oAuth20TokenHelper)
    {
        ConsumerFactory = consumerFactory;
        _oAuth20TokenHelper = oAuth20TokenHelper;

        _log = log;
        _loginProvider = ConsumerFactory.Get<T>();

        try
        {
            if (String.IsNullOrEmpty(_loginProvider.ClientID))
                throw new ArgumentNullException("ClientId");

            if (String.IsNullOrEmpty(_loginProvider.ClientSecret))
                throw new ArgumentNullException("ClientSecret");

            if (String.IsNullOrEmpty(_loginProvider.RedirectUri))
                throw new ArgumentNullException("RedirectUrl");
        }
        catch (Exception ex)
        {
            log.ErrorBaseOAuth2AuthorizationGoogleOAuth(ex.ToString());
        }
    }

    public OAuth20Token RequestAccessToken(string refreshToken)
    {
        var token = new OAuth20Token
        {
            ClientID = ClientId,
            ClientSecret = ClientSecret,
            RedirectUri = RedirectUrl,
            RefreshToken = refreshToken,
        };

        try
        {
            return _oAuth20TokenHelper.RefreshToken<T>(ConsumerFactory, token);
        }
        catch (Exception ex)
        {
            _log.ErrorBaseOAuth2Authorization(ex.ToString());
            return null;
        }
    }
}
