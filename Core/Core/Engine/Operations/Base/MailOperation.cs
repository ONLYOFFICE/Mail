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

namespace ASC.Mail.Core.Engine.Operations.Base;

public abstract class MailOperation
{
    public const string TENANT = "MailOperationOwnerTenant";
    public const string OWNER = "MailOperationOwnerID";
    public const string OPERATION_TYPE = "MailOperationType";
    public const string SOURCE = "MailOperationSource";
    public const string PROGRESS = "MailOperationProgress";
    public const string STATUS = "MailOperationResult";
    public const string ERROR = "MailOperationError";
    public const string FINISHED = "MailOperationFinished";

    private readonly string _culture;

    protected DistributedTask TaskInfo { get; private set; }

    protected int Progress { get; private set; }

    protected string Source { get; private set; }

    protected string Status { get; set; }

    protected string Error { get; set; }

    protected Tenant CurrentTenant { get; private set; }

    protected IAccount CurrentUser { get; private set; }

    protected ILogger Log { get; private set; }

    protected CancellationToken CancellationToken { get; private set; }

    public abstract MailOperationType OperationType { get; }

    public TenantManager TenantManager { get; }
    public SecurityContext SecurityContext { get; }
    public IMailDaoFactory MailDaoFactory { get; }
    public CoreSettings CoreSettings { get; }
    public MailStorageManager StorageManager { get; }
    public MailStorageFactory StorageFactory { get; }

    protected MailOperation(
        TenantManager tenantManager,
        SecurityContext securityContext,
        IMailDaoFactory mailDaoFactory,
        CoreSettings coreSettings,
        MailStorageManager storageManager,
        ILoggerProvider logProvider,
        MailStorageFactory storageFactory = null)
    {
        CurrentTenant = tenantManager.GetCurrentTenant();
        CurrentUser = securityContext.CurrentAccount;

        _culture = Thread.CurrentThread.CurrentCulture.Name;

        Source = "";
        Progress = 0;
        Status = "";
        Error = "";
        Source = "";

        TaskInfo = new DistributedTask();
        TenantManager = tenantManager;
        SecurityContext = securityContext;
        MailDaoFactory = mailDaoFactory;
        CoreSettings = coreSettings;
        StorageManager = storageManager;
        StorageFactory = storageFactory;
        Log = logProvider.CreateLogger("ASC.Mail.Operation");
    }

    public System.Threading.Tasks.Task RunJob(DistributedTask _, CancellationToken cancellationToken)
    {
        try
        {
            CancellationToken = cancellationToken;

            //TODO: Check and fix
            //TenantManager.SetCurrentTenant(CurrentTenant);
            //SecurityContext.AuthenticateMe(ASC.Core.Configuration.Constants.CoreSystem);

            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(_culture);
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(_culture);

            Do();
        }
        catch (AuthorizingException authError)
        {
            Error = "ErrorAccessDenied";
            var logError = new System.Security.SecurityException(Error, authError).ToString();
            Log.ErrorMailOperationAuthorizing(logError);
        }
        catch (AggregateException ae)
        {
            ae.Flatten().Handle(e => e is TaskCanceledException || e is OperationCanceledException);
        }
        catch (TenantQuotaException e)
        {
            Error = "TenantQuotaSettled";
            Log.ErrorMailOperationTenantQuota(e.ToString());
        }
        catch (FormatException e)
        {
            Error = "CantCreateUsers";
            Log.ErrorMailOperationFormat(e.ToString());
        }
        catch (Exception e)
        {
            Error = "InternalServerError";
            Log.ErrorMailOperationServer(e.ToString());
        }
        finally
        {
            try
            {
                TaskInfo[FINISHED] = true;
                PublishTaskInfo();
            }
            catch
            {
                /* ignore */
            }
        }

        return System.Threading.Tasks.Task.CompletedTask;
    }

    public virtual DistributedTask GetDistributedTask()
    {
        FillDistributedTask();
        return TaskInfo;
    }

    protected virtual void FillDistributedTask()
    {
        TaskInfo[SOURCE] = Source;
        TaskInfo[OPERATION_TYPE] = OperationType;
        TaskInfo[TENANT] = CurrentTenant.Id;
        TaskInfo[OWNER] = CurrentUser.ID.ToString();
        TaskInfo[PROGRESS] = Progress < 100 ? Progress : 100;
        TaskInfo[STATUS] = Status;
        TaskInfo[ERROR] = Error;
    }

    protected int GetProgress()
    {
        return Progress;
    }

    public void SetSource(string source)
    {
        Source = source;
    }

    public void SetProgress(int? currentPercent = null, string currentStatus = null, string currentSource = null)
    {
        if (!currentPercent.HasValue && currentStatus == null && currentSource == null)
            return;

        if (currentPercent.HasValue)
            Progress = currentPercent.Value;

        if (currentStatus != null)
            Status = currentStatus;

        if (currentSource != null)
            Source = currentSource;

        PublishTaskInfo();
    }

    protected void PublishTaskInfo()
    {
        FillDistributedTask();
        TaskInfo.PublishChanges();
    }

    protected abstract void Do();
}
