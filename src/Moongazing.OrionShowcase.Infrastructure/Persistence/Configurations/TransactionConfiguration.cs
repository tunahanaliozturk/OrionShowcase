namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("transactions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, v => new TransactionId(v))
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(t => t.Kind).HasConversion<string>().HasColumnName("kind").HasMaxLength(16);

        builder.OwnsOne(t => t.Amount, m =>
        {
            m.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(20,4)");
            m.Property(x => x.Currency).HasColumnName("currency").HasConversion<string>().HasMaxLength(3);
        });

        builder.OwnsOne(t => t.BalanceAfter, m =>
        {
            m.Property(x => x.Amount).HasColumnName("balance_after_amount").HasColumnType("numeric(20,4)");
            m.Property(x => x.Currency).HasColumnName("balance_after_currency").HasConversion<string>().HasMaxLength(3);
        });

        builder.Property(t => t.IdempotencyKey)
            .HasConversion(k => k.Value, s => new IdempotencyKey(s))
            .HasColumnName("idempotency_key")
            .HasMaxLength(128);

        builder.Property(t => t.At).HasColumnName("at");
    }
}
