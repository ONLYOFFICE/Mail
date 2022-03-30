﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
using FolderType = ASC.Mail.Enums.FolderType;

namespace ASC.Mail.Core.Dao.Entities;

public partial class MailFolderCounters : BaseEntity
{
    public int Tenant { get; set; }
    public string IdUser { get; set; }
    public FolderType Folder { get; set; }
    public uint UnreadMessagesCount { get; set; }
    public uint TotalMessagesCount { get; set; }
    public uint UnreadConversationsCount { get; set; }
    public uint TotalConversationsCount { get; set; }
    public DateTime TimeModified { get; set; }

    public override object[] GetKeys()
    {
        return new object[] { Tenant, IdUser, Folder };
    }
}

public static class MailFolderCountersExtension
{
    public static ModelBuilder AddMailFolderCounters(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MailFolderCounters>(entity =>
        {
            entity.ToTable("mail_folder_counters");

            entity.HasKey(e => new { e.Tenant, e.IdUser, e.Folder })
                .HasName("PRIMARY");

            entity.Property(e => e.Tenant)
                .HasColumnName("tenant")
                .HasColumnType("int(11)");

            entity.Property(e => e.Folder)
                .HasColumnName("folder")
                .HasColumnType("smallint(5) unsigned");

            entity.Property(e => e.IdUser)
                .HasColumnName("id_user")
                .HasColumnType("varchar(255)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.TimeModified)
                .HasColumnName("time_modified")
                .HasColumnType("timestamp")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate();

            entity.Property(e => e.UnreadMessagesCount)
                .HasColumnName("unread_messages_count")
                .HasColumnType("int(10) unsigned");

            entity.Property(e => e.TotalMessagesCount)
                .HasColumnName("total_messages_count")
                .HasColumnType("int(10) unsigned");

            entity.Property(e => e.UnreadConversationsCount)
                .HasColumnName("unread_conversations_count")
                .HasColumnType("int(10) unsigned");

            entity.Property(e => e.TotalConversationsCount)
                .HasColumnName("total_conversations_count")
                .HasColumnType("int(10) unsigned");
        });

        return modelBuilder;
    }
}
