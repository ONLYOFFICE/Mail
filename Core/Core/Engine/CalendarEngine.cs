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



using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Core.Engine;

[Scope]
public class CalendarEngine
{
    private readonly ILogger _log;
    private readonly SecurityContext _securityContext;
    private readonly TenantManager _tenantManager;
    private readonly ApiHelper _apiHelper;

    public CalendarEngine(SecurityContext securityContext,
        TenantManager tenantManager,
        ApiHelper apiHelper,
        ILoggerProvider logProvider)
    {
        _securityContext = securityContext;
        _tenantManager = tenantManager;
        _apiHelper = apiHelper;
        _log = logProvider.CreateLogger("ASC.Mail.CalendarEngine");
    }

    public void UploadIcsToCalendar(MailBoxData mailBoxData, 
        int calendarId, 
        string calendarEventUid, 
        string calendarIcs,
        string calendarCharset, 
        string calendarContentType,
        List<MailAttachmentData> mailAttachments,
        IEnumerable<MimeEntity> mimeAttachments
        )
    {
        try
        {
          if (string.IsNullOrEmpty(calendarEventUid) ||
                string.IsNullOrEmpty(calendarIcs) ||
                calendarContentType != "text/calendar")
                return;

            var calendar = MailUtil.ParseValidCalendar(calendarIcs, _log);

            if (calendar == null)
                return;

            var eventObj = calendar.Events[0];
            var alienEvent = true;

            var organizer = eventObj.Organizer;

            if (organizer != null)
            {
                var orgEmail = eventObj.Organizer.Value.ToString()
                    .ToLowerInvariant()
                    .Replace("mailto:", "");

                if (orgEmail.Equals(mailBoxData.EMail.Address))
                    alienEvent = false;
            }
            else
            {
                throw new ArgumentException("calendarIcs.organizer is null");
            }

            if (alienEvent)
            {
                if (eventObj.Attendees.Any(
                    a =>
                        a.Value.ToString()
                            .ToLowerInvariant()
                            .Replace("mailto:", "")
                            .Equals(mailBoxData.EMail.Address)))
                {
                    alienEvent = false;
                }
            }

            if (alienEvent)
                return;

            _tenantManager.SetCurrentTenant(mailBoxData.TenantId);
            _securityContext.AuthenticateMe(new Guid(mailBoxData.UserId));

            using (var ms = new MemoryStream(EncodingTools.GetEncodingByCodepageName(calendarCharset).GetBytes(calendarIcs)))
            {
                _apiHelper.UploadIcsToCalendar(calendarId, ms, "calendar.ics", calendarContentType,
                    eventObj, mimeAttachments, mailAttachments);
            }

            _log.InfoCalendarSucceededUploadIcs();
        }
        catch (Exception ex)
        {
            _log.ErrorUploadIcsToCalendar(calendarId, calendarEventUid, calendarIcs, calendarCharset, calendarContentType,
                mailBoxData.EMail.Address, ex.ToString());
        }
    }
}
