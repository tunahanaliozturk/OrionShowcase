namespace Moongazing.OrionShowcase.Api.Authorization;

/// <summary>
/// The banking permission set enforced by OrionGrant. Permissions are colon-separated hierarchies
/// matched with wildcard semantics (for example a <c>customers:*</c> grant covers both
/// <see cref="CustomersRead"/> and <see cref="CustomersWrite"/>).
/// </summary>
public static class BankingPermissions
{
    /// <summary>Read customer records.</summary>
    public const string CustomersRead = "customers:read";

    /// <summary>Create or modify customer records.</summary>
    public const string CustomersWrite = "customers:write";

    /// <summary>Open a new account.</summary>
    public const string AccountsOpen = "accounts:open";

    /// <summary>Read account state and activity (for example the SSE activity stream).</summary>
    public const string AccountsRead = "accounts:read";

    /// <summary>Move money between accounts.</summary>
    public const string AccountsTransfer = "accounts:transfer";

    /// <summary>Administer API keys (rotate, bulk-revoke) and read delivery diagnostics.</summary>
    public const string AdminKeysManage = "admin:keys";
}
