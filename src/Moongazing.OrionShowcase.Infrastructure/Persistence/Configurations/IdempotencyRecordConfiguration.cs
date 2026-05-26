namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moongazing.OrionShowcase.Infrastructure.Idempotency;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("idempotency_records");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(128);
        builder.Property(x => x.RequestHash).HasColumnName("request_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResponseJson).HasColumnName("response_json").HasColumnType("jsonb");
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completed_at_utc");
    }
}
