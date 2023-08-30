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



using Ical.Net.CalendarComponents;
using Microsoft.Extensions.Logging;
using RestSharp;
using AuthenticationException = System.Security.Authentication.AuthenticationException;
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Utils;

[Scope]
public class ApiHelper
{
    private const int MAIL_CRM_HISTORY_CATEGORY = -3;
    private const string ERR_MESSAGE = "Error retrieving response. Check inner details for more info.";

    private UriBuilder _baseUrl;
    private string _token;

    private readonly ILogger _log;
    private readonly SecurityContext _securityContext;
    private readonly TenantManager _tenantManager;
    private readonly CoreSettings _coreSettings;
    private readonly ApiDateTimeHelper _apiDateTimeHelper;
    private readonly string _scheme;
    private readonly MailSettings _mailSettings;
    private readonly HttpContext _httpContext;

    private Tenant Tenant => _tenantManager.GetCurrentTenant(_httpContext);

    /// <summary>
    /// Constructor of class ApiHelper
    /// </summary>
    /// <exception cref="ApiHelperException">Exception happens when scheme is invalid.</exception>>
    public ApiHelper(
        IHttpContextAccessor httpContextAccessor,
        SecurityContext securityContext,
        TenantManager tenantManager,
        CoreSettings coreSettings,
        ApiDateTimeHelper apiDateTimeHelper,
        MailSettings mailSettings,
        ILoggerProvider logProvider)
        : this(securityContext, tenantManager, coreSettings, apiDateTimeHelper, mailSettings, logProvider)
    {
        if (httpContextAccessor != null || httpContextAccessor.HttpContext != null)
        {
            _httpContext = httpContextAccessor.HttpContext;
        }
    }

    public ApiHelper(
        SecurityContext securityContext,
        TenantManager tenantManager,
        CoreSettings coreSettings,
        ApiDateTimeHelper apiDateTimeHelper,
        MailSettings mailSettings,
        ILoggerProvider logProvider)
    {
        _mailSettings = mailSettings;
        _log = logProvider.CreateLogger("ASC.Mail.ApiHelper");
        _securityContext = securityContext;
        _tenantManager = tenantManager;
        _coreSettings = coreSettings;
        _apiDateTimeHelper = apiDateTimeHelper;
        _scheme = mailSettings.Defines.DefaultApiSchema ?? Uri.UriSchemeHttp;

        if (!_scheme.Equals(Uri.UriSchemeHttps) && !_scheme.Equals(Uri.UriSchemeHttp))
            throw new ApiHelperException("ApiHelper: url scheme not setup", HttpStatusCode.InternalServerError, "");

        if (!_scheme.Equals(Uri.UriSchemeHttps) || !_mailSettings.Defines.SslCertificatesErrorsPermit)
            return;

        ServicePointManager.ServerCertificateValidationCallback =
            (sender, certificate, chain, sslPolicyErrors) => true;
    }

    private void Setup()
    {
        var user = _securityContext.CurrentAccount;

        var httpCon = _httpContext != null
                      ? string.Format("not null and UrlRewriter = {0}, RequestUrl = {1}",
                        _httpContext.Request.GetUrlRewriter().ToString(),
                        _httpContext.Request.Url().ToString())
                      : "null";

        _log.DebugApiHelperSetup(Tenant.Id, user.ID, user.IsAuthenticated, _scheme, httpCon);

        if (!user.IsAuthenticated)
            throw new AuthenticationException("User not authenticated");

        var tempUrl = _mailSettings.Aggregator.ApiPrefix;

        var ubBase = new UriBuilder
        {
            Scheme = _scheme,
            Host = Tenant.GetTenantDomain(_coreSettings, false)
        };

        if (!string.IsNullOrEmpty(_mailSettings.Aggregator.ApiVirtualDirPrefix))
            tempUrl = string.Format("{0}/{1}", _mailSettings.Aggregator.ApiVirtualDirPrefix.Trim('/'), tempUrl);

        if (!string.IsNullOrEmpty(_mailSettings.Aggregator.ApiHost))
            ubBase.Host = _mailSettings.Aggregator.ApiHost;

        if (!string.IsNullOrEmpty(_mailSettings.Aggregator.ApiPort))
            ubBase.Port = int.Parse(_mailSettings.Aggregator.ApiPort);

        ubBase.Path = tempUrl;

        _baseUrl = ubBase;

        _token = _securityContext.AuthenticateMe(_securityContext.CurrentAccount.ID);
    }

