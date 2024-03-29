﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
namespace ASC.Mail.Core.Dao.Entities;

public partial class MailAttachment : BaseEntity
{
    public int Id { get; set; }
    public int IdMail { get; set; }
    public string Name { get; set; }
    public string StoredName { get; set; }
    public string Type { get; set; }
    public long Size { get; set; }
    public bool NeedRemove { get; set; }
    public int FileNumber { get; set; }
    public string ContentId { get; set; }
    public int Tenant { get; set; }
    public int IdMailbox { get; set; }

    public MailMail Mail { get; set; }

    public override object[] GetKeys() => new object[] { Id };
}

public static class MailAttachmentExtension
{
    public static ModelBuilderWrapper AddMailAttachment(this ModelBuilderWrapper modelBuilder)
    {
        modelBuilder
            .Add(MySqlAddMailAttachment, Provider.MySql);

        return modelBuilder;
    }

    public static void MySqlAddMailAttachment(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MailAttachment>(entity =>
        {
            entity.ToTable("mail_attachment");

            entity.HasIndex(e => new { e.IdMail, e.ContentId })
                .HasDatabaseName("id_mail");

            entity.Property(e => e.IdMail)
                .HasColumnName("id_mail")
                .HasColumnType("int(11)");

            entity.HasIndex(e => new { e.IdMailbox, e.Tenant })
                .HasDatabaseName("id_mailbox");

            entity.Property(e => e.IdMailbox)
                .HasColumnName("id_mailbox")
                .HasColumnType("int(11)");

            entity.HasIndex(e => new { e.Tenant, e.IdMail })
                .HasDatabaseName("tenant");

            entity.HasKey(e => e.Id)
                .HasName("PRIMARY");

            entity.Property(e => e.Id)
                .HasColumnType("int(11)")
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.ContentId)
                .HasColumnName("content_id")
                .HasColumnType("varchar(255)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasColumnName("name")
                .HasColumnType("varchar(255)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.StoredName)
                .HasColumnName("stored_name")
                .HasColumnType("varchar(255)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Type)
                .HasColumnName("type")
                .HasColumnType("varchar(255)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Size)
                .HasColumnName("size")
                .HasColumnType("bigint(20)");

            entity.Property(e => e.NeedRemove)
                .HasColumnName("need_remove")
                .HasColumnType("int(11)");

            entity.Property(e => e.FileNumber)
                .HasColumnName("file_number")
                .HasColumnType("int(11)");

            entity.Property(e => e.Tenant)
                .HasColumnName("tenant")
                .HasColumnType("int(11)");

            entity.HasOne(a => a.Mail)
                .WithMany(m => m.Attachments)
                .HasForeignKey(a => a.IdMail);
        });
    }
}
