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
            .HasAnnotation(EncryptedAnnotation, true);   // OrionVault: randomized ciphertext (Base64 text)

        // OrionVault deterministic blind index over the national id. Stored as raw bytea (NOT
        // encrypted): the value is already a keyed HMAC digest. Indexed so the uniqueness check
        // and "find by national id" run as a single equality seek without decrypting any row.
        //
        // Nullable: the column was introduced on an already-populated table, so pre-existing rows
        // keep a null blind index until a production backfill recomputes it. New registrations
        // always set it.
        builder.Property(c => c.NationalIdIndex)
            .HasColumnName("national_id_index")
            .HasColumnType("bytea");

        // UNIQUE FILTERED index: enforces national-id uniqueness at the database level so a race
        // between two concurrent registrations cannot insert two customers with the same national
        // id even if both pass the async uniqueness validator. The "national_id_index IS NOT NULL"
        // filter excludes legacy null rows from the uniqueness constraint, so the not-yet-backfilled
        // existing customers do not collide with each other.
        builder.HasIndex(c => c.NationalIdIndex)
            .HasDatabaseName("ix_customers_national_id_index")
            .IsUnique()
            .HasFilter("national_id_index IS NOT NULL");

        builder.Property(c => c.Email).HasColumnName("email").HasAnnotation(EncryptedAnnotation, true);
        builder.Property(c => c.Phone).HasColumnName("phone").HasAnnotation(EncryptedAnnotation, true);
        builder.Property(c => c.RegisteredAt).HasColumnName("registered_at");

        builder.Ignore(c => c.DomainEvents);
    }
}
