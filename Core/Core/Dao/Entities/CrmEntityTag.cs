﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
namespace ASC.Mail.Core.Dao.Entities;

public partial class CrmEntityTag
{
    public int TagId { get; set; }
    public int EntityId { get; set; }
    public int EntityType { get; set; }
}

public static class CrmEntityTagExtension
{
    public static ModelBuilderWrapper AddCrmEntityTag(this ModelBuilderWrapper modelBuilder)
    {
        modelBuilder
            .Add(MySqlAddCrmEntityTag, Provider.MySql);

        return modelBuilder;
    }

    public static void MySqlAddCrmEntityTag(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CrmEntityTag>(entity =>
        {
            entity.ToTable("crm_entity_tag");

            entity.HasKey(e => new { e.TagId, e.EntityId, e.EntityType })
                .HasName("PRIMARY");

            entity.HasIndex(e => e.TagId)
                .HasDatabaseName("tag_id");

            entity.Property(e => e.TagId)
                .HasColumnName("tag_id")
                .HasColumnType("int(11)");

            entity.Property(e => e.EntityId)
                .HasColumnName("entity_id")
                .HasColumnType("int(11)");

            entity.Property(e => e.EntityType)
                .HasColumnName("entity_type")
                .HasColumnType("int(11)");
        });
    }
}