    public RestResponse Execute(RestRequest request)
    {
        Setup();

        _log.DebugApiHelperExecuteRequest(_baseUrl.Uri, request.Resource);

        var options = new RestClientOptions(_baseUrl.Uri)
        {
            RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
        };

        var client = new RestClient(options);

        request.AddHeader("Authorization", _token);

        var response = client.ExecuteSafe(request);

        _log.DebugApiHelperResponseCode(response.StatusCode);

        if (response.ErrorException is ApiHelperException)
            return response;

        if (response.ErrorException != null)
            throw new ApplicationException(ERR_MESSAGE, response.ErrorException);

        return response;
    }

    public DefineConstants.TariffType GetTenantTariff(int tenantOverdueDays)
    {
        _log.DebugApiHelperCreateTariffRequest();
        var request = new RestRequest("portal/tariff.json", Method.Get);

        request.AddHeader("Payment-Info", "false");

        _log.DebugApiHelperExecuteTariffRequest();
        var response = Execute(request);

        if (response.StatusCode == HttpStatusCode.PaymentRequired)
        {
            _log.DebugApiHelperPaymentRequired();
            return DefineConstants.TariffType.LongDead;
        }


        if (response.ResponseStatus != ResponseStatus.Completed ||
            (response.StatusCode != HttpStatusCode.Created &&
             response.StatusCode != HttpStatusCode.OK))
        {
            _log.DebugApiHelperCannotGetTariff(response.StatusCode);
            throw new ApiHelperException("Get tenant tariff failed.", response.StatusCode, response.Content);
        }

        var json = JObject.Parse(response.Content);

        TariffState state;

        Enum.TryParse(json["response"]["state"].ToString(), out state);

        DefineConstants.TariffType result;

        if (state < TariffState.NotPaid)
        {
            result = DefineConstants.TariffType.Active;
        }
        else
        {
            var dueDate = DateTime.Parse(json["response"]["dueDate"].ToString());

            var delayDateString = json["response"]["delayDueDate"].ToString();

            var delayDueDate = DateTime.Parse(delayDateString);

            var maxDateStr = DateTime.MaxValue.CutToSecond().ToString(CultureInfo.InvariantCulture);

            delayDateString = delayDueDate.CutToSecond().ToString(CultureInfo.InvariantCulture);

            result = (!delayDateString.Equals(maxDateStr) ? delayDueDate : dueDate)
                         .AddDays(tenantOverdueDays) <= DateTime.UtcNow
                         ? DefineConstants.TariffType.LongDead
                         : DefineConstants.TariffType.Overdue;
        }

        return result;
    }

    public void RemoveTeamlabMailbox(int mailboxId)
    {
        var request = new RestRequest("mailserver/mailboxes/remove/{id}", Method.Delete);

        request.AddUrlSegment("id", mailboxId.ToString(CultureInfo.InvariantCulture));

        var response = Execute(request);

        if (response.ResponseStatus != ResponseStatus.Completed ||
            (response.StatusCode != HttpStatusCode.Created &&
             response.StatusCode != HttpStatusCode.OK))
        {
            throw new ApiHelperException("Delete teamlab mailbox failed.", response.StatusCode, response.Content);
        }
    }

    public void SendMessage(MailMessageData message, bool isAutoreply = false)
    {
        var request = new RestRequest("mail/messages/simpleSend.json", Method.Put);

        request.AddParameter("id", message.Id);

        request.AddParameter("from", message.From);

        request.AddParameter("to", message.To);

        if (!string.IsNullOrEmpty(message.Cc)) request.AddParameter("cc", message.Cc);

        if (!string.IsNullOrEmpty(message.Bcc)) request.AddParameter("bcc", message.Bcc);

        request.AddParameter("subject", message.Subject);

        request.AddParameter("body", message.HtmlBody);

        request.AddParameter("mimeReplyToId", message.MimeReplyToId);

        request.AddParameter("importance", message.Important);

        if (message.TagIds != null && message.TagIds.Count != 0)
            request.AddParameter("tags", JsonConvert.SerializeObject(message.TagIds));

        if (message.Attachments != null && message.Attachments.Count != 0)
            request.AddParameter("attachments", JsonConvert.SerializeObject(message.Attachments));

        if (!string.IsNullOrEmpty(message.CalendarEventIcs))
            request.AddParameter("calendarIcs", message.CalendarEventIcs);

        request.AddParameter("isAutoreply", isAutoreply);

        request.AddHeader("Accept", "application/json");

        var response = Execute(request);

        if (response.ResponseStatus == ResponseStatus.Completed &&
            (response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK))
            return;

        if (response.ErrorException is ApiHelperException)
            throw response.ErrorException;

        throw new ApiHelperException("Send message to api failed.", response.StatusCode, response.Content);
    }

