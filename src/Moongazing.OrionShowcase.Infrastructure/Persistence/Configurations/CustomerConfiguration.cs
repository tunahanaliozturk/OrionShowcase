namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Domain.ValueObjects;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
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
            .HasMaxLength(11);

        builder.Property(c => c.Email).HasColumnName("email").HasMaxLength(256);
        builder.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(32);
        builder.Property(c => c.RegisteredAt).HasColumnName("registered_at");

        builder.Ignore(c => c.DomainEvents);
    }
}
