namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.ValueObjects;
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    // OrionVault encryption annotation key. IsEncrypted() extension only supports
    // PropertyBuilder<string>/<byte[]>; for value-converted Tckn we apply the same
    // annotation manually so the OrionVault model customizer recognises it.
    private const string EncryptedAnnotation = "OrionVault:Encrypted";

    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, v => new CustomerId(v))
            .HasColumnName("id");

        builder.Property(c => c.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();

        builder.Property(c => c.NationalId)
            .HasConversion(v => v.Value, s => new Tckn(s))
            .HasColumnName("national_id")
            .HasAnnotation(EncryptedAnnotation, true);   // OrionVault: stored as bytea

        builder.Property(c => c.Email).HasColumnName("email").HasAnnotation(EncryptedAnnotation, true);
        builder.Property(c => c.Phone).HasColumnName("phone").HasAnnotation(EncryptedAnnotation, true);
        builder.Property(c => c.RegisteredAt).HasColumnName("registered_at");

        builder.Ignore(c => c.DomainEvents);
    }
}
