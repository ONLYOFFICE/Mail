﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASC.Mail.Core.Dao.Entities
{
    [Table("mail_imap_special_mailbox")]
    public partial class MailImapSpecialMailbox
    {
        [Key]
        [Column("server", TypeName = "varchar(255)")]
        public string Server { get; set; }
        [Key]
        [Column("name", TypeName = "varchar(255)")]
        public string Name { get; set; }
        [Column("folder_id", TypeName = "int(11)")]
        public int FolderId { get; set; }
        [Column("skip", TypeName = "int(11)")]
        public bool Skip { get; set; }
    }

    public static class MailImapSpecialMailboxExtension
    {
        public static ModelBuilder AddMailImapSpecialMailbox(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MailImapSpecialMailbox>(entity =>
            {
                entity.HasKey(e => new { e.Server, e.Name })
                    .HasName("PRIMARY");

                entity.Property(e => e.Server)
                    .HasCharSet("utf8")
                    .UseCollation("utf8_general_ci");

                entity.Property(e => e.Name)
                    .HasCharSet("utf8")
                    .UseCollation("utf8_general_ci");
            });

            return modelBuilder;
        }
    }
}