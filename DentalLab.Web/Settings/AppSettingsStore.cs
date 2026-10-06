using Microsoft.Data.SqlClient;

namespace DentalLab.Web.Settings;

/// <summary>
/// Small key/value settings saved in dbo.AppSettings (created automatically),
/// e.g. the default UPI ID printed on bills. Values are cached in memory and
/// re-read at most once a minute, and straight after every save.
/// </summary>
public sealed class AppSettingsStore
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

    private readonly string connectionString;
    private readonly ILogger<AppSettingsStore> logger;
    private readonly SemaphoreSlim gate = new(1, 1);

    private volatile Dictionary<string, Entry> values = new(StringComparer.OrdinalIgnoreCase);
    private DateTime loadedAtUtc = DateTime.MinValue;

    public sealed record Entry(string Value, DateTime ModifiedAtUtc, string? ModifiedBy);

    public AppSettingsStore(IConfiguration configuration, ILogger<AppSettingsStore> logger)
    {
        connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        this.logger = logger;
    }

    public async Task InitializeAsync()
    {
        try
        {
            await using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    IF OBJECT_ID(N'dbo.AppSettings', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.AppSettings
                        (
                            SettingKey   NVARCHAR(100)  NOT NULL CONSTRAINT PK_AppSettings PRIMARY KEY,
                            SettingValue NVARCHAR(2000) NOT NULL,
                            ModifiedAt   DATETIME2      NOT NULL CONSTRAINT DF_AppSettings_ModifiedAt DEFAULT SYSUTCDATETIME(),
                            ModifiedBy   NVARCHAR(256)  NULL
                        );
                    END
                    """;
                await command.ExecuteNonQueryAsync();
            }

            await ReloadAsync();
        }
        catch (Exception exception)
        {
            // The app keeps working with built-in defaults.
            logger.LogError(exception, "App settings table could not be prepared; using built-in defaults.");
        }
    }

    /// <summary>Saved value, or <paramref name="fallback"/> when nothing has been saved.</summary>
    public async Task<string> GetAsync(string key, string fallback)
    {
        await EnsureFreshAsync();
        return values.TryGetValue(key, out var entry) ? entry.Value : fallback;
    }

    public async Task<Entry?> GetEntryAsync(string key)
    {
        await EnsureFreshAsync();
        return values.TryGetValue(key, out var entry) ? entry : null;
    }

    public async Task SaveAsync(IReadOnlyDictionary<string, string> settings, string modifiedBy)
    {
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            foreach (var (key, value) in settings)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    MERGE dbo.AppSettings AS target
                    USING (SELECT @key AS SettingKey) AS source ON target.SettingKey = source.SettingKey
                    WHEN MATCHED THEN
                        UPDATE SET SettingValue = @value, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = @by
                    WHEN NOT MATCHED THEN
                        INSERT (SettingKey, SettingValue, ModifiedAt, ModifiedBy) VALUES (@key, @value, SYSUTCDATETIME(), @by);
                    """;
                command.Parameters.AddWithValue("@key", key);
                command.Parameters.AddWithValue("@value", value);
                command.Parameters.AddWithValue("@by", (object?)modifiedBy ?? DBNull.Value);
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }

        await ReloadAsync();
    }

    private async Task EnsureFreshAsync()
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
            loadedAtUtc = DateTime.UtcNow; // keep cached values, retry next minute
            logger.LogWarning(exception, "App settings refresh failed; using cached values.");
        }
    }

    private async Task ReloadAsync()
    {
        await gate.WaitAsync();
        try
        {
            var data = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT SettingKey, SettingValue, ModifiedAt, ModifiedBy FROM dbo.AppSettings";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                data[reader.GetString(0)] = new Entry(
                    reader.GetString(1),
                    DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc),
                    reader.IsDBNull(3) ? null : reader.GetString(3));
            }

            values = data;
            loadedAtUtc = DateTime.UtcNow;
        }
        finally
        {
            gate.Release();
        }
    }
}

/// <summary>Keys and built-in fallbacks for the bill payment defaults.</summary>
public static class BillDefaults
{
    public const string UpiIdKey = "Bill.UpiId";
    public const string PayeeNameKey = "Bill.PayeeName";
    public const string PayToKey = "Bill.PayTo";
    public const string PayAppKey = "Bill.PayApp";

    public const string UpiId = "9966684787@ybl";
    public const string PayeeName = "Natural Dental Labs";
    public const string PayTo = "9966684787";
    public const string PayApp = "PhonePe";
}
