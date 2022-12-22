using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore.Infrastructure;
using CrmContact = ASC.Mail.Core.Dao.Entities.CrmContact;
using CrmTag = ASC.Mail.Core.Dao.Entities.CrmTag;
using MailFolder = ASC.Mail.Core.Dao.Entities.MailFolder;

namespace ASC.Mail.Core.Dao.Context;

public class MySqlMailDbContext : MailDbContext { }

public class PostgreSqlMailDbContext : MailDbContext { }

public class MailDbContext : DbContext
{
    public static readonly ServerVersion ServerVersion = ServerVersion.Parse("8.0.25");

    protected Provider _provider;

    public ConnectionStringSettings ConnectionStringSettings { get; set; }

    internal string MigrateAssembly { get; set; }

    internal ILoggerFactory LoggerFactory { get; set; }

    protected virtual Dictionary<Provider, Func<MailDbContext>> ProviderContext
    {
        get
        {
            return new Dictionary<Provider, Func<MailDbContext>>()
            {
                { Provider.MySql, () => new MySqlMailDbContext() } ,
                { Provider.PostgreSql, () => new PostgreSqlMailDbContext() } ,
            };
        }
    }

    public void Migrate()
    {
        if (ProviderContext != null)
        {
            Provider providerByConnectionString = GetProviderByConnectionString();
            using MailDbContext baseDbContext = ProviderContext[providerByConnectionString]();
            baseDbContext.ConnectionStringSettings = ConnectionStringSettings;
            baseDbContext.LoggerFactory = LoggerFactory;
            baseDbContext.MigrateAssembly = MigrateAssembly;
            baseDbContext.Database.Migrate();
        }
        else
        {
            Database.Migrate();
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseLoggerFactory(LoggerFactory);
        optionsBuilder.EnableSensitiveDataLogging();
        _provider = GetProviderByConnectionString();
        switch (_provider)
        {
            case Provider.MySql:
                optionsBuilder.UseMySql(ConnectionStringSettings.ConnectionString, ServerVersion, delegate (MySqlDbContextOptionsBuilder providerOptions)
                {
                    if (!string.IsNullOrEmpty(MigrateAssembly))
                    {
                        providerOptions.MigrationsAssembly(MigrateAssembly);
                    }

                    providerOptions.EnableRetryOnFailure(15, TimeSpan.FromSeconds(30.0), null);
                });
                break;
            case Provider.PostgreSql:
                optionsBuilder.UseNpgsql(ConnectionStringSettings.ConnectionString);
                break;
        }
    }

    public Provider GetProviderByConnectionString()
    {
        string providerName = ConnectionStringSettings.ProviderName;
        string text = providerName;
        if (!(text == "MySql.Data.MySqlClient"))
        {
            if (text == "Npgsql")
            {
                return Provider.PostgreSql;
            }

            return Provider.MySql;
        }

        return Provider.MySql;
    }

    #region DbSets

    public DbSet<MailAlert> MailAlerts { get; set; }

    public DbSet<MailAttachment> MailAttachments { get; set; }

    public DbSet<MailChain> MailChains { get; set; }

    public DbSet<MailChainXCrmEntity> MailChainXCrmEntities { get; set; }

    public DbSet<MailContactInfo> MailContactInfos { get; set; }

    public DbSet<MailContact> MailContacts { get; set; }

    public DbSet<MailDisplayImages> MailDisplayImages { get; set; }

    public DbSet<MailFilter> MailFilters { get; set; }

    public DbSet<MailFolder> MailFolders { get; set; }

    public DbSet<MailFolderCounters> MailFolderCounters { get; set; }

    public DbSet<MailImapFlags> MailImapFlags { get; set; }

    public DbSet<MailImapSpecialMailbox> MailImapSpecialMailboxes { get; set; }

    public DbSet<MailMail> MailMails { get; set; }

    public DbSet<MailMailbox> MailMailboxes { get; set; }

    public DbSet<MailMailboxAutoreply> MailMailboxAutoreplies { get; set; }

    public DbSet<MailMailboxAutoreplyHistory> MailMailboxAutoreplyHistories { get; set; }

    public DbSet<MailMailboxDomain> MailMailboxDomains { get; set; }

    public DbSet<MailMailboxProvider> MailMailboxProviders { get; set; }

    public DbSet<MailMailboxServer> MailMailboxServers { get; set; }

    public DbSet<MailMailboxSignature> MailMailboxSignatures { get; set; }

    public DbSet<MailPopUnorderedDomain> MailPopUnorderedDomains { get; set; }

    public DbSet<MailServerAddress> MailServerAddresses { get; set; }

    public DbSet<MailServerDns> MailServerDnses { get; set; }

    public DbSet<MailServerDomain> MailServerDomains { get; set; }

    public DbSet<MailServerMailGroup> MailServerMailGroups { get; set; }

    public DbSet<MailServerMailGroupXMailServerAddress> MailServerMailGroupXMailServerAddresses { get; set; }

    public DbSet<MailServerServer> MailServerServers { get; set; }

    public DbSet<MailServerServerType> MailServerServerTypes { get; set; }

    public DbSet<MailServerServerXTenant> MailServerServerXTenants { get; set; }

    public DbSet<MailTag> MailTags { get; set; }

    public DbSet<CrmTag> CrmTags { get; set; }

    public DbSet<CrmEntityTag> CrmEntityTags { get; set; }

    public DbSet<MailTagAddresses> MailTagAddresses { get; set; }

    public DbSet<MailTagMail> MailTagMails { get; set; }

    public DbSet<MailUserFolder> MailUserFolders { get; set; }

    public DbSet<MailUserFolderTree> MailUserFolderTrees { get; set; }

    public DbSet<MailUserFolderXMail> MailUserFolderXMails { get; set; }

    public DbSet<CrmContact> CrmContacts { get; set; }

    public DbSet<CrmContactInfo> CrmContactInfos { get; set; }

    public DbSet<CrmCurrencyInfo> CrmCurrencyInfos { get; set; }

    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ModelBuilderWrapper
            .From(modelBuilder, _provider)
            .AddDbTenant()
            .AddMailAlert()
            .AddMailAttachment()
            .AddMailChain()
            .AddMailChainXCrmEntity()
            .AddMailContactInfo()
            .AddMailContact()
            .AddMailDisplayImages()
            .AddMailFilter()
            .AddMailFolder()
            .AddMailFolderCounters()
            .AddMailImapFlags()
            .AddMailImapSpecialMailbox()
            .AddMailMail()
            .AddMailMailbox()
            .AddMailMailboxAutoreply()
            .AddMailMailboxAutoreplyHistory()
            .AddMailMailboxDomain()
            .AddMailMailboxProvider()
            .AddMailMailboxServer()
            .AddMailMailboxSignature()
            .AddMailPopUnorderedDomain()
            .AddMailServerAddress()
            .AddMailServerDns()
            .AddMailServerDomain()
            .AddMailServerMailGroup()
            .AddMailServerMailGroupXMailServerAddress()
            .AddMailServerServer()
            .AddMailServerServerType()
            .AddMailServerServerXTenant()
            .AddMailTag()
            .AddCrmTag()
            .AddCrmEntityTag()
            .AddMailTagAddresses()
            .AddMailTagMail()
            .AddMailUserFolder()
            .AddMailUserFolderTree()
            .AddMailUserFolderXMail()
            .AddCrmContactMail()
            .AddCrmContactInfo()
            .AddCrmCurrencyInfo();
    }
}