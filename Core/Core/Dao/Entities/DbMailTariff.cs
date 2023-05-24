
namespace ASC.Mail.Core.Dao.Entities;

public class DbMailTariff : BaseEntity
{
    public int Id { get; set; }
    public int Tenant { get; set; }
    public DateTime Stamp { get; set; }
    public string CustomerId { get; set; }
    public string Comment { get; set; }
    public DateTime CreateOn { get; set; }

    public override object[] GetKeys()
    {
        return new object[] { Id };
    }
}

public static class DbMailTariffExtension
{
    public static ModelBuilderWrapper AddDbMailTariff(this ModelBuilderWrapper modelBuilder)
    {
        modelBuilder
            .Add(MySqlAddDbMailTariff, Provider.MySql);

        return modelBuilder;
    }
    public static void MySqlAddDbMailTariff(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbMailTariff>(entity =>
        {
            entity.ToTable("tenants_tariff")
                .HasCharSet("utf8");

            entity.HasIndex(e => e.Tenant)
                .HasDatabaseName("tenant");

            entity.Property(e => e.Id).HasColumnName("id");

            entity.Property(e => e.Comment)
                .HasColumnName("comment")
                .HasColumnType("varchar(255)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.CreateOn)
                .HasColumnName("create_on")
                .HasColumnType("timestamp");

            entity.Property(e => e.Stamp)
                .HasColumnName("stamp")
                .HasColumnType("datetime");

            entity.Property(e => e.CustomerId)
                .HasColumnName("customer_id")
                .HasColumnType("varchar(255)")
                .HasCharSet("utf8")
                .UseCollation("utf8_general_ci");

            entity.Property(e => e.Tenant).HasColumnName("tenant");
        });
    }
}
