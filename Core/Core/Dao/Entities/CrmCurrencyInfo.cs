namespace ASC.Mail.Core.Dao.Entities;

public class CrmCurrencyInfo : BaseEntity
{
    public string ResourceKey { get; set; }
    public string Abbreviation { get; set; }
    public string Symbol { get; set; }
    public string CultureName { get; set; }
    public bool IsConvertable { get; set; }
    public bool IsBasic { get; set; }

    public override object[] GetKeys() => new object[] { Abbreviation };
}

public static class CrmCurrencyInfoExtension
{
    public static ModelBuilderWrapper AddCrmCurrencyInfo(this ModelBuilderWrapper modelBuilder)
    {
        modelBuilder
            .Add(MySqlAddCrmCurrencyInfo, Provider.MySql);

        return modelBuilder;
    }

    public static void MySqlAddCrmCurrencyInfo(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CrmCurrencyInfo>(entity =>
        {
            entity.ToTable("crm_currency_info");

            entity.HasKey(e => e.Abbreviation)
                .HasName("PRIMARY");

            entity.Property(e => e.Abbreviation)
                .HasColumnName("abbreviation")
                .HasColumnType("varchar(255)");

            entity.Property(e => e.ResourceKey)
                .IsRequired()
                .HasColumnName("resource_key")
                .HasColumnType("varchar(255)");

            entity.Property(e => e.Symbol)
                .IsRequired()
                .HasColumnName("symbol")
                .HasColumnType("varchar(255)");

            entity.Property(e => e.CultureName)
                .IsRequired()
                .HasColumnName("culture_name")
                .HasColumnType("varchar(255)");

            entity.Property(e => e.IsConvertable)
                .HasColumnName("is_convertable")
                .HasColumnType("tinyint(4)");

            entity.Property(e => e.IsBasic)
                .HasColumnName("is_basic")
                .HasColumnType("tinyint(4)");
        });
    }
}
