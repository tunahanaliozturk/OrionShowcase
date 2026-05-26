namespace Moongazing.OrionShowcase.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Moongazing.OrionAudit;
using Moongazing.OrionPatch.EntityFrameworkCore;
using Moongazing.OrionShowcase.Domain.Accounts;
using Moongazing.OrionShowcase.Domain.Customers;
using Moongazing.OrionShowcase.Infrastructure.Audit;
using Moongazing.OrionShowcase.Infrastructure.Idempotency;

public sealed class BankingDbContext : DbContext
{
    public BankingDbContext(DbContextOptions<BankingDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<CommandAuditEntry> CommandAuditEntries => Set<CommandAuditEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BankingDbContext).Assembly);
        modelBuilder.ApplyOrionPatchConfiguration();
        // OrionAudit table mappings (AuditLog + SnapshotCursor + Capture queue). The
        // DbContext-aware overload reaches into the application service provider for any
        // custom columns registered via AddOrionAudit's AddColumn fluent surface.
        modelBuilder.ApplyOrionAuditConfigurations(this);
    }
}
