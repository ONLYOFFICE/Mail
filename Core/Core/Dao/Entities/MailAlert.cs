﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
namespace ASC.Mail.Core.Dao.Entities;

public partial class MailAlert : BaseEntity
{
    public int Id { get; set; }
    public int Tenant { get; set; }
    public string IdUser { get; set; }
    public int IdMailbox { get; set; }
    public MailAlertTypes Type { get; set; }
    public string Data { get; set; }

    public override object[] GetKeys() => new object[] { Id };
}

public static class MailAlertExtension
{
    public static ModelBuilderWrapper AddMailAlert(this ModelBuilderWrapper modelBuilder)
    {
        modelBuilder
            .Add(MySqlAddMailAlert, Provider.MySql);

        return modelBuilder;
    }

    public static void MySqlAddMailAlert(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MailAlert>(entity =>
        {
            entity.ToTable("mail_alerts");

            entity.HasIndex(e => new { e.Tenant, e.IdUser, e.IdMailbox, e.Type })
                .HasDatabaseName("tenant_id_user_id_mailbox_type");

            entity.HasKey(e => e.Id)
                .HasName("PRIMARY");

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasColumnType("int(11)")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.Tenant)
                .HasColumnName("tenant");

            entity.Property(e => e.IdUser)
                .IsRequired()
                .HasColumnName("id_user")
                .HasColumnType("varchar(255)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.IdMailbox)
                .HasColumnName("id_mailbox")
                .HasColumnType("int(11)")
                .HasDefaultValueSql("'-1'");

            entity.Property(e => e.Type)
                .HasColumnName("type")
                .HasColumnType("int")
                .HasDefaultValueSql("'0'");

            entity.Property(e => e.Data)
                .HasColumnName("data")
                .HasColumnType("mediumtext")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");
        });
    }
}
