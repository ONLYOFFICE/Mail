﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>

using Alias = ASC.Mail.Server.Core.Entities.Alias;
using ApiKeys = ASC.Mail.Server.Core.Entities.ApiKeys;
using Mailbox = ASC.Mail.Server.Core.Entities.Mailbox;

namespace ASC.Mail.Server.Core.Dao;

public partial class MailServerDbContext : DbContext
{
    public MailServerDbContext() { }

    public MailServerDbContext(DbContextOptions<MailServerDbContext> options) : base(options) { }

    public virtual DbSet<Alias> Alias { get; set; }
    public virtual DbSet<ApiKeys> ApiKeys { get; set; }
    public virtual DbSet<Dkim> Dkim { get; set; }
    public virtual DbSet<Domain> Domain { get; set; }
    public virtual DbSet<Mailbox> Mailbox { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ModelBuilderWrapper.From(modelBuilder, Provider.MySql)
            .AddDbTenant();

        modelBuilder.Entity<Alias>(entity =>
        {
            entity.HasKey(e => e.Address)
                .HasName("PRIMARY");

            entity.HasIndex(e => e.Active)
                .HasDatabaseName("active");

            entity.HasIndex(e => e.Domain)
                .HasDatabaseName("domain");

            entity.HasIndex(e => e.Expired)
                .HasDatabaseName("expired");

            entity.HasIndex(e => e.Islist)
                .HasDatabaseName("islist");

            entity.Property(e => e.Address)
                .HasDefaultValueSql("''")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Accesspolicy)
                .HasDefaultValueSql("''")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Active).HasDefaultValueSql("'1'");

            entity.Property(e => e.Created).HasDefaultValueSql("'0000-00-00 00:00:00'");

            entity.Property(e => e.Domain)
                .HasDefaultValueSql("''")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Expired).HasDefaultValueSql("'9999-12-31 00:00:00'");

            entity.Property(e => e.Goto)
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Moderators)
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Modified).HasDefaultValueSql("'0000-00-00 00:00:00'");

            entity.Property(e => e.Name)
                .HasDefaultValueSql("''")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");
        });

        modelBuilder.Entity<ApiKeys>(entity =>
        {
            entity.Property(e => e.AccessToken)
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Active).HasDefaultValueSql("'1'");
        });

        modelBuilder.Entity<Dkim>(entity =>
        {
            entity.HasIndex(e => e.DomainName)
                .HasDatabaseName("domain_name");

            entity.Property(e => e.DomainName)
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.Property(e => e.PrivateKey)
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.Property(e => e.PublicKey)
                .HasCharSet("latin1")
                .UseCollation("latin1_swedish_ci");

            entity.Property(e => e.Selector)
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");
        });

        modelBuilder.Entity<Domain>(entity =>
        {
            entity.HasKey(e => e.DomainName)
                .HasName("PRIMARY");

            entity.HasIndex(e => e.Active)
                .HasDatabaseName("active");

            entity.HasIndex(e => e.Backupmx)
                .HasDatabaseName("backupmx");

            entity.HasIndex(e => e.Expired)
                .HasDatabaseName("expired");

            entity.Property(e => e.DomainName)
                .HasDefaultValueSql("''")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Active).HasDefaultValueSql("'1'");

            entity.Property(e => e.Created).HasDefaultValueSql("'0000-00-00 00:00:00'");

            entity.Property(e => e.Description)
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Disclaimer)
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Expired).HasDefaultValueSql("'9999-12-31 00:00:00'");

            entity.Property(e => e.Modified).HasDefaultValueSql("'0000-00-00 00:00:00'");

            entity.Property(e => e.Settings)
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Transport)
                .HasDefaultValueSql("'dovecot'")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");
        });

        modelBuilder.Entity<Mailbox>(entity =>
        {
            entity.HasKey(e => e.Username)
                .HasName("PRIMARY");

            entity.HasIndex(e => e.Active)
                .HasDatabaseName("active");

            entity.HasIndex(e => e.Department)
                .HasDatabaseName("department");

            entity.HasIndex(e => e.Domain)
                .HasDatabaseName("domain");

            entity.HasIndex(e => e.Employeeid)
                .HasDatabaseName("employeeid");

            entity.HasIndex(e => e.Enabledeliver)
                .HasDatabaseName("enabledeliver");

            entity.HasIndex(e => e.Enabledoveadm)
                .HasDatabaseName("enabledoveadm");

            entity.HasIndex(e => e.Enableimap)
                .HasDatabaseName("enableimap");

            entity.HasIndex(e => e.Enableimapsecured)
                .HasDatabaseName("enableimapsecured");

            entity.HasIndex(e => e.Enableinternal)
                .HasDatabaseName("enableinternal");

            entity.HasIndex(e => e.Enablelda)
                .HasDatabaseName("enablelda");

            entity.HasIndex(e => e.EnablelibStorage)
                .HasDatabaseName("enablelib-storage");

            entity.HasIndex(e => e.Enablelmtp)
                .HasDatabaseName("enablelmtp");

            entity.HasIndex(e => e.Enablemanagesieve)
                .HasDatabaseName("enablemanagesieve");

            entity.HasIndex(e => e.Enablemanagesievesecured)
                .HasDatabaseName("enablemanagesievesecured");

            entity.HasIndex(e => e.Enablepop3)
                .HasDatabaseName("enablepop3");

            entity.HasIndex(e => e.Enablepop3secured)
                .HasDatabaseName("enablepop3secured");

            entity.HasIndex(e => e.Enablesieve)
                .HasDatabaseName("enablesieve");

            entity.HasIndex(e => e.Enablesievesecured)
                .HasDatabaseName("enablesievesecured");

            entity.HasIndex(e => e.Enablesmtp)
                .HasDatabaseName("enablesmtp");

            entity.HasIndex(e => e.Enablesmtpsecured)
                .HasDatabaseName("enablesmtpsecured");

            entity.HasIndex(e => e.Expired)
                .HasDatabaseName("expired");

            entity.HasIndex(e => e.Isadmin)
                .HasDatabaseName("isadmin");

            entity.HasIndex(e => e.Isglobaladmin)
                .HasDatabaseName("isglobaladmin");

            entity.HasIndex(e => e.Passwordlastchange)
                .HasDatabaseName("passwordlastchange");

            entity.Property(e => e.Username)
                .HasDefaultValueSql("''")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Active).HasDefaultValueSql("'1'");

            entity.Property(e => e.AllowNets)
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Allowedrecipients)
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Allowedsenders)
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Created).HasDefaultValueSql("'0000-00-00 00:00:00'");

            entity.Property(e => e.Department)
                .HasDefaultValueSql("''")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Disclaimer)
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Domain)
                .HasDefaultValueSql("''")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Employeeid)
                .HasDefaultValueSql("''")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Enabledeliver).HasDefaultValueSql("'1'");

            entity.Property(e => e.Enabledoveadm).HasDefaultValueSql("'1'");

            entity.Property(e => e.Enableimap).HasDefaultValueSql("'1'");

            entity.Property(e => e.Enableimapsecured).HasDefaultValueSql("'1'");

            entity.Property(e => e.Enableinternal).HasDefaultValueSql("'1'");

            entity.Property(e => e.Enablelda).HasDefaultValueSql("'1'");

            entity.Property(e => e.EnablelibStorage).HasDefaultValueSql("'1'");

            entity.Property(e => e.Enablelmtp).HasDefaultValueSql("'1'");

            entity.Property(e => e.Enablemanagesieve).HasDefaultValueSql("'1'");

            entity.Property(e => e.Enablemanagesievesecured).HasDefaultValueSql("'1'");

            entity.Property(e => e.Enablepop3).HasDefaultValueSql("'1'");

            entity.Property(e => e.Enablepop3secured).HasDefaultValueSql("'1'");

            entity.Property(e => e.Enablesieve).HasDefaultValueSql("'1'");

            entity.Property(e => e.Enablesievesecured).HasDefaultValueSql("'1'");

            entity.Property(e => e.Enablesmtp).HasDefaultValueSql("'1'");

            entity.Property(e => e.Enablesmtpsecured).HasDefaultValueSql("'1'");

            entity.Property(e => e.Expired).HasDefaultValueSql("'9999-12-31 00:00:00'");

            entity.Property(e => e.Language)
                .HasDefaultValueSql("'en_US'")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Lastlogindate).HasDefaultValueSql("'0000-00-00 00:00:00'");

            entity.Property(e => e.Lastloginprotocol)
                .HasDefaultValueSql("''")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.LocalPart)
                .HasDefaultValueSql("''")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Maildir)
                .HasDefaultValueSql("''")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Modified).HasDefaultValueSql("'0000-00-00 00:00:00'");

            entity.Property(e => e.Name)
                .HasDefaultValueSql("''")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Password)
                .HasDefaultValueSql("''")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Passwordlastchange).HasDefaultValueSql("'0000-00-00 00:00:00'");

            entity.Property(e => e.Rank)
                .HasDefaultValueSql("'normal'")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Rejectedrecipients)
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Rejectedsenders)
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Settings)
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Storagebasedirectory)
                .HasDefaultValueSql("'/var/vmail'")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Storagenode)
                .HasDefaultValueSql("'vmail1'")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Transport)
                .HasDefaultValueSql("''")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

