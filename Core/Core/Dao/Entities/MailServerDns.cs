﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
namespace ASC.Mail.Core.Dao.Entities;

public partial class MailServerDns : BaseEntity
{
    public uint Id { get; set; }
    public int Tenant { get; set; }
    public string IdUser { get; set; }
    public int IdDomain { get; set; }
    public string DkimSelector { get; set; }
    public string DkimPrivateKey { get; set; }
    public string DkimPublicKey { get; set; }
    public int DkimTtl { get; set; }
    public bool DkimVerified { get; set; }
    public DateTime? DkimDateChecked { get; set; }
    public string DomainCheck { get; set; }
    public string Spf { get; set; }
    public int SpfTtl { get; set; }
    public bool SpfVerified { get; set; }
    public DateTime? SpfDateChecked { get; set; }
    public string Mx { get; set; }
    public int MxTtl { get; set; }
    public bool MxVerified { get; set; }
    public DateTime? MxDateChecked { get; set; }
    public DateTime TimeModified { get; set; }

    public override object[] GetKeys() => new object[] { Id };
}

public static class MailServerDnsExtension
{
    public static ModelBuilderWrapper AddMailServerDns(this ModelBuilderWrapper modelBuilder)
    {
        modelBuilder
            .Add(MySqlAddMailServerDns, Provider.MySql);

        return modelBuilder;
    }

    public static void MySqlAddMailServerDns(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MailServerDns>(entity =>
        {
            entity.ToTable("mail_server_dns");

            entity.HasKey(e => e.Id)
                .HasName("PRIMARY");

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("int(11) unsigned")
                .ValueGeneratedOnAdd();

            entity.HasIndex(e => new { e.IdDomain, e.Tenant, e.IdUser })
                .HasDatabaseName("id_domain_tenant_id_user");

            entity.Property(e => e.Tenant)
                .HasColumnName("tenant")
                .HasColumnType("int(11)");

            entity.Property(e => e.DkimPrivateKey)
                .HasColumnName("dkim_private_key")
                .HasColumnType("text")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.DkimPublicKey)
                .HasColumnName("dkim_public_key")
                .HasColumnType("text")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.DkimSelector)
                .IsRequired()
                .HasColumnName("dkim_selector")
                .HasColumnType("varchar(63)")
                .HasDefaultValueSql("'dkim'")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.DkimTtl)
                .HasColumnName("dkim_ttl")
                .HasColumnType("int(11)")
                .HasDefaultValueSql("'300'");

            entity.Property(e => e.DkimVerified)
                .HasColumnName("dkim_verified");

            entity.Property(e => e.MxVerified)
                .HasColumnName("mx_verified");

            entity.Property(e => e.DomainCheck)
                .HasColumnName("domain_check")
                .HasColumnType("text")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.IdDomain)
                .HasColumnName("id_domain")
                .HasColumnType("int(11)")
                .HasDefaultValueSql("'-1'");

            entity.Property(e => e.DkimDateChecked)
                .HasColumnName("dkim_date_checked")
                .HasColumnType("datetime");

            entity.Property(e => e.MxDateChecked)
                .HasColumnName("mx_date_checked")
                .HasColumnType("datetime");

            entity.Property(e => e.TimeModified)
                .HasColumnName("time_modified")
                .HasColumnType("timestamp");

            entity.Property(e => e.IdUser)
                .IsRequired()
                .HasColumnName("id_user")
                .HasColumnType("varchar(255)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Mx)
                .HasColumnName("mx")
                .HasColumnType("varchar(255)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.MxTtl)
                .HasColumnName("mx_ttl")
                .HasColumnType("int(11)")
                .HasDefaultValueSql("'300'");

            entity.Property(e => e.Spf)
                .HasColumnName("spf")
                .HasColumnType("text")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.SpfTtl)
                .HasColumnName("spf_ttl")
                .HasColumnType("int(11)")
                .HasDefaultValueSql("'300'");

            entity.Property(e => e.SpfVerified)
                .HasColumnName("spf_verified");

            entity.Property(e => e.SpfDateChecked)
                .HasColumnName("spf_date_checked")
                .HasColumnType("datetime");

            entity.Property(e => e.TimeModified)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate();
        });
    }
}
