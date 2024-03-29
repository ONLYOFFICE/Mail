﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
namespace ASC.Mail.Core.Dao.Entities;

[ElasticsearchType(RelationName = Tables.ContactInfo)]
public partial class MailContactInfo : BaseEntity, ISearchItem
{
    public int Id { get; set; }        
    public int TenantId { get; set; }     
    public string IdUser { get; set; }        
    public int IdContact { get; set; }
    public string Data { get; set; }
    public int Type { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime LastModified { get; set; }

    [Nested]
    public MailContact Contact { get; set; }

    [Ignore]
    public string IndexName => Tables.ContactInfo; 

    public override object[] GetKeys()
    {
        return new object[] { Id };
    }

    public Expression<Func<ISearchItem, object[]>> GetSearchContentFields(SearchSettingsHelper searchSettings)
    {
        return (a) => new[] { Data };
    }
}

public static class MailContactInfoExtension
{
    public static ModelBuilderWrapper AddMailContactInfo(this ModelBuilderWrapper modelBuilder)
    {
        modelBuilder
            .Add(MySqlAddMailContactInfo, Provider.MySql);

        return modelBuilder;
    }

    public static void MySqlAddMailContactInfo(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MailContactInfo>(entity =>
        {
            entity.ToTable("mail_contact_info");

            entity.Ignore(e => e.Contact);
            entity.Ignore(e => e.IndexName);

            entity.HasIndex(e => e.IdContact)
                .HasDatabaseName("contact_id");

            entity.HasIndex(e => e.LastModified)
                .HasDatabaseName("last_modified");

            entity.HasIndex(e => new { e.TenantId, e.IdUser, e.Data })
                .HasDatabaseName("tenant_id_user_data");

            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.Property(e => e.Id)
               .HasColumnName("id")
               .HasColumnType("int(10) unsigned")
               .ValueGeneratedOnAdd();

            entity.Property(e => e.TenantId)
                .HasColumnName("tenant")
                .HasColumnType("int(11)");

            entity.Property(e => e.Data)
                .IsRequired()
                .HasColumnName("data")
                .HasColumnType("varchar(255)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");
                                           
            entity.Property(e => e.IdUser)
                .IsRequired()
                .HasColumnName("id_user")
                .HasColumnType("varchar(255)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");
                            
            entity.Property(e => e.IdContact)
                .HasColumnName("id_contact")
                .HasColumnType("int(11) unsigned");

            entity.Property(e => e.Type)
                .HasColumnName("type")
                .HasColumnType("int(11)");

            entity.Property(e => e.IsPrimary)
                .HasColumnName("is_primary");

            entity.Property(e => e.LastModified)
                .HasColumnName("last_modified")
                .HasColumnType("timestamp")
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate();

            entity.HasOne(a => a.Contact)
                .WithMany(m => m.InfoList)
                .HasForeignKey(a => a.IdContact);
        });
    }
}
