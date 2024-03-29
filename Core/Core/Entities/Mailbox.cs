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

namespace ASC.Mail.Core.Entities;

public class Mailbox
{
    public int Id { get; set; }
    public string User { get; set; }
    public int Tenant { get; set; }
    public string Address { get; set; }
    public bool Enabled { get; set; }
    public string Password { get; set; }
    public int MsgCountLast { get; set; }
    public long SizeLast { get; set; }
    public string SmtpPassword { get; set; }
    public string Name { get; set; }
    public int LoginDelay { get; set; }
    public bool IsProcessed { get; set; }
    public bool IsRemoved { get; set; }
    public bool IsDefault { get; set; }
    public bool QuotaError { get; set; }
    public bool Imap { get; set; }
    public DateTime BeginDate { get; set; }
    public int OAuthType { get; set; }
    public string OAuthToken { get; set; }
    public string ImapIntervals { get; set; }
    public int SmtpServerId { get; set; }
    public int ServerId { get; set; }
    public string EmailInFolder { get; set; }
    public bool IsTeamlabMailbox { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateChecked { get; set; }
    public DateTime DateUserChecked { get; set; }
    public bool UserOnline { get; set; }
    public DateTime DateLoginDelayExpires { get; set; }
    public DateTime? DateAuthError { get; set; }
}