    public List<string> SearchEmails(string term)
    {
        var request = new RestRequest("mail/emails/search.json", Method.Get);

        request.AddParameter("term", term);

        var response = Execute(request);

        if (response.ResponseStatus != ResponseStatus.Completed ||
            (response.StatusCode != HttpStatusCode.Created &&
             response.StatusCode != HttpStatusCode.OK))
        {
            if (response.ErrorException is ApiHelperException)
            {
                throw response.ErrorException;
            }

            throw new ApiHelperException("Search Emails failed.", response.StatusCode, response.Content);
        }

        var json = JObject.Parse(response.Content);

        return json["response"].ToObject<List<string>>();
    }

    public List<string> SearchCrmEmails(string term, int maxCount)
    {
        var request = new RestRequest("crm/contact/simple/byEmail.json", Method.Get);

        request.AddParameter("term", term)
            .AddParameter("maxCount", maxCount.ToString());

        var response = Execute(request);

        var crmEmails = new List<string>();

        var json = JObject.Parse(response.Content);

        var contacts = json["response"] as JArray;

        if (contacts == null)
            return crmEmails;

        foreach (var contact in contacts)
        {
            var commonData = contact["contact"]["commonData"] as JArray;

            if (commonData == null)
                continue;

            var emails = commonData.Where(d => int.Parse(d["infoType"].ToString()) == 1).Select(d => (string)d["data"]).ToList();

            if (!emails.Any())
                continue;

            var displayName = contact["contact"]["displayName"].ToString();

            if (displayName.IndexOf(term, StringComparison.OrdinalIgnoreCase) > -1)
            {
                crmEmails.AddRange(emails.Select(e => MailUtil.CreateFullEmail(displayName, e)));
            }
            else
            {
                crmEmails.AddRange(emails
                    .Where(e => e.IndexOf(term, StringComparison.OrdinalIgnoreCase) > -1)
                    .Select(e => MailUtil.CreateFullEmail(displayName, e)));
            }
        }

        return crmEmails;
    }

    public List<string> SearchPeopleEmails(string term, int startIndex, int count)
    {
        var request = new RestRequest("people/filter.json?filterValue={FilterValue}&StartIndex={StartIndex}&Count={Count}", Method.Get);

        request.AddParameter("FilterValue", term, ParameterType.UrlSegment)
            .AddParameter("StartIndex", startIndex.ToString(), ParameterType.UrlSegment)
            .AddParameter("Count", count.ToString(), ParameterType.UrlSegment);

        var response = Execute(request);

        var peopleEmails = new List<string>();

        var json = JObject.Parse(response.Content);

        var contacts = json["response"] as JArray;

        if (contacts == null)
            return peopleEmails;

        foreach (var contact in contacts)
        {
            var displayName = contact["displayName"].ToString();

            var emails = new List<string>();

            var email = contact["email"].ToString();

            if (!string.IsNullOrEmpty(email))
                emails.Add(email);

            var contactData = contact["contacts"] as JArray;

            if (contactData != null)
            {
                emails.AddRange(contactData.Where(d => d["type"].ToString() == "mail").Select(d => (string)d["value"]).ToList());
            }

            if (displayName.IndexOf(term, StringComparison.OrdinalIgnoreCase) > -1)
            {
                peopleEmails.AddRange(emails.Select(e => MailUtil.CreateFullEmail(displayName, e)));
            }
            else
            {
                peopleEmails.AddRange(emails
                    .Where(e => e.IndexOf(term, StringComparison.OrdinalIgnoreCase) > -1)
                    .Select(e => MailUtil.CreateFullEmail(displayName, e)));
            }
        }

        return peopleEmails;
    }

