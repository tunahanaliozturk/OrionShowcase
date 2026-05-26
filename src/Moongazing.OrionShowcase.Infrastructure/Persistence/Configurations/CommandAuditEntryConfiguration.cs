namespace Moongazing.OrionShowcase.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Moongazing.OrionShowcase.Infrastructure.Audit;

public sealed class CommandAuditEntryConfiguration : IEntityTypeConfiguration<CommandAuditEntry>
{
    public void Configure(EntityTypeBuilder<CommandAuditEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("command_audit_entries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Actor).HasColumnName("actor").HasMaxLength(256).IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequestJson).HasColumnName("request_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ResponseJson).HasColumnName("response_json").HasColumnType("jsonb");
        builder.Property(x => x.Succeeded).HasColumnName("succeeded");
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message");
        builder.Property(x => x.OccurredOnUtc).HasColumnName("occurred_on_utc");
        builder.HasIndex(x => x.OccurredOnUtc);
        builder.HasIndex(x => new { x.Actor, x.Action });
    }
}
