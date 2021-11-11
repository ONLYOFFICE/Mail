﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ASC.Mail.Core.Dao.Entities
{
    [Table("mail_mailbox_domain")]
    public partial class MailMailboxDomain
    {
        [Key]
        [Column("id", TypeName = "int(11)")]
        public int Id { get; set; }
        [Column("id_provider", TypeName = "int(11)")]
        public int IdProvider { get; set; }
        [Required]
        [Column("name", TypeName = "varchar(255)")]
        public string Name { get; set; }
    }

    public static class MailMailboxDomainExtension
    {
        public static ModelBuilder AddMailMailboxDomain(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MailMailboxDomain>(entity =>
            {
                entity.HasIndex(e => new { e.Name, e.IdProvider })
                    .HasDatabaseName("id_provider");

                entity.Property(e => e.Name)
                    .HasCharSet("utf8")
                    .UseCollation("utf8_general_ci");
            });

            return modelBuilder;
        }
    }
}