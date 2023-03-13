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

using ASC.Mail.Core.Storage;
using SecurityContext = ASC.Core.SecurityContext;

namespace ASC.Mail.Core.Engine;

[Scope]
public class OperationEngine
{
    private readonly DistributedTaskQueue _tasks;
    private readonly TenantManager _tenantManager;
    private readonly SecurityContext _securityContext;
    private readonly IMailDaoFactory _mailDaoFactory;
    private readonly MailboxEngine _mailboxEngine;
    private readonly QuotaEngine _quotaEngine;
    private readonly FolderEngine _folderEngine;
    private readonly CacheEngine _cacheEngine;
    private readonly IndexEngine _indexEngine;
    private readonly UserFolderEngine _userFolderEngine;
    private readonly FilterEngine _filterEngine;
    private readonly MessageEngine _messageEngine;
    private readonly ServerMailboxEngine _serverMailboxEngine;
    private readonly CoreSettings _coreSettings;
    private readonly MailStorageManager _storageManager;
    private readonly MailStorageFactory _storageFactory;
    private readonly FactoryIndexer<MailMail> _factoryIndexer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerProvider _logProvider;
    private readonly TempStream _tempStream;

    public OperationEngine(
        TenantManager tenantManager,
        SecurityContext securityContext,
        IMailDaoFactory mailDaoFactory,
        MailboxEngine mailboxEngine,
        QuotaEngine quotaEngine,
        FolderEngine folderEngine,
        CacheEngine cacheEngine,
        IndexEngine indexEngine,
        UserFolderEngine userFolderEngine,
        FilterEngine filterEngine,
        MessageEngine messageEngine,
        ServerMailboxEngine serverMailboxEngine,
        CoreSettings coreSettings,
        MailStorageManager storageManager,
        MailStorageFactory storageFactory,
        FactoryIndexer<MailMail> factoryIndexer,
        TempStream tempStream,
        IServiceProvider serviceProvider,
        ILoggerProvider logProvider)
    {
        _tasks = _serviceProvider.GetRequiredService<DistributedTaskQueue>();
        _tasks.Name = "mailOperations";

        _tenantManager = tenantManager;
        _securityContext = securityContext;
        _mailDaoFactory = mailDaoFactory;
        _mailboxEngine = mailboxEngine;
        _quotaEngine = quotaEngine;
        _folderEngine = folderEngine;
        _cacheEngine = cacheEngine;
        _indexEngine = indexEngine;
        _userFolderEngine = userFolderEngine;
        _filterEngine = filterEngine;
        _messageEngine = messageEngine;
        _serverMailboxEngine = serverMailboxEngine;
        _coreSettings = coreSettings;
        _storageManager = storageManager;
        _storageFactory = storageFactory;
        _factoryIndexer = factoryIndexer;
        _serviceProvider = serviceProvider;
        _logProvider = logProvider;
        _tempStream = tempStream;
    }

    public MailOperationStatus RemoveMailbox(MailBoxData mailbox,
        Func<DistributedTask, string> translateMailOperationStatus = null)
    {
        var tenant = _tenantManager.GetCurrentTenant();
        var user = _securityContext.CurrentAccount;

        var operations = _tasks.GetAllTasks()
            .Where(o =>
            {
                var oTenant = o[MailOperation.TENANT];
                var oUser = o[MailOperation.OWNER];
                var oType = o[MailOperation.OPERATION_TYPE];
                return oTenant == tenant.Id &&
                       oUser == user.ID.ToString() &&
                       oType == MailOperationType.RemoveMailbox;
            })
            .ToList();

        var sameOperation = operations.FirstOrDefault(o =>
        {
            var oSource = o[MailOperation.SOURCE];
            return oSource == mailbox.MailBoxId.ToString();
        });

        if (sameOperation != null)
        {
            return GetMailOperationStatus(sameOperation.Id, translateMailOperationStatus);
        }

        var runningOperation = operations.FirstOrDefault(o => o.Status <= DistributedTaskStatus.Running);

        if (runningOperation != null)
            throw new MailOperationAlreadyRunningException("Remove mailbox operation already running.");

        var op = new MailRemoveMailboxOperation(
            _tenantManager,
            _securityContext,
            _mailboxEngine,
            _quotaEngine,
            _folderEngine,
            _cacheEngine,
            _indexEngine,
            _mailDaoFactory,
            _coreSettings,
            _storageManager,
            _logProvider,
            mailbox);

        return QueueTask(op, translateMailOperationStatus);
    }

    public MailOperationStatus DownloadAllAttachments(int messageId,
        Func<DistributedTask, string> translateMailOperationStatus = null)
    {
        var tenant = _tenantManager.GetCurrentTenant();
        var user = _securityContext.CurrentAccount;

        var operations = _tasks.GetAllTasks()
            .Where(o =>
            {
                var oTenant = o[MailOperation.TENANT];
                var oUser = o[MailOperation.OWNER];
                var oType = o[MailOperation.OPERATION_TYPE];
                return oTenant == tenant.Id &&
                       oUser == user.ID.ToString() &&
                       oType == MailOperationType.DownloadAllAttachments;
            })
            .ToList();

        var sameOperation = operations.FirstOrDefault(o =>
        {
            var oSource = o[MailOperation.SOURCE];
            return oSource == messageId.ToString();
        });

        if (sameOperation != null)
        {
            return GetMailOperationStatus(sameOperation.Id, translateMailOperationStatus);
        }

        var runningOperation = operations.FirstOrDefault(o => o.Status <= DistributedTaskStatus.Running);

        if (runningOperation != null)
            throw new MailOperationAlreadyRunningException("Download all attachments operation already running.");

        var op = new MailDownloadAllAttachmentsOperation(
            _tenantManager,
            _securityContext,
            _mailDaoFactory,
            _messageEngine,
            _coreSettings,
            _storageManager,
            _storageFactory,
            _logProvider,
            _tempStream,
            messageId);

        return QueueTask(op, translateMailOperationStatus);
    }

    public MailOperationStatus RecalculateFolders(Func<DistributedTask, string> translateMailOperationStatus = null)
    {
        var tenant = _tenantManager.GetCurrentTenant();
        var user = _securityContext.CurrentAccount;

        var operations = _tasks.GetAllTasks()
            .Where(o =>
            {
                var oTenant = o[MailOperation.TENANT];
                var oUser = o[MailOperation.OWNER];
                var oType = o[MailOperation.OPERATION_TYPE];
                return oTenant == tenant.Id &&
                       oUser == user.ID.ToString() &&
                       oType == MailOperationType.RecalculateFolders;
            });

        var runningOperation = operations.FirstOrDefault(o => o.Status <= DistributedTaskStatus.Running);

        if (runningOperation != null)
            return GetMailOperationStatus(runningOperation.Id, translateMailOperationStatus);

        var op = new MailRecalculateFoldersOperation(
            _tenantManager,
            _securityContext,
            _mailDaoFactory,
            _folderEngine,
            _coreSettings,
            _storageManager,
            _logProvider);

        return QueueTask(op, translateMailOperationStatus);
    }

    public MailOperationStatus CheckDomainDns(string domainName, ServerDns dns,
        Func<DistributedTask, string> translateMailOperationStatus = null)
    {
        var tenant = _tenantManager.GetCurrentTenant();
        var user = _securityContext.CurrentAccount;

        var operations = _tasks.GetAllTasks()
            .Where(o =>
            {
                var oTenant = o[MailOperation.TENANT];
                var oUser = o[MailOperation.OWNER];
                var oType = o[MailOperation.OPERATION_TYPE];
                var oSource = o[MailOperation.SOURCE];
                return oTenant == tenant.Id &&
                       oUser == user.ID.ToString() &&
                       oType == MailOperationType.CheckDomainDns &&
                       oSource == domainName;
            });

        var runningOperation = operations.FirstOrDefault(o => o.Status <= DistributedTaskStatus.Running);

        if (runningOperation != null)
            return GetMailOperationStatus(runningOperation.Id, translateMailOperationStatus);

        var op = new MailCheckMailserverDomainsDnsOperation(
            _tenantManager,
            _securityContext,
            _mailDaoFactory,
            _coreSettings,
            _storageManager,
            _logProvider,
            domainName,
            dns);

        return QueueTask(op, translateMailOperationStatus);
    }

    public MailOperationStatus RemoveUserFolder(int userFolderId,
        Func<DistributedTask, string> translateMailOperationStatus = null)
    {
        var tenant = _tenantManager.GetCurrentTenant();
        var user = _securityContext.CurrentAccount;

        var operations = _tasks.GetAllTasks()
            .Where(o =>
            {
                var oTenant = o[MailOperation.TENANT];
                var oUser = o[MailOperation.OWNER];
                var oType = o[MailOperation.OPERATION_TYPE];
                return oTenant == tenant.Id &&
                       oUser == user.ID.ToString() &&
                       oType == MailOperationType.RemoveUserFolder;
            })
            .ToList();

        var sameOperation = operations.FirstOrDefault(o =>
        {
            var oSource = o[MailOperation.SOURCE];
            return oSource == userFolderId.ToString();
        });

        if (sameOperation != null)
        {
            return GetMailOperationStatus(sameOperation.Id, translateMailOperationStatus);
        }

        var runningOperation = operations.FirstOrDefault(o => o.Status <= DistributedTaskStatus.Running);

        if (runningOperation != null)
            throw new MailOperationAlreadyRunningException("Remove user folder operation already running.");

        var op = new MailRemoveUserFolderOperation(
            _tenantManager,
            _securityContext,
            _mailDaoFactory,
            _messageEngine,
            _indexEngine,
            _coreSettings,
            _storageManager,
            _factoryIndexer,
            _serviceProvider,
            _logProvider,
            userFolderId);

        return QueueTask(op, translateMailOperationStatus);
    }

    public MailOperationStatus ApplyFilter(int filterId,
        Func<DistributedTask, string> translateMailOperationStatus = null)
    {
        var tenant = _tenantManager.GetCurrentTenant();
        var user = _securityContext.CurrentAccount;

        var operations = _tasks.GetAllTasks()
            .Where(o =>
            {
                var oTenant = o[MailOperation.TENANT];
                var oUser = o[MailOperation.OWNER];
                var oType = o[MailOperation.OPERATION_TYPE];
                return oTenant == tenant.Id &&
                       oUser == user.ID.ToString() &&
                       oType == MailOperationType.ApplyFilter;
            })
            .ToList();

        var sameOperation = operations.FirstOrDefault(o =>
        {
            var oSource = o[MailOperation.SOURCE];
            return oSource == filterId.ToString();
        });

        if (sameOperation != null)
        {
            return GetMailOperationStatus(sameOperation.Id, translateMailOperationStatus);
        }

        var runningOperation = operations.FirstOrDefault(o => o.Status <= DistributedTaskStatus.Running);

        if (runningOperation != null)
            throw new MailOperationAlreadyRunningException("Apply filter operation already running.");

        var op = new ApplyFilterOperation(
            _tenantManager,
            _securityContext,
            _mailDaoFactory,
            _filterEngine,
            _messageEngine,
            _coreSettings,
            _storageManager,
            _storageFactory,
            _logProvider,
            filterId);

        return QueueTask(op, translateMailOperationStatus);
    }

    public MailOperationStatus ApplyFilters(List<int> ids,
        Func<DistributedTask, string> translateMailOperationStatus = null)
    {
        var tenant = _tenantManager.GetCurrentTenant();
        var user = _securityContext.CurrentAccount;

        var op = new ApplyFiltersOperation(
            _tenantManager,
            _securityContext,
            _mailDaoFactory,
            _filterEngine,
            _messageEngine,
            _mailboxEngine,
            _coreSettings,
            _storageManager,
            _logProvider,
            ids);

        return QueueTask(op, translateMailOperationStatus);
    }

    public MailOperationStatus RemoveServerDomain(ServerDomainData domain)
    {
        var tenant = _tenantManager.GetCurrentTenant();
        var user = _securityContext.CurrentAccount;

        var operations = _tasks.GetAllTasks()
            .Where(o =>
            {
                var oTenant = o[MailOperation.TENANT];
                var oUser = o[MailOperation.OWNER];
                var oType = o[MailOperation.OPERATION_TYPE];
                return oTenant == tenant.Id &&
                       oUser == user.ID.ToString() &&
                       oType == MailOperationType.RemoveDomain;
            })
            .ToList();

        var sameOperation = operations.FirstOrDefault(o =>
        {
            var oSource = o[MailOperation.SOURCE];
            return oSource == domain.Id.ToString();
        });

        if (sameOperation != null)
        {
            return GetMailOperationStatus(sameOperation.Id);
        }

        var runningOperation = operations.FirstOrDefault(o => o.Status <= DistributedTaskStatus.Running);

        if (runningOperation != null)
            throw new MailOperationAlreadyRunningException("Remove mailbox operation already running.");

        var op = new MailRemoveMailserverDomainOperation(
            _tenantManager, _securityContext,
            _mailDaoFactory, _mailboxEngine, _cacheEngine, _indexEngine,
            _coreSettings, _storageManager,
            _logProvider, domain);

        return QueueTask(op);
    }

    public MailOperationStatus RemoveServerMailbox(MailBoxData mailbox)
    {
        var tenant = _tenantManager.GetCurrentTenant();
        var user = _securityContext.CurrentAccount;

        var operations = _tasks.GetAllTasks()
            .Where(o =>
            {
                var oTenant = o[MailOperation.TENANT];
                var oUser = o[MailOperation.OWNER];
                var oType = o[MailOperation.OPERATION_TYPE];
                return oTenant == tenant.Id &&
                       oUser == user.ID.ToString() &&
                       oType == MailOperationType.RemoveMailbox;
            })
            .ToList();

        var sameOperation = operations.FirstOrDefault(o =>
        {
            var oSource = o[MailOperation.SOURCE];
            return oSource == mailbox.MailBoxId.ToString();
        });

        if (sameOperation != null)
        {
            return GetMailOperationStatus(sameOperation.Id);
        }

        var runningOperation = operations.FirstOrDefault(o => o.Status <= DistributedTaskStatus.Running);

        if (runningOperation != null)
            throw new MailOperationAlreadyRunningException("Remove mailbox operation already running.");

        var op = new MailRemoveMailserverMailboxOperation(
            _tenantManager, _securityContext,
            _mailDaoFactory, _serverMailboxEngine, this, _cacheEngine, _indexEngine,
            _coreSettings, _storageManager,
            _logProvider, mailbox);

        return QueueTask(op);
    }

    public MailOperationStatus QueueTask(MailOperation op, Func<DistributedTask, string> translateMailOperationStatus = null)
    {
        var task = op.GetDistributedTask();
        _tasks.EnqueueTask(op.RunJob, task);
        return GetMailOperationStatus(task.Id, translateMailOperationStatus);
    }

    public List<MailOperationStatus> GetMailOperations(Func<DistributedTask, string> translateMailOperationStatus = null)
    {
        var tenant = _tenantManager.GetCurrentTenant().Id;

        var operations = _tasks.GetAllTasks().Where(
                o =>
                    o[MailOperation.TENANT] == tenant &&
                    o[MailOperation.OWNER] == _securityContext.CurrentAccount.ID.ToString());

        var list = new List<MailOperationStatus>();

        foreach (var o in operations)
        {
            if (string.IsNullOrEmpty(o.Id))
                continue;

            list.Add(GetMailOperationStatus(o.Id, translateMailOperationStatus));
        }

        return list;
    }

    public MailOperationStatus GetMailOperationStatus(string operationId, Func<DistributedTask, string> translateMailOperationStatus = null)
    {
        var defaultResult = new MailOperationStatus
        {
            Id = null,
            Completed = true,
            Percents = 100,
            Status = "",
            Error = "",
            Source = "",
            OperationType = -1
        };

        if (string.IsNullOrEmpty(operationId))
            return defaultResult;

        var operations = _tasks.GetAllTasks().ToList();

        foreach (var o in operations)
        {
            if (o.InstanceId != 0 && Process.GetProcesses().Any(p => p.Id == o.InstanceId))
                continue;

            o[MailOperation.PROGRESS] = 100;
            _tasks.DequeueTask(o.Id);
        }

        var tenant = _tenantManager.GetCurrentTenant().Id;

        var operation = operations
            .FirstOrDefault(
                o =>
                    o[MailOperation.TENANT] == tenant &&
                    o[MailOperation.OWNER] == _securityContext.CurrentAccount.ID.ToString() &&
                    o.Id.Equals(operationId));

        if (operation == null)
            return defaultResult;

        if (DistributedTaskStatus.Running < operation.Status)
        {
            operation[MailOperation.PROGRESS]= 100;
            _tasks.DequeueTask(operation.Id);
        }

        var operationTypeIndex = (int)operation[MailOperation.OPERATION_TYPE];

        var result = new MailOperationStatus
        {
            Id = operation.Id,
            Completed = operation[MailOperation.FINISHED],
            Percents = operation[MailOperation.PROGRESS],
            Status = translateMailOperationStatus != null
                ? translateMailOperationStatus(operation)
                : operation[MailOperation.STATUS],
            Error = operation[MailOperation.ERROR],
            Source = operation[MailOperation.SOURCE],
            OperationType = operationTypeIndex,
            Operation = Enum.GetName(typeof(MailOperationType), operationTypeIndex)
        };

        return result;
    }
}