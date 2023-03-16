using ASC.Common.Mapping;

namespace ASC.Mail.Core.Dao.Entities;

public class DbMailQuotaRow : BaseEntity, IMapFrom<TenantQuota>
{
    public int Tenant { get; set; }
    public string Path { get; set; }
    public long Counter { get; set; }
    public string Tag { get; set; }
    public DateTime LastModified { get; set; }
    public Guid UserId { get; set; }
    public override object[] GetKeys()
    {
        return new object[] { Tenant, UserId, Path };
    }
}

public static class DbMailQuotaRowExtension
{
    public static ModelBuilderWrapper AddDbMailQuotaRow(this ModelBuilderWrapper modelBuilder)
    {
        modelBuilder
            .Add(MySqlAddDbMailQuotaRow, Provider.MySql);

        return modelBuilder;
    }
    public static void MySqlAddDbMailQuotaRow(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbMailQuotaRow>(entity =>
        {
            entity.HasKey(e => new { e.Tenant, e.UserId, e.Path })
                .HasName("PRIMARY");

            entity.ToTable("tenants_quotarow")
                .HasCharSet("utf8");

            entity.HasIndex(e => e.LastModified)
                .HasDatabaseName("last_modified");

            entity.Property(e => e.Tenant).HasColumnName("tenant");

            entity.Property(e => e.Path)
                .HasColumnName("path")
                .HasColumnType("varchar(255)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Counter)
                .HasColumnName("counter")
                .HasDefaultValueSql("'0'");

            entity.Property(e => e.LastModified)
                .HasColumnName("last_modified")
                .HasColumnType("timestamp");

            entity.Property(e => e.Tag)
                .HasColumnName("tag")
                .HasColumnType("varchar(1024)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.UserId)
                .HasColumnName("user_id")
                .HasColumnType("char(36)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");
        });
    }
}
