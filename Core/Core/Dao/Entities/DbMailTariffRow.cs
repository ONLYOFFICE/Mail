
namespace ASC.Mail.Core.Dao.Entities;

public class DbMailTariffRow : BaseEntity
{
    public int TariffId { get; set; }
    public int Quota { get; set; }
    public int Quantity { get; set; }
    public int Tenant { get; set; }

    public override object[] GetKeys()
    {
        return new object[] { Tenant, TariffId, Quota };
    }
}
public static class DbMailTariffRowExtension
{
    public static ModelBuilderWrapper AddDbMailTariffRow(this ModelBuilderWrapper modelBuilder)
    {
        modelBuilder
            .Add(MySqlAddDbMailTariffRow, Provider.MySql);

        return modelBuilder;
    }

    public static void MySqlAddDbMailTariffRow(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DbMailTariffRow>(entity =>
        {
            entity.ToTable("tenants_tariffrow");

            entity.HasKey(e => new { e.Tenant, e.TariffId, e.Quota })
                .HasName("PRIMARY");

            entity.Property(e => e.TariffId)
                .HasColumnName("tariff_id")
                .HasColumnType("int");

            entity.Property(e => e.Quota)
                .HasColumnName("quota")
                .HasColumnType("int");

            entity.Property(e => e.Quantity)
                .HasColumnName("quantity")
                .HasColumnType("int");

            entity.Property(e => e.Tenant)
                .HasColumnName("tenant")
                .HasColumnType("int");
        });
    }
}