    public void AddToCrmHistory(MailMessageData message, CrmContactData entity, IEnumerable<object> fileIds)
    {
        var request = new RestRequest("crm/history.json", Method.Post);

        var contentJson = string.Format("{{ message_id : {0} }}", message.Id);

        request.AddParameter("content", contentJson)
               .AddParameter("categoryId", MAIL_CRM_HISTORY_CATEGORY)
               .AddParameter("created", _apiDateTimeHelper.Get(message.Date).ToString());

        var crmEntityType = entity.EntityTypeName;

        if (crmEntityType == CrmContactData.CrmEntityTypeNames.CONTACT)
        {
            request.AddParameter("contactId", entity.Id)
                   .AddParameter("entityId", 0);
        }
        else
        {
            if (crmEntityType != CrmContactData.CrmEntityTypeNames.CASE
                && crmEntityType != CrmContactData.CrmEntityTypeNames.OPPORTUNITY)
                throw new ArgumentException(String.Format("Invalid crm entity type: {0}", crmEntityType));

            request.AddParameter("contactId", 0)
                   .AddParameter("entityId", entity.Id)
                   .AddParameter("entityType", crmEntityType);
        }

        if (fileIds != null)
        {
            fileIds.ToList().ForEach(
                id => request.AddParameter("fileId[]", id.ToString()));
        }

        var response = Execute(request);

        if (response.ResponseStatus == ResponseStatus.Completed &&
            (response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK))
            return;

        if (response.ErrorException is ApiHelperException)
            throw response.ErrorException;

        throw new ApiHelperException("Add message to crm history failed.", response.StatusCode, response.Content);
    }

    public object UploadToCrm(Stream fileStream, string filename, string contentType,
                                  CrmContactData entity)
    {
        if (entity == null)
            throw new ArgumentNullException("entity");

        var request = new RestRequest("crm/{entityType}/{entityId}/files/upload.json", Method.Post);

        request.AddUrlSegment("entityType", entity.EntityTypeName)
            .AddUrlSegment("entityId", entity.Id.ToString())
            .AddParameter("storeOriginalFileFlag", false);

        request.AddFile(filename, () => fileStream, filename, contentType);

        var response = Execute(request);

        if (response.ResponseStatus != ResponseStatus.Completed ||
            (response.StatusCode != HttpStatusCode.Created &&
             response.StatusCode != HttpStatusCode.OK))
        {
            throw new ApiHelperException("Upload file to crm failed.", response.StatusCode, response.Content);
        }

        var json = JObject.Parse(response.Content);

        var id = json["response"]["id"];

        return id;
    }

    public object UploadToDocuments(Stream fileStream, string filename, string contentType, string folderId, bool createNewIfExist)
    {
        var request = new RestRequest("files/{folderId}/upload.json", Method.Post);

        request.AddUrlSegment("folderId", folderId)
               .AddParameter("createNewIfExist", createNewIfExist);

        request.AddFile(filename, () => fileStream, filename, contentType);

        var response = Execute(request);

        if (response.ResponseStatus != ResponseStatus.Completed ||
            (response.StatusCode != HttpStatusCode.Created &&
             response.StatusCode != HttpStatusCode.OK))
        {
            throw new ApiHelperException("Upload file to documents failed.", response.StatusCode, response.Content);
        }

        var json = JObject.Parse(response.Content);

        var id = json["response"]["id"];

        return id;
    }

    //TODO: need refactoring to comman execute method
    public void SendEmlToSpamTrainer(string serverIp, string serverProtocol, int serverPort,
                                     string serverApiVersion, string serverApiToken, string urlEml,
                                     bool isSpam)
    {
        if (string.IsNullOrEmpty(urlEml))
            return;

        var saLearnApiClient =
            new RestClient(string.Format("{0}://{1}:{2}/", serverProtocol,
                                         serverIp, serverPort));

        var saLearnRequest =
            new RestRequest(
                string.Format("/api/{0}/spam/training.json?auth_token={1}", serverApiVersion,
                              serverApiToken), Method.Post);

        saLearnRequest.AddParameter("url", urlEml)
                      .AddParameter("is_spam", isSpam ? 1 : 0);

        var response = saLearnApiClient.Execute(saLearnRequest);

        if (response.ResponseStatus != ResponseStatus.Completed ||
            (response.StatusCode != HttpStatusCode.Created &&
             response.StatusCode != HttpStatusCode.OK))
        {
            throw new ApiHelperException("Send eml to spam trainer failed.", response.StatusCode, response.Content);
        }
    }

