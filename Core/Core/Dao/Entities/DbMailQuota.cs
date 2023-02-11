using ASC.Common.Mapping;

namespace ASC.Mail.Core.Dao.Entities;

public class DbMailQuota : BaseEntity, IMapFrom<TenantQuota>
{
    public int Tenant { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Features { get; set; }
    public decimal Price { get; set; }
    public string ProductId { get; set; }
    public bool Visible { get; set; }

    public override object[] GetKeys()
    {
        return new object[] { Tenant };
    }

    public void Mapping(AutoMapper.Profile profile)
    {
        profile.CreateMap<TenantQuota, DbMailQuota>();
    }
}
public static class DbMailQuotaExtension
{
    public static ModelBuilderWrapper AddDbMailQuota(this ModelBuilderWrapper modelBuilder)
    {
        modelBuilder
            .Add(MySqlAddDbMailQuota, Provider.MySql)
            .HasData(
                new DbMailQuota
                {
                    Tenant = -1,
                    Name = "trial",
                    Description = null,
                    Features = "trial,audit,ldap,sso,whitelabel,thirdparty,restore,total_size:107374182400,file_size:100,manager:1",
                    Price = 0,
                    ProductId = null,
                    Visible = false
                },
                new DbMailQuota
                {
                    Tenant = -2,
                    Name = "admin",
                    Description = null,
                    Features = "audit,ldap,sso,whitelabel,thirdparty,restore,total_size:107374182400,file_size:1024,manager:1",
                    Price = 30,
                    ProductId = "1002",
                    Visible = true
                },
                new DbMailQuota
                {
                    Tenant = -3,
                    Name = "startup",
                    Description = null,
                    Features = "free,thirdparty,audit,total_size:2147483648,manager:1,room:12,usersInRoom:3",
                    Price = 0,
                    ProductId = null,
                    Visible = false
                }
                );

        return modelBuilder;
    }

    public static void MySqlAddDbMailQuota(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbMailQuota>(entity =>
        {
            entity.HasKey(e => e.Tenant)
                .HasName("PRIMARY");

            entity.ToTable("tenants_quota")
                .HasCharSet("utf8");

            entity.Property(e => e.Tenant)
                .HasColumnName("tenant")
                .ValueGeneratedNever();

            entity.Property(e => e.ProductId)
                .HasColumnName("product_id")
                .HasColumnType("varchar(128)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasColumnType("varchar(128)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Features)
                .HasColumnName("features")
                .HasColumnType("text");

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasColumnType("varchar(128)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Price)
                .HasColumnName("price")
                .HasDefaultValueSql("'0.00'")
                .HasColumnType("decimal(10,2)");

            entity.Property(e => e.Visible)
                .HasColumnName("visible")
                .HasColumnType("tinyint(1)")
                .HasDefaultValueSql("'0'");
        });
    }
}
