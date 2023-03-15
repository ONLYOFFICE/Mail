using ASC.Mail.Core.Core.Entities;

namespace ASC.Mail.Core.Storage
{
    [Scope]
    public class MailTenantQuotaController : IQuotaController
    {
        private int _tenant;
        private SecurityContext _securityContext;
        private readonly CoreBaseSettings _coreBaseSettings;
        private readonly IServiceProvider _serviceProvider;
        private long _currentSize;

        public MailTenantQuotaController(
            SecurityContext securityContext,
            CoreBaseSettings coreBaseSettings,
            IServiceProvider serviceProvider)
        {
            _securityContext = securityContext;
            _coreBaseSettings = coreBaseSettings;
            _serviceProvider = serviceProvider;
        }

        public void Init(int tenant)
        {
            var quotaservice = _serviceProvider.GetRequiredService<DbMailQuotaService>();

            _currentSize = quotaservice.FindTenantQuotaRows(tenant)
                          .Where(r => UsedInQuota(r.Tag))
                          .Sum(r => r.Counter);
        }

        #region IQuotaController Members
        public void QuotaUsedAdd(string module, string domain, string dataTag, long size, bool quotaCheckFileSize = true)
        {
            QuotaUsedAdd(module, domain, dataTag, size, Guid.Empty, quotaCheckFileSize);
        }
        public void QuotaUsedAdd(string module, string domain, string dataTag, long size, Guid ownerId, bool quotaCheckFileSize)
        {
            size = Math.Abs(size);
            if (UsedInQuota(dataTag))
            {
                QuotaUsedCheck(size, quotaCheckFileSize, ownerId);
                Interlocked.Add(ref _currentSize, size);
            }

            SetTenantQuotaRow(module, domain, size, dataTag, true, Guid.Empty);
            if (ownerId != ASC.Core.Configuration.Constants.CoreSystem.ID)
            {
                SetTenantQuotaRow(module, domain, size, dataTag, true, ownerId != Guid.Empty ? ownerId : _securityContext.CurrentAccount.ID);
            }

        }

        public void QuotaUsedDelete(string module, string domain, string dataTag, long size)
        {
            QuotaUsedDelete(module, domain, dataTag, size, Guid.Empty);
        }
        public void QuotaUsedDelete(string module, string domain, string dataTag, long size, Guid ownerId)
        {
            size = -Math.Abs(size);
            if (UsedInQuota(dataTag))
            {
                Interlocked.Add(ref _currentSize, size);
            }

            SetTenantQuotaRow(module, domain, size, dataTag, true, Guid.Empty);
            if (ownerId != ASC.Core.Configuration.Constants.CoreSystem.ID)
            {
                SetTenantQuotaRow(module, domain, size, dataTag, true, ownerId != Guid.Empty ? ownerId : _securityContext.CurrentAccount.ID);
            }
        }

        public void QuotaUsedSet(string module, string domain, string dataTag, long size)
        {
            size = Math.Max(0, size);
            if (UsedInQuota(dataTag))
            {
                Interlocked.Exchange(ref _currentSize, size);
            }
            SetTenantQuotaRow(module, domain, size, dataTag, false, Guid.Empty);
        }

        public void QuotaUsedCheck(long size, Guid ownedId)
        {
            QuotaUsedCheck(size, true, ownedId);
        }

        public void QuotaUsedCheck(long size, bool quotaCheckFileSize, Guid ownedId)
        {
            var quotaservice = _serviceProvider.GetRequiredService<DbMailQuotaService>();

            var quota = quotaservice.GetTenantQuota(_tenant);

            SettingsManager settingsManager = _serviceProvider.GetRequiredService<SettingsManager>();
            if (quota != null)
            {
                if (quotaCheckFileSize && quota.MaxFileSize != 0 && quota.MaxFileSize < size)
                {
                    throw new TenantQuotaException(string.Format("Exceeds the maximum file size ({0}MB)", BytesToMegabytes(quota.MaxFileSize)));
                }

                if (_coreBaseSettings.Standalone)
                {
                    var tenantQuotaSettings = settingsManager.Load<TenantQuotaSettings>();
                    if (!tenantQuotaSettings.DisableQuota)
                    {
                        if (quota.MaxTotalSize != 0 && quota.MaxTotalSize < _currentSize + size)
                        {
                            throw new TenantQuotaException(string.Format("Exceeded maximum amount of disk quota ({0}MB)", BytesToMegabytes(quota.MaxTotalSize)));
                        }
                    }
                }
                else
                {
                    if (quota.MaxTotalSize != 0 && quota.MaxTotalSize < _currentSize + size)
                    {
                        throw new TenantQuotaException(string.Format("Exceeded maximum amount of disk quota ({0}MB)", BytesToMegabytes(quota.MaxTotalSize)));
                    }
                }
            }
            var quotaSettings = settingsManager.Load<TenantUserQuotaSettings>();

            if (quotaSettings.EnableUserQuota)
            {
                var userQuotaSettings = settingsManager.Load<UserQuotaSettings>(ownedId);
                var quotaLimit = userQuotaSettings.UserQuota;

                if (quotaLimit != -1)
                {
                    var usedSpace = quotaservice.FindUserQuotaRows(_tenant, ownedId)
                        .Where(r => !string.IsNullOrEmpty(r.Tag))
                        .Sum(r => r.Counter);

                    var userUsedSpace = Math.Max(0, usedSpace);

                    if (quotaLimit - userUsedSpace < size)
                    {
                        throw new TenantQuotaException(string.Format("Exceeds the maximum file size ({0}MB)", BytesToMegabytes(quotaLimit)));
                    }
                }
            }
        }

        #endregion

        public long QuotaCurrentGet()
        {
            return _currentSize;
        }

        private void SetTenantQuotaRow(string module, string domain, long size, string dataTag, bool exchange, Guid userId)
        {
            var quotaservice = _serviceProvider.GetRequiredService<DbMailQuotaService>();

            quotaservice.SetTenantQuotaRow(
                new TenantQuotaRow
                {
                    Tenant = _tenant,
                    Path = string.Format("/{0}/{1}", module, domain),
                    Counter = size,
                    Tag = dataTag,
                    UserId = userId
                },
                exchange);
        }

        private static bool UsedInQuota(string tag)
        {
            return !string.IsNullOrEmpty(tag) && new Guid(tag) != Guid.Empty;
        }

        private static double BytesToMegabytes(long bytes)
        {
            return Math.Round(bytes / 1024d / 1024d, 1);
        }

        public void QuotaUsedCheck(long size)
        {
            throw new NotImplementedException();
        }
    }
}