    public void UploadIcsToCalendar(int calendarId, Stream fileStream, string filename, string contentType,
        CalendarEvent eventObj,
        IEnumerable<MimeEntity> mimeAttachments,
        List<MailAttachmentData> mailAttachments)
    {
        var request = new RestRequest("calendar/import.json", Method.Post);

        request.AddParameter("calendarId", calendarId);

        request.AddFile(filename, () => fileStream, filename, contentType);

        foreach (var attachment in eventObj.Attachments)
        {
            if (attachment.Uri.AbsoluteUri.StartsWith("cid:", StringComparison.OrdinalIgnoreCase))
            {
                var contentId = attachment.Uri.AbsoluteUri.Replace("cid:", "");
                var mimeEntity = mimeAttachments.FirstOrDefault(a => a.ContentId == contentId);

                if (mimeEntity != null)
                {
                    var file = mailAttachments.FirstOrDefault(a => a.fileName == mimeEntity.ContentDisposition.FileName);

                    if (file != null)
                    {
                        file.dataStream.Position = 0;
                        request.AddFile(contentId, () => file.dataStream, string.Format("{0}/{1}", contentId, file.fileName), file.contentType);
                    }
                }
            }
        }

        var response = Execute(request);

        if (response.ResponseStatus != ResponseStatus.Completed ||
            (response.StatusCode != HttpStatusCode.Created &&
             response.StatusCode != HttpStatusCode.OK))
        {
            throw new ApiHelperException("Upload ics-file to calendar failed.", response.StatusCode, response.Content);
        }

        var json = JObject.Parse(response.Content);

        int count;

        if (!int.TryParse(json["response"].ToString(), out count))
        {
            _log.WarningUploadIcsFileToCalendar();
        }
    }

    public UserInfo CreateEmployee(bool isVisitor, string email, string firstname, string lastname, string password)
    {
        var request = new RestRequest("people.json", Method.Post);

        request.AddParameter("isVisitor", isVisitor)
            .AddParameter("email", email)
            .AddParameter("firstname", firstname)
            .AddParameter("lastname", lastname)
            .AddParameter("password", password);

        var response = Execute(request);

        if (response.ResponseStatus != ResponseStatus.Completed ||
            (response.StatusCode != HttpStatusCode.Created &&
             response.StatusCode != HttpStatusCode.OK))
        {
            throw new ApiHelperException("ApiHelper->CreateEmployee() failed.", response.StatusCode, response.Content);
        }

        var json = JObject.Parse(response.Content);

        _log.DebugApiHelperResponse(json["response"].ToString());

        var userInfo = new UserInfo
        {
            Id = Guid.Parse(json["response"]["id"].ToString()),
            Email = json["response"]["email"].ToString(),
            FirstName = json["response"]["firstName"].ToString(),
            LastName = json["response"]["lastName"].ToString(),
            UserName = json["response"]["userName"].ToString(),
        };

        return userInfo;
    }

    public JObject GetPortalSettings()
    {
        var request = new RestRequest("settings/security.json", Method.Get);

        var response = Execute(request);

        if (response.ResponseStatus != ResponseStatus.Completed ||
            (response.StatusCode != HttpStatusCode.Created &&
             response.StatusCode != HttpStatusCode.OK))
        {
            throw new ApiHelperException("GetPortalSettings failed.", response.StatusCode, response.Content);
        }

        var json = JObject.Parse(response.Content);

        return json;
    }

    public bool IsCalendarModuleAvailable()
    {
        var json = GetPortalSettings();

        var jWebItem = json["response"].Children<JObject>()
            .FirstOrDefault(
                o =>
                    o["webItemId"] != null &&
                    o["webItemId"].ToString() == WebItemManager.CalendarProductID.ToString());

        var isAvailable = jWebItem != null && jWebItem["enabled"] != null && Convert.ToBoolean(jWebItem["enabled"]);

        return isAvailable;
    }

    public bool IsMailModuleAvailable()
    {
        var json = GetPortalSettings();

        var jWebItem = json["response"].Children<JObject>()
            .FirstOrDefault(
                o =>
                    o["webItemId"] != null &&
                    o["webItemId"].ToString() == WebItemManager.MailProductID.ToString());

        var isAvailable = jWebItem != null && jWebItem["enabled"] != null && Convert.ToBoolean(jWebItem["enabled"]);

        return isAvailable;
    }

    public bool IsCrmModuleAvailable()
    {
        var json = GetPortalSettings();

        var crmId = WebItemManager.CRMProductID.ToString();

        var jWebItem = json["response"].Children<JObject>()
            .FirstOrDefault(
                o =>
                    o["webItemId"] != null &&
                    o["webItemId"].ToString() == crmId);

        var isAvailable = jWebItem != null && jWebItem["enabled"] != null && Convert.ToBoolean(jWebItem["enabled"]);

        return isAvailable;
    }
}
