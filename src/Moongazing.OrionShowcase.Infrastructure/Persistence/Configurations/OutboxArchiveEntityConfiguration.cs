namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moongazing.OrionShowcase.Infrastructure.Outbox;

/// <summary>
/// Maps the outbox archive table. Successfully dispatched rows past the retention window are moved
/// here out of the hot outbox so the active claim-query working set stays small while an
/// audit/replay horizon is preserved.
/// </summary>
internal sealed class OutboxArchiveEntityConfiguration : IEntityTypeConfiguration<OutboxArchiveEntity>
{
    public void Configure(EntityTypeBuilder<OutboxArchiveEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("orion_patch_outbox_archive");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MessageType).HasColumnName("message_type").HasMaxLength(256).IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").IsRequired();
        builder.Property(x => x.HeadersJson).HasColumnName("headers_json");
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128);
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.Property(x => x.EnqueuedAtUtc).HasColumnName("enqueued_at_utc");
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.LastError).HasColumnName("last_error");
        builder.Property(x => x.ProcessedAtUtc).HasColumnName("processed_at_utc");
        builder.Property(x => x.ArchivedAtUtc).HasColumnName("archived_at_utc");
        builder.HasIndex(x => x.ProcessedAtUtc).HasDatabaseName("ix_orion_patch_outbox_archive_processed_at");
    }
}
