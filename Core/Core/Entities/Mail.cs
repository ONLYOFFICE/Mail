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


using System;
using ASC.Mail.Enums;

namespace ASC.Mail.Core.Entities
{
    public class Mail
    {
        public int Id { get; set; }
        public int MailboxId { get; set; }
        public string User { get; set; }
        public int Tenant { get; set; }
        public string Address { get; set; }
        public string Uidl { get; set; }
        public string Md5 { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Reply { get; set; }
        public string Cc { get; set; }
        public string Bcc { get; set; }
        public string Subject { get; set; }
        public string Introduction { get; set; }
        public bool Importance { get; set; }
        public DateTime DateReceived { get; set; }
        public DateTime DateSent { get; set; }
        public long Size { get; set; }
        public int AttachCount { get; set; }
        public bool Unread { get; set; }
        public bool IsAnswered { get; set; }
        public bool IsForwarded { get; set; }
        public string Stream { get; set; }
        public FolderType Folder { get; set; }
        public FolderType FolderRestore { get; set; }
        public bool Spam { get; set; }
        public bool IsRemoved { get; set; }
        public DateTime TimeModified { get; set; }
        public string MimeMessageId { get; set; }
        public string MimeInReplyTo { get; set; }
        public string ChainId { get; set; }
        public DateTime ChainDate { get; set; }
        public bool IsTextBodyOnly { get; set; }
        public bool HasParseError { get; set; }
        public string CalendarUid { get; set; }
    }
}
