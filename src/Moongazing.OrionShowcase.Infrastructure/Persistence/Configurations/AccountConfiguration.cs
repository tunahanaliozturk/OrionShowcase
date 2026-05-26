namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("accounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, v => new AccountId(v))
            .HasColumnName("id");

        builder.Property(a => a.CustomerId)
            .HasConversion(id => id.Value, v => new CustomerId(v))
            .HasColumnName("customer_id");

        builder.OwnsOne(a => a.Iban, iban =>
        {
            iban.Property(i => i.Value).HasColumnName("iban").HasMaxLength(34).IsRequired();
        });

        builder.OwnsOne(a => a.Balance, money =>
        {
            money.Property(m => m.Amount).HasColumnName("balance_amount").HasColumnType("numeric(20,4)");
            money.Property(m => m.Currency).HasColumnName("balance_currency").HasConversion<string>().HasMaxLength(3);
        });

        builder.Property(a => a.Status).HasConversion<string>().HasColumnName("status").HasMaxLength(16);
        builder.Property(a => a.OpenedAt).HasColumnName("opened_at");

        builder.HasMany(a => a.Transactions).WithOne().HasForeignKey("account_id").OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(a => a.DomainEvents);
    }
}
