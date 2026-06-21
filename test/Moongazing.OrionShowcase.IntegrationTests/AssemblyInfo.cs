// The end-to-end integration tests each boot the full application against a freshly provisioned
// database. Running them sequentially keeps resource use bounded and the behaviour deterministic
// when several classes share the one CI Postgres server, and avoids many app hosts starting at once.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
