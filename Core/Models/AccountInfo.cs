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


using System.Collections.Generic;

namespace ASC.Mail.Models
{
    public class AccountInfo
    {
        public int Id { get; set; }

        public string Email { get; set; }

        public bool Enabled { get; set; }

        public bool QuotaError { get; set; }

        public bool AuthError { get; set; }

        public bool OAuthConnection { get; set; }

        public string Name { get; set; }

        public string EMailInFolder { get; set; }

        public bool IsTeamlabMailbox { get; set; }

        public MailSignatureData Signature { get; set; }

        public MailAutoreplyData Autoreply { get; set; }

        public List<MailAddressInfo> Aliases { get; set; }

        public List<MailAddressInfo> Groups { get; set; }

        public bool IsSharedDomainMailbox { get; set; }

        public override string ToString()
        {
            return Name + " <" + Email + ">";
        }

        public AccountInfo(int id, string address, string name, bool enabled,
            bool quotaError, MailBoxData.AuthProblemType authError, MailSignatureData signature, MailAutoreplyData autoreply,
            bool oauthConnection, string emailInFolder, bool isTeamlabMailbox, bool isSharedDomainMailbox)
        {
            Id = id;
            Email = address;
            Name = name;
            Enabled = enabled;
            QuotaError = quotaError;
            AuthError = authError > MailBoxData.AuthProblemType.NoProblems;
            Autoreply = autoreply;
            Signature = signature;
            Aliases = new List<MailAddressInfo>();
            Groups = new List<MailAddressInfo>();
            OAuthConnection = oauthConnection;
            EMailInFolder = emailInFolder;
            IsTeamlabMailbox = isTeamlabMailbox;
            IsSharedDomainMailbox = isSharedDomainMailbox;
        }
    }
}
