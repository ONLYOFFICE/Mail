using ASC.Common.Logging;
using Microsoft.Extensions.Options;

namespace ASC.Mail.Core.Search;

[Scope(Additional = typeof(FactoryIndexerMailMailExtension))]
public sealed class FactoryIndexerMailMail : FactoryIndexer<MailMail>
{
    private readonly Lazy<MailDbContext> _lazyMailDbContext;
    private MailDbContext MailDbContext { get => _lazyMailDbContext.Value; }

    public FactoryIndexerMailMail(
        IOptionsMonitor<ILog> options,
        TenantManager tenantManager,
        SearchSettingsHelper searchSettingsHelper,
        FactoryIndexer factoryIndexer,
        BaseIndexer<MailMail> baseIndexer,
        IServiceProvider serviceProvider,
        DbContextManager<MailDbContext> dbContext,
        ICache cache)
        : base(options, tenantManager, searchSettingsHelper, factoryIndexer, baseIndexer, serviceProvider, cache)
    {
        _lazyMailDbContext = new Lazy<MailDbContext>(() => dbContext.Get("mail"));
    }
}

public class FactoryIndexerMailMailExtension
{
    public static void Register(DIHelper services)
    {
        services.TryAdd<MailMail>();
    }
}
