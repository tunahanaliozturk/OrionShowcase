namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moongazing.OrionShowcase.Infrastructure.Outbox;

/// <summary>
/// Maps the durable outbox dead-letter store table. Sibling of the OrionPatch outbox table; a row
/// lands here exactly once when an outbox message exhausts its delivery budget.
/// </summary>
internal sealed class OutboxDeadLetterEntityConfiguration : IEntityTypeConfiguration<OutboxDeadLetterEntity>
{
    public void Configure(EntityTypeBuilder<OutboxDeadLetterEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("orion_patch_dead_letter");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MessageType).HasColumnName("message_type").HasMaxLength(256).IsRequired();
        builder.Property(x => x.Payload).HasColumnName("payload").IsRequired();
        builder.Property(x => x.HeadersJson).HasColumnName("headers_json");
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(128);
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.Property(x => x.EnqueuedAtUtc).HasColumnName("enqueued_at_utc");
        builder.Property(x => x.AttemptCount).HasColumnName("attempt_count");
        builder.Property(x => x.FinalError).HasColumnName("final_error").IsRequired();
        builder.Property(x => x.DeadLetteredAtUtc).HasColumnName("dead_lettered_at_utc");
        builder.HasIndex(x => x.DeadLetteredAtUtc).HasDatabaseName("ix_orion_patch_dead_letter_dead_lettered_at");
    }
}
