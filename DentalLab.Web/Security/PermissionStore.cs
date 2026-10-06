using System.Security.Claims;
using Microsoft.Data.SqlClient;

namespace DentalLab.Web.Security;

/// <summary>
/// Holds the Admin / Staff permission switches.
/// - Saved in the dbo.RolePermissions table (created automatically on first start).
/// - Kept in memory, so checks are instant; re-read from the database at most once a minute
///   (so several web servers stay in sync) and immediately after every save.
/// - A switch that has never been saved uses its default from PermissionCatalog,
///   which matches how the app behaved before this feature existed.
/// - The Owner always has every permission.
/// </summary>
public sealed class PermissionStore
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

    private readonly string connectionString;
    private readonly ILogger<PermissionStore> logger;
    private readonly SemaphoreSlim gate = new(1, 1);

    // (Role, Key) -> allowed. Only contains saved rows; missing = default.
    private volatile Dictionary<(string Role, string Key), bool> saved = new();
    private DateTime loadedAtUtc = DateTime.MinValue;

    public PermissionStore(IConfiguration configuration, ILogger<PermissionStore> logger)
    {
        connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        this.logger = logger;
    }

    /// <summary>Last database problem (shown on the Access Control page), or null.</summary>
    public string? LastError { get; private set; }

    /// <summary>Raised after permissions are saved.</summary>
    public event Action? Changed;

    // ------------------------------------------------------------------ checks

    /// <summary>Does this signed-in user have the permission? (policy "Perm:X" or key "X")</summary>
    public bool IsAllowed(ClaimsPrincipal? user, string policyOrKey)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.IsInRole(PermissionCatalog.OwnerRole))
        {
            return true;
        }

        var definition = PermissionCatalog.Find(policyOrKey);
        if (definition is null)
        {
            return false;
        }

        foreach (var role in PermissionCatalog.ConfigurableRoles)
        {
            if (user.IsInRole(role) && IsAllowedForRole(role, definition))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsAllowedForRole(string role, PermissionDefinition definition)
    {
        if (role == PermissionCatalog.OwnerRole)
        {
            return true;
        }

        return saved.TryGetValue((role, definition.Key), out var allowed)
            ? allowed
            : PermissionCatalog.DefaultFor(role, definition);
    }

    // ------------------------------------------------------------------ loading

    /// <summary>Called once at startup: creates the table if needed and loads it.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            await EnsureTableAsync();
            await ReloadAsync();
        }
        catch (Exception exception)
        {
            // The app keeps working with the default permissions.
            LastError = "Could not prepare the permissions table: " + exception.Message;
            logger.LogError(exception, "Permission store initialisation failed; using default permissions.");
        }
    }

    /// <summary>Re-reads from the database if the cache is older than a minute.</summary>
    public async Task EnsureFreshAsync()
    {
        if (DateTime.UtcNow - loadedAtUtc < RefreshInterval)
        {
            return;
        }

        try
        {
            await ReloadAsync();
        }
        catch (Exception exception)
        {
            // Keep using the cached values; try again next minute.
            loadedAtUtc = DateTime.UtcNow;
            LastError = "Could not refresh permissions: " + exception.Message;
            logger.LogWarning(exception, "Permission refresh failed; keeping cached values.");
        }
    }

    private async Task ReloadAsync()
    {
        await gate.WaitAsync();
        try
        {
            var data = new Dictionary<(string, string), bool>();

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT RoleName, PermissionKey, IsAllowed FROM dbo.RolePermissions";

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                data[(reader.GetString(0), reader.GetString(1))] = reader.GetBoolean(2);
            }

            saved = data;
            loadedAtUtc = DateTime.UtcNow;
            LastError = null;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EnsureTableAsync()
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.RolePermissions
                (
                    RoleName      NVARCHAR(50)  NOT NULL,
                    PermissionKey NVARCHAR(100) NOT NULL,
                    IsAllowed     BIT           NOT NULL,
                    ModifiedAt    DATETIME2     NOT NULL CONSTRAINT DF_RolePermissions_ModifiedAt DEFAULT SYSUTCDATETIME(),
                    ModifiedBy    NVARCHAR(256) NULL,
                    CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleName, PermissionKey)
                );
            END
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    // ------------------------------------------------------------------ saving

    /// <summary>Saves the full Admin/Staff matrix in one transaction, then refreshes the cache.</summary>
    public async Task SaveAsync(IReadOnlyDictionary<(string Role, string Key), bool> matrix, string modifiedBy)
    {
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            foreach (var ((role, key), allowed) in matrix)
            {
                if (!PermissionCatalog.ConfigurableRoles.Contains(role) || PermissionCatalog.Find(key) is null)
                {
                    continue; // never store anything outside the catalogue
                }

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    MERGE dbo.RolePermissions AS target
                    USING (SELECT @role AS RoleName, @key AS PermissionKey) AS source
                       ON target.RoleName = source.RoleName AND target.PermissionKey = source.PermissionKey
                    WHEN MATCHED AND target.IsAllowed <> @allowed THEN
                        UPDATE SET IsAllowed = @allowed, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = @by
                    WHEN NOT MATCHED THEN
                        INSERT (RoleName, PermissionKey, IsAllowed, ModifiedAt, ModifiedBy)
                        VALUES (@role, @key, @allowed, SYSUTCDATETIME(), @by);
                    """;
                command.Parameters.AddWithValue("@role", role);
                command.Parameters.AddWithValue("@key", key);
                command.Parameters.AddWithValue("@allowed", allowed);
                command.Parameters.AddWithValue("@by", (object?)modifiedBy ?? DBNull.Value);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        await ReloadAsync();
        Changed?.Invoke();
    }

    /// <summary>Removes all saved switches, so every permission goes back to its default.</summary>
    public async Task ResetToDefaultsAsync()
    {
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM dbo.RolePermissions";
            await command.ExecuteNonQueryAsync();
        }

        await ReloadAsync();
        Changed?.Invoke();
    }

    /// <summary>When / by whom the permissions were last changed (for the page header).</summary>
    public async Task<(DateTime? At, string? By)> GetLastChangeAsync()
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT TOP 1 ModifiedAt, ModifiedBy FROM dbo.RolePermissions ORDER BY ModifiedAt DESC";
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc),
                        reader.IsDBNull(1) ? null : reader.GetString(1));
            }
        }
        catch
        {
            // informational only
        }

        return (null, null);
    }
}
