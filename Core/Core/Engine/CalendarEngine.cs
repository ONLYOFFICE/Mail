/*
 *
 * (c) Copyright Ascensio System Limited 2010-2020
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
using System.IO;
using System.Linq;
using ASC.Common;
using ASC.Common.Logging;
using ASC.Core;
using ASC.Mail.Models;
using ASC.Mail.Utils;
using Microsoft.Extensions.Options;

namespace ASC.Mail.Core.Engine
{
    [Scope]
    public class CalendarEngine
    {
        private ILog Log { get; }
        private SecurityContext SecurityContext { get; }
        private TenantManager TenantManager { get; }
        private ApiHelper ApiHelper { get; }

        public CalendarEngine(SecurityContext securityContext,
            TenantManager tenantManager,
            ApiHelper apiHelper,
            IOptionsMonitor<ILog> option)
        {
            SecurityContext = securityContext;
            TenantManager = tenantManager;
            ApiHelper = apiHelper;
            Log = option.Get("ASC.Mail.CalendarEngine");
        }

        public void UploadIcsToCalendar(MailBoxData mailBoxData, int calendarId, string calendarEventUid, string calendarIcs,
            string calendarCharset, string calendarContentType, string calendarEventReceiveEmail, string httpContextScheme)
        {
            try
            {
                if (string.IsNullOrEmpty(calendarEventUid) ||
                    string.IsNullOrEmpty(calendarIcs) ||
                    calendarContentType != "text/calendar")
                    return;

                var calendar = MailUtil.ParseValidCalendar(calendarIcs, Log);

                if (calendar == null)
                    return;

                var alienEvent = true;

                var organizer = calendar.Events[0].Organizer;

                if (organizer != null)
                {
                    var orgEmail = calendar.Events[0].Organizer.Value.ToString()
                        .ToLowerInvariant()
                        .Replace("mailto:", "");

                    if (orgEmail.Equals(calendarEventReceiveEmail))
                        alienEvent = false;
                }
                else
                {
                    throw new ArgumentException("calendarIcs.organizer is null");
                }

                if (alienEvent)
                {
                    if (calendar.Events[0].Attendees.Any(
                        a =>
                            a.Value.ToString()
                                .ToLowerInvariant()
                                .Replace("mailto:", "")
                                .Equals(calendarEventReceiveEmail)))
                    {
                        alienEvent = false;
                    }
                }

                if (alienEvent)
                    return;

                TenantManager.SetCurrentTenant(mailBoxData.TenantId);
                SecurityContext.AuthenticateMe(new Guid(mailBoxData.UserId));

                using (var ms = new MemoryStream(EncodingTools.GetEncodingByCodepageName(calendarCharset).GetBytes(calendarIcs)))
                {
                    ApiHelper.UploadIcsToCalendar(calendarId, ms, "calendar.ics", calendarContentType);
                }

                Log.Info("CalendarEngine->UploadIcsToCalendar() has been succeeded");
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("CalendarEngine->UploadIcsToCalendar with \r\n" +
                          "calendarId: {0}\r\n" +
                          "calendarEventUid: '{1}'\r\n" +
                          "calendarIcs: '{2}'\r\n" +
                          "calendarCharset: '{3}'\r\n" +
                          "calendarContentType: '{4}'\r\n" +
                          "calendarEventReceiveEmail: '{5}'\r\n" +
                          "Exception:\r\n{6}\r\n",
                    calendarId, calendarEventUid, calendarIcs, calendarCharset, calendarContentType,
                    calendarEventReceiveEmail, ex.ToString());
            }
        }
    }
}