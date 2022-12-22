using ASC.Api.Core;
using ASC.Common;
using ASC.Common.Threading;
using ASC.Core;
using ASC.Mail.Configuration;
using ASC.Mail.Core.Engine;
using ASC.Mail.Core.Engine.Operations.Base;
using ASC.Mail.Core.Resources;
using ASC.Web.Api.Routing;
using ASC.Web.Core.Users;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Configuration;
using System.Globalization;

namespace ASC.Mail.Controllers
{
    [DefaultRoute]
    [ApiController]
    [Scope]
    public partial class MailController : ControllerBase
    {
        private int TenantId => _tenantManager.GetCurrentTenant().Id;

        private string UserId => _securityContext.CurrentAccount.ID.ToString();

        private readonly TenantManager _tenantManager;
        private readonly SecurityContext _securityContext;
        private readonly UserManager _userManager;
        private readonly DisplayUserSettingsHelper _displayUserSettingsHelper;
        private readonly ApiContext _apiContext;
        private readonly AccountEngine _accountEngine;
        private readonly AlertEngine _alertEngine;
        private readonly DisplayImagesAddressEngine _displayImagesAddressEngine;
        private readonly SignatureEngine _signatureEngine;
        private readonly TagEngine _tagEngine;
        private readonly MailboxEngine _mailboxEngine;
        private readonly DocumentsEngine _documentsEngine;
        private readonly AutoreplyEngine _autoreplyEngine;
        private readonly ContactEngine _contactEngine;
        private readonly MessageEngine _messageEngine;
        private readonly CrmLinkEngine _crmLinkEngine;
        private readonly SpamEngine _spamEngine;
        private readonly FilterEngine _filterEngine;
        private readonly UserFolderEngine _userFolderEngine;
        private readonly FolderEngine _folderEngine;
        private readonly DraftEngine _draftEngine;
        private readonly TemplateEngine _templateEngine;
        private readonly SettingEngine _settingEngine;
        private readonly ServerEngine _serverEngine;
        private readonly ServerDomainEngine _serverDomainEngine;
        private readonly ServerMailboxEngine _serverMailboxEngine;
        private readonly ServerMailgroupEngine _serverMailgroupEngine;
        private readonly OperationEngine _operationEngine;
        private readonly TestEngine _testEngine;
        private readonly CoreBaseSettings _coreBaseSettings;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _log;
        private readonly MailSettings _mailSettings;

        private string Username
        {
            get { return _securityContext.CurrentAccount.ID.ToString(); }
        }

        private CultureInfo CurrentCulture
        {
            get
            {
                var u = _userManager.GetUsers(new Guid(Username));

                var culture = !string.IsNullOrEmpty(u.CultureName)
                    ? u.GetCulture()
                    : _tenantManager.GetCurrentTenant().GetCulture();

                return culture;
            }
        }

        public MailController(
            TenantManager tenantManager,
            SecurityContext securityContext,
            UserManager userManager,
            DisplayUserSettingsHelper displayUserSettingsHelper,
            ApiContext apiContext,
            AccountEngine accountEngine,
            AlertEngine alertEngine,
            DisplayImagesAddressEngine displayImagesAddressEngine,
            SignatureEngine signatureEngine,
            TagEngine tagEngine,
            MailboxEngine mailboxEngine,
            DocumentsEngine documentsEngine,
            AutoreplyEngine autoreplyEngine,
            ContactEngine contactEngine,
            MessageEngine messageEngine,
            CrmLinkEngine crmLinkEngine,
            SpamEngine spamEngine,
            FilterEngine filterEngine,
            UserFolderEngine userFolderEngine,
            FolderEngine folderEngine,
            DraftEngine draftEngine,
            TemplateEngine templateEngine,
            SettingEngine settingEngine,
            ServerEngine serverEngine,
            ServerDomainEngine serverDomainEngine,
            ServerMailboxEngine serverMailboxEngine,
            ServerMailgroupEngine serverMailgroupEngine,
            OperationEngine operationEngine,
            TestEngine testEngine,
            CoreBaseSettings coreBaseSettings,
            MailSettings mailSettings,
            IServiceProvider serviceProvider,
            ILoggerProvider logProvider)
        {
            _tenantManager = tenantManager;
            _securityContext = securityContext;
            _mailSettings = mailSettings;
            _userManager = userManager;
            _displayUserSettingsHelper = displayUserSettingsHelper;
            _apiContext = apiContext;
            _accountEngine = accountEngine;
            _alertEngine = alertEngine;
            _displayImagesAddressEngine = displayImagesAddressEngine;
            _signatureEngine = signatureEngine;
            _tagEngine = tagEngine;
            _mailboxEngine = mailboxEngine;
            _documentsEngine = documentsEngine;
            _autoreplyEngine = autoreplyEngine;
            _contactEngine = contactEngine;
            _messageEngine = messageEngine;
            _crmLinkEngine = crmLinkEngine;
            _spamEngine = spamEngine;
            _filterEngine = filterEngine;
            _userFolderEngine = userFolderEngine;
            _folderEngine = folderEngine;
            _draftEngine = draftEngine;
            _templateEngine = templateEngine;
            _settingEngine = settingEngine;
            _serverEngine = serverEngine;
            _serverDomainEngine = serverDomainEngine;
            _serverMailboxEngine = serverMailboxEngine;
            _serverMailgroupEngine = serverMailgroupEngine;
            _operationEngine = operationEngine;
            _testEngine = testEngine;
            _coreBaseSettings = coreBaseSettings;
            _serviceProvider = serviceProvider;
            _log = logProvider.CreateLogger("ASC.Api.MailController");
        }

        [HttpGet("info")]
        public Module GetModule()
        {
            var product = new MailProduct();
            product.Init();
            return new Module(product);
        }


        /// <summary>
        /// Method for translation mail operation statuses
        /// </summary>
        /// <param name="op">instance of DistributedTask</param>
        /// <returns>translated status text</returns>
        private static string TranslateMailOperationStatus(DistributedTask op)
        {
            var type = op[MailOperation.OPERATION_TYPE];
            var status = op[MailOperation.STATUS];
            //TODO: Move strings to Resource file
            switch (type)
            {
                case MailOperationType.DownloadAllAttachments:
                    {
                        var progress = op[MailOperation.PROGRESS];
                        switch (progress)
                        {
                            case MailOperationDownloadAllAttachmentsProgress.Init:
                                return MailApiResource.SetupTenantAndUserHeader;
                            case MailOperationDownloadAllAttachmentsProgress.GetAttachments:
                                return MailApiResource.GetAttachmentsHeader;
                            case MailOperationDownloadAllAttachmentsProgress.Zipping:
                                return MailApiResource.ZippingAttachmentsHeader;
                            case MailOperationDownloadAllAttachmentsProgress.ArchivePreparation:
                                return MailApiResource.PreparationArchiveHeader;
                            case MailOperationDownloadAllAttachmentsProgress.CreateLink:
                                return MailApiResource.CreatingLinkHeader;
                            case MailOperationDownloadAllAttachmentsProgress.Finished:
                                return MailApiResource.FinishedHeader;
                            default:
                                return status;
                        }
                    }
                case MailOperationType.RemoveMailbox:
                    {
                        var progress = op[MailOperation.PROGRESS];
                        switch (progress)
                        {
                            case MailOperationRemoveMailboxProgress.Init:
                                return "Setup tenant and user";
                            case MailOperationRemoveMailboxProgress.RemoveFromDb:
                                return "Remove mailbox from Db";
                            case MailOperationRemoveMailboxProgress.FreeQuota:
                                return "Decrease newly freed quota space";
                            case MailOperationRemoveMailboxProgress.RecalculateFolder:
                                return "Recalculate folders counters";
                            case MailOperationRemoveMailboxProgress.ClearCache:
                                return "Clear accounts cache";
                            case MailOperationRemoveMailboxProgress.Finished:
                                return "Finished";
                            default:
                                return status;
                        }
                    }
                case MailOperationType.RecalculateFolders:
                    {
                        var progress = op[MailOperation.PROGRESS];
                        switch (progress)
                        {
                            case MailOperationRecalculateMailboxProgress.Init:
                                return "Setup tenant and user";
                            case MailOperationRecalculateMailboxProgress.CountUnreadMessages:
                                return "Calculate unread messages";
                            case MailOperationRecalculateMailboxProgress.CountTotalMessages:
                                return "Calculate total messages";
                            case MailOperationRecalculateMailboxProgress.CountUreadConversation:
                                return "Calculate unread conversations";
                            case MailOperationRecalculateMailboxProgress.CountTotalConversation:
                                return "Calculate total conversations";
                            case MailOperationRecalculateMailboxProgress.UpdateFoldersCounters:
                                return "Update folders counters";
                            case MailOperationRecalculateMailboxProgress.CountUnreadUserFolderMessages:
                                return "Calculate unread messages in user folders";
                            case MailOperationRecalculateMailboxProgress.CountTotalUserFolderMessages:
                                return "Calculate total messages in user folders";
                            case MailOperationRecalculateMailboxProgress.CountUreadUserFolderConversation:
                                return "Calculate unread conversations in user folders";
                            case MailOperationRecalculateMailboxProgress.CountTotalUserFolderConversation:
                                return "Calculate total conversations in user folders";
                            case MailOperationRecalculateMailboxProgress.UpdateUserFoldersCounters:
                                return "Update user folders counters";
                            case MailOperationRecalculateMailboxProgress.Finished:
                                return "Finished";
                            default:
                                return status;
                        }
                    }
                case MailOperationType.RemoveUserFolder:
                    {
                        var progress = op[MailOperation.PROGRESS];
                        switch (progress)
                        {
                            case MailOperationRemoveUserFolderProgress.Init:
                                return "Setup tenant and user";
                            case MailOperationRemoveUserFolderProgress.MoveMailsToTrash:
                                return "Move mails into Trash folder";
                            case MailOperationRemoveUserFolderProgress.DeleteFolders:
                                return "Delete folder";
                            case MailOperationRemoveUserFolderProgress.Finished:
                                return "Finished";
                            default:
                                return status;
                        }
                    }
                default:
                    return status;
            }
        }

        /// <summary>
        /// Limit result per Contact System
        /// </summary>
        private static int MailAutocompleteMaxCountPerSystem
        {
            get
            {
                var count = 20;
                if (ConfigurationManager.AppSettings["mail.autocomplete-max-count"] == null)
                    return count;

                int.TryParse(ConfigurationManager.AppSettings["mail.autocomplete-max-count"], out count);
                return count;
            }
        }

        /// <summary>
        /// Timeout in milliseconds
        /// </summary>
        private static int MailAutocompleteTimeout
        {
            get
            {
                var count = 3000;
                if (ConfigurationManager.AppSettings["mail.autocomplete-timeout"] == null)
                    return count;

                int.TryParse(ConfigurationManager.AppSettings["mail.autocomplete-timeout"], out count);
                return count;
            }
        }
    }
}