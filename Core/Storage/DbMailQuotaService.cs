using AutoMapper;
using AutoMapper.QueryableExtensions;

namespace ASC.Mail.Core.Storage
{
    [Scope]
    public class DbMailQuotaService : IQuotaService
    {
        private const string tenants_quota = "tenants_quota";
        public const string tenants_quotarow = "tenants_quotarow";
        private readonly MailDbContext mailDbContext;
        private readonly IMapper mapper;

        public DbMailQuotaService(MailDbContext mailDbContext, IMapper mapper)
        {
            this.mailDbContext = mailDbContext;
            this.mapper = mapper;
        }

        public IEnumerable<TenantQuota> GetTenantQuotas()
        {
            return mailDbContext.MailQuotas
            .ProjectTo<TenantQuota>(mapper.ConfigurationProvider)
            .ToList();
        }

        public TenantQuota GetTenantQuota(int id)
        {
            return mailDbContext.MailQuotas.Where(r => r.Tenant == id)
            .ProjectTo<TenantQuota>(mapper.ConfigurationProvider)
            .SingleOrDefault();
        }

        public IEnumerable<TenantQuotaRow> FindTenantQuotaRows(int tenantId)
        {
            return FindUserQuotaRows(tenantId, Guid.Empty);
        }

        public IEnumerable<TenantQuotaRow> FindUserQuotaRows(int tenantId, Guid userId)
        {
            var q = mailDbContext.MailQuotaRows.Where(r => r.UserId == userId);

            if (tenantId != Tenant.DefaultTenant)
            {
                q = q.Where(r => r.Tenant == tenantId);
            }

            return q.ProjectTo<TenantQuotaRow>(mapper.ConfigurationProvider).ToList();
        }

        public TenantQuota SaveTenantQuota(TenantQuota quota)
        {
            ArgumentNullException.ThrowIfNull(quota);

            mailDbContext.AddOrUpdate(mailDbContext.MailQuotas, mapper.Map<TenantQuota, DbMailQuota>(quota));
            mailDbContext.SaveChanges();

            return quota;
        }

        public void RemoveTenantQuota(int id)
        {
            var strategy = mailDbContext.Database.CreateExecutionStrategy();

            strategy.Execute(() =>
            {
                using var tr = mailDbContext.Database.BeginTransaction();
                var d = mailDbContext.MailQuotas
                     .Where(r => r.Tenant == id)
                     .SingleOrDefault();

                if (d != null)
                {
                    mailDbContext.MailQuotas.Remove(d);
                    mailDbContext.SaveChanges();
                }

                tr.Commit();
            });
        }

        public void SetTenantQuotaRow(TenantQuotaRow row, bool exchange)
        {
            ArgumentNullException.ThrowIfNull(row);

            var strategy = mailDbContext.Database.CreateExecutionStrategy();

            strategy.Execute(() =>
            {
                using var tx = mailDbContext.Database.BeginTransaction();


                AddQuota(mailDbContext, row.UserId);
                tx.Commit();
            });

            void AddQuota(MailDbContext mailDbContext, Guid userId)
            {
                var dbTenantQuotaRow = mapper.Map<TenantQuotaRow, DbMailQuotaRow>(row);
                dbTenantQuotaRow.UserId = userId;

                if (exchange)
                {
                    var counter = mailDbContext.MailQuotaRows
                    .Where(r => r.Path == row.Path && r.Tenant == row.Tenant && r.UserId == userId)
                    .Select(r => r.Counter)
                    .Take(1)
                    .FirstOrDefault();

                    dbTenantQuotaRow.Counter = counter + row.Counter;
                }

                mailDbContext.AddOrUpdate(mailDbContext.MailQuotaRows, dbTenantQuotaRow);
                mailDbContext.SaveChanges();
            }
        }
    }
}
