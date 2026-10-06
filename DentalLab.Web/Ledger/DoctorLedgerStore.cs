using System.Data;
using System.Globalization;
using DentalLab.Web.Settings;
using Microsoft.Data.SqlClient;

namespace DentalLab.Web.Ledger;

// ---------------------------------------------------------------------------
//  Doctor dues ("ledger")
//
//  Balance a doctor owes = opening balance (amount pending when tracking started)
//                        + work done (active case lines, by case date)
//                        + bill extras (GST and adjustments of bills saved to the ledger)
//                        − payments received (not voided)
//
//  Old due on a bill for the period From..To
//      Previous balance  = everything above dated before From
//      Payments received = payments dated From .. "payments up to" (default: today)
//      Old due           = Previous balance − Payments received
//  so the bill's grand total is exactly what the doctor owes for work up to To.
// ---------------------------------------------------------------------------

public static class PaymentModes
{
    public static readonly string[] All = ["Cash", "UPI", "Bank transfer", "Cheque", "Other"];
}

public sealed record NewPayment(
    int DocId,
    DateOnly PaymentDate,
    decimal Amount,
    string Mode,
    string? Reference,
    string? Notes);

public sealed record DoctorPayment(
    int PaymentId,
    int DocId,
    string DoctorName,
    DateOnly PaymentDate,
    decimal Amount,
    string Mode,
    string? Reference,
    string? Notes,
    bool IsVoided,
    string? VoidReason,
    DateTime CreatedAtUtc,
    string CreatedBy,
    DateTime? VoidedAtUtc,
    string? VoidedBy);

/// <summary>One doctor's position, as of today.</summary>
public sealed record DoctorDue(
    int DocId,
    string DoctorName,
    bool IsActive,
    decimal Opening,
    decimal ChargesBeforeMonth,
    decimal ChargesThisMonth,
    decimal Received,
    DateOnly? LastPaymentDate)
{
    /// <summary>Pending from earlier months (already billed) after all payments.</summary>
    public decimal PendingFromBills => Opening + ChargesBeforeMonth - Received;

    /// <summary>Including this month's work that is not billed yet.</summary>
    public decimal TotalBalance => PendingFromBills + ChargesThisMonth;
}

/// <summary>How the old due on a bill was worked out.</summary>
public sealed record OldDueInfo(
    DateOnly TrackingStart,
    bool BeforeTracking,
    decimal Opening,
    decimal ChargesBefore,
    decimal PaidBefore,
    decimal ReceivedInWindow,
    int ReceivedCount,
    DateOnly WindowFrom,
    DateOnly WindowTo)
{
    public decimal PreviousBalance => Opening + ChargesBefore - PaidBefore;
    public decimal OldDue => PreviousBalance - ReceivedInWindow;
}

public enum LedgerEntryKind
{
    Opening,
    Work,
    BillExtras,
    Payment
}

public sealed record LedgerEntry(
    DateOnly Date,
    LedgerEntryKind Kind,
    string Description,
    decimal Charge,
    decimal Payment,
    int? PaymentId = null,
    bool IsVoided = false,
    string? Detail = null);

public sealed record SaveBillRequest(
    int DocId,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    decimal Subtotal,
    decimal GstAmount,
    decimal AdjustmentsTotal,
    string? AdjustmentsText,
    decimal OldDue,
    decimal GrandTotal);

public sealed record SavedBill(
    int BillId,
    int DocId,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    decimal Subtotal,
    decimal GstAmount,
    decimal AdjustmentsTotal,
    string? AdjustmentsText,
    decimal OldDue,
    decimal GrandTotal,
    DateTime SavedAtUtc,
    string SavedBy)
{
    /// <summary>The part of the bill that is not case work (GST + adjustments).</summary>
    public decimal Extras => GstAmount + AdjustmentsTotal;
}

/// <summary>
/// Payments, opening balances and saved bills, kept in three tables that are
/// created automatically on start-up (dbo.DoctorPayments, dbo.DoctorOpeningBalances,
/// dbo.DoctorBills). Case work is read live from dbo.Records.
/// </summary>
public sealed class DoctorLedgerStore
{
    public const string StartDateKey = "Ledger.StartDate";

    private readonly string connectionString;
    private readonly AppSettingsStore settings;
    private readonly ILogger<DoctorLedgerStore> logger;

    public DoctorLedgerStore(IConfiguration configuration, AppSettingsStore settings, ILogger<DoctorLedgerStore> logger)
    {
        connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        this.settings = settings;
        this.logger = logger;
    }

    /// <summary>Set when the tables could not be prepared on start-up.</summary>
    public string? StartupError { get; private set; }

    // ------------------------------------------------------------------ setup

    public async Task InitializeAsync()
    {
        try
        {
            await using var connection = await OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                IF OBJECT_ID(N'dbo.DoctorPayments', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.DoctorPayments
                    (
                        PaymentId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DoctorPayments PRIMARY KEY,
                        DocId       INT            NOT NULL CONSTRAINT FK_DoctorPayments_Doctors REFERENCES dbo.Doctors (DocId),
                        PaymentDate DATE           NOT NULL,
                        Amount      DECIMAL(12, 2) NOT NULL CONSTRAINT CK_DoctorPayments_Amount CHECK (Amount > 0),
                        Mode        NVARCHAR(30)   NOT NULL,
                        Reference   NVARCHAR(100)  NULL,
                        Notes       NVARCHAR(250)  NULL,
                        IsVoided    BIT            NOT NULL CONSTRAINT DF_DoctorPayments_IsVoided DEFAULT (0),
                        VoidReason  NVARCHAR(250)  NULL,
                        VoidedAt    DATETIME2(0)   NULL,
                        VoidedBy    NVARCHAR(256)  NULL,
                        CreatedAt   DATETIME2(0)   NOT NULL CONSTRAINT DF_DoctorPayments_CreatedAt DEFAULT (SYSUTCDATETIME()),
                        CreatedBy   NVARCHAR(256)  NOT NULL
                    );
                    CREATE NONCLUSTERED INDEX IX_DoctorPayments_DocDate
                        ON dbo.DoctorPayments (DocId, PaymentDate) INCLUDE (Amount, IsVoided);
                END;

                IF OBJECT_ID(N'dbo.DoctorOpeningBalances', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.DoctorOpeningBalances
                    (
                        DocId      INT            NOT NULL CONSTRAINT PK_DoctorOpeningBalances PRIMARY KEY
                                                  CONSTRAINT FK_DoctorOpeningBalances_Doctors REFERENCES dbo.Doctors (DocId),
                        Amount     DECIMAL(12, 2) NOT NULL,
                        ModifiedAt DATETIME2(0)   NOT NULL CONSTRAINT DF_DoctorOpeningBalances_ModifiedAt DEFAULT (SYSUTCDATETIME()),
                        ModifiedBy NVARCHAR(256)  NULL
                    );
                END;

                IF OBJECT_ID(N'dbo.DoctorBills', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.DoctorBills
                    (
                        BillId           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DoctorBills PRIMARY KEY,
                        DocId            INT            NOT NULL CONSTRAINT FK_DoctorBills_Doctors REFERENCES dbo.Doctors (DocId),
                        PeriodFrom       DATE           NOT NULL,
                        PeriodTo         DATE           NOT NULL,
                        Subtotal         DECIMAL(12, 2) NOT NULL,
                        GstAmount        DECIMAL(12, 2) NOT NULL,
                        AdjustmentsTotal DECIMAL(12, 2) NOT NULL,
                        AdjustmentsText  NVARCHAR(1000) NULL,
                        OldDue           DECIMAL(12, 2) NOT NULL,
                        GrandTotal       DECIMAL(12, 2) NOT NULL,
                        SavedAt          DATETIME2(0)   NOT NULL CONSTRAINT DF_DoctorBills_SavedAt DEFAULT (SYSUTCDATETIME()),
                        SavedBy          NVARCHAR(256)  NOT NULL,
                        CONSTRAINT UQ_DoctorBills_DocPeriod UNIQUE (DocId, PeriodFrom, PeriodTo)
                    );
                END;
                """;
            await command.ExecuteNonQueryAsync();

            // First run: start tracking from the 1st of last month, so the bills
            // being collected right now can be settled here.
            if (await settings.GetEntryAsync(StartDateKey) is null)
            {
                var start = FirstOfMonth(Today).AddMonths(-1);
                await settings.SaveAsync(new Dictionary<string, string> { [StartDateKey] = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }, "system");
            }

            StartupError = null;
        }
        catch (Exception exception)
        {
            StartupError = "The payments tables could not be prepared. Check the database connection and restart the app.";
            logger.LogError(exception, "Doctor ledger tables could not be prepared.");
        }
    }

    /// <summary>Date from which dues are tracked. Older work is assumed settled (use opening balances for anything still pending).</summary>
    public async Task<DateOnly> GetStartDateAsync()
    {
        var text = await settings.GetAsync(StartDateKey, "");
        return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : FirstOfMonth(Today).AddMonths(-1);
    }

    public async Task SetStartDateAsync(DateOnly date, string by) =>
        await settings.SaveAsync(new Dictionary<string, string> { [StartDateKey] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }, by);

    public async Task<Dictionary<int, decimal>> GetOpeningBalancesAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<int, decimal>();
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DocId, Amount FROM dbo.DoctorOpeningBalances";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result[reader.GetInt32(0)] = reader.GetDecimal(1);
        }

        return result;
    }

    /// <summary>Saves opening balances (amount pending on the tracking start date). Zero removes the row.</summary>
    public async Task SaveOpeningBalancesAsync(IReadOnlyDictionary<int, decimal> balances, string by, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        foreach (var (docId, amount) in balances)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            if (amount == 0)
            {
                command.CommandText = "DELETE FROM dbo.DoctorOpeningBalances WHERE DocId = @doc";
                Add(command, "@doc", SqlDbType.Int, docId);
            }
            else
            {
                command.CommandText = """
                    MERGE dbo.DoctorOpeningBalances AS target
                    USING (SELECT @doc AS DocId) AS source ON target.DocId = source.DocId
                    WHEN MATCHED AND target.Amount <> @amount THEN
                        UPDATE SET Amount = @amount, ModifiedAt = SYSUTCDATETIME(), ModifiedBy = @by
                    WHEN NOT MATCHED THEN
                        INSERT (DocId, Amount, ModifiedAt, ModifiedBy) VALUES (@doc, @amount, SYSUTCDATETIME(), @by);
                    """;
                Add(command, "@doc", SqlDbType.Int, docId);
                AddMoney(command, "@amount", amount);
                Add(command, "@by", SqlDbType.NVarChar, by, 256);
            }

            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    // --------------------------------------------------------------- payments

    public async Task<int> AddPaymentAsync(NewPayment payment, string by, CancellationToken ct = default)
    {
        if (payment.Amount <= 0)
        {
            throw new ArgumentException("Amount must be more than zero.");
        }

        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.DoctorPayments (DocId, PaymentDate, Amount, Mode, Reference, Notes, CreatedAt, CreatedBy)
            OUTPUT INSERTED.PaymentId
            VALUES (@doc, @date, @amount, @mode, @ref, @notes, SYSUTCDATETIME(), @by);
            """;
        Add(command, "@doc", SqlDbType.Int, payment.DocId);
        AddDate(command, "@date", payment.PaymentDate);
        AddMoney(command, "@amount", Math.Round(payment.Amount, 2));
        Add(command, "@mode", SqlDbType.NVarChar, Trim(payment.Mode, 30) ?? "Cash", 30);
        Add(command, "@ref", SqlDbType.NVarChar, Trim(payment.Reference, 100), 100);
        Add(command, "@notes", SqlDbType.NVarChar, Trim(payment.Notes, 250), 250);
        Add(command, "@by", SqlDbType.NVarChar, by, 256);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
    }

    /// <summary>Payments are never deleted; a voided payment stays visible but no longer counts.</summary>
    public async Task<bool> VoidPaymentAsync(int paymentId, string reason, string by, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.DoctorPayments
            SET IsVoided = 1, VoidReason = @reason, VoidedAt = SYSUTCDATETIME(), VoidedBy = @by
            WHERE PaymentId = @id AND IsVoided = 0;
            """;
        Add(command, "@id", SqlDbType.Int, paymentId);
        Add(command, "@reason", SqlDbType.NVarChar, Trim(reason, 250) ?? "Voided", 250);
        Add(command, "@by", SqlDbType.NVarChar, by, 256);
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<List<DoctorPayment>> GetPaymentsAsync(DateOnly from, DateOnly to, int? docId = null, bool includeVoided = true, CancellationToken ct = default)
    {
        var list = new List<DoctorPayment>();
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.PaymentId, p.DocId, d.DocName, p.PaymentDate, p.Amount, p.Mode, p.Reference, p.Notes,
                   p.IsVoided, p.VoidReason, p.CreatedAt, p.CreatedBy, p.VoidedAt, p.VoidedBy
            FROM dbo.DoctorPayments AS p
            JOIN dbo.Doctors AS d ON d.DocId = p.DocId
            WHERE p.PaymentDate >= @from AND p.PaymentDate <= @to
              AND (@doc IS NULL OR p.DocId = @doc)
              AND (@voided = 1 OR p.IsVoided = 0)
            ORDER BY p.PaymentDate DESC, p.PaymentId DESC;
            """;
        AddDate(command, "@from", from);
        AddDate(command, "@to", to);
        Add(command, "@doc", SqlDbType.Int, docId);
        Add(command, "@voided", SqlDbType.Bit, includeVoided);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadPayment(reader));
        }

        return list;
    }

    // ------------------------------------------------------------------- dues

    /// <summary>Every doctor's position as of today (doctors with no activity at all are left out unless active).</summary>
    public async Task<List<DoctorDue>> GetDuesAsync(CancellationToken ct = default)
    {
        var start = await GetStartDateAsync();
        var monthStart = FirstOfMonth(Today);
        var list = new List<DoctorDue>();

        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.DocId, d.DocName, d.IsActive,
                   ISNULL(ob.Amount, 0)                          AS Opening,
                   ISNULL(w.BeforeMonth, 0) + ISNULL(x.BeforeMonth, 0) AS ChargesBefore,
                   ISNULL(w.ThisMonth, 0)   + ISNULL(x.ThisMonth, 0)   AS ChargesThisMonth,
                   ISNULL(p.Received, 0)                         AS Received,
                   p.LastDate
            FROM dbo.Doctors AS d
            LEFT JOIN dbo.DoctorOpeningBalances AS ob ON ob.DocId = d.DocId
            LEFT JOIN (
                SELECT DocId,
                       SUM(CASE WHEN CaseDate <  @month THEN Amount ELSE 0 END) AS BeforeMonth,
                       SUM(CASE WHEN CaseDate >= @month THEN Amount ELSE 0 END) AS ThisMonth
                FROM dbo.Records
                WHERE IsCancelled = 0 AND CaseDate >= @start
                GROUP BY DocId) AS w ON w.DocId = d.DocId
            LEFT JOIN (
                SELECT DocId,
                       SUM(CASE WHEN PeriodTo <  @month THEN GstAmount + AdjustmentsTotal ELSE 0 END) AS BeforeMonth,
                       SUM(CASE WHEN PeriodTo >= @month THEN GstAmount + AdjustmentsTotal ELSE 0 END) AS ThisMonth
                FROM dbo.DoctorBills
                WHERE PeriodFrom >= @start
                GROUP BY DocId) AS x ON x.DocId = d.DocId
            LEFT JOIN (
                SELECT DocId, SUM(Amount) AS Received, MAX(PaymentDate) AS LastDate
                FROM dbo.DoctorPayments
                WHERE IsVoided = 0 AND PaymentDate >= @start
                GROUP BY DocId) AS p ON p.DocId = d.DocId
            WHERE d.IsActive = 1 OR ob.DocId IS NOT NULL OR w.DocId IS NOT NULL OR x.DocId IS NOT NULL OR p.DocId IS NOT NULL
            ORDER BY d.DocName;
            """;
        AddDate(command, "@start", start);
        AddDate(command, "@month", monthStart);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new DoctorDue(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetBoolean(2),
                reader.GetDecimal(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5),
                reader.GetDecimal(6),
                reader.IsDBNull(7) ? null : DateOnly.FromDateTime(reader.GetDateTime(7))));
        }

        return list;
    }

    /// <summary>Old due for a bill covering <paramref name="from"/>.., counting payments up to <paramref name="paymentsUpTo"/>.</summary>
    public async Task<OldDueInfo> GetOldDueAsync(int docId, DateOnly from, DateOnly paymentsUpTo, CancellationToken ct = default)
    {
        var start = await GetStartDateAsync();
        if (paymentsUpTo < from)
        {
            paymentsUpTo = from;
        }

        if (from < start)
        {
            return new OldDueInfo(start, true, 0, 0, 0, 0, 0, from, paymentsUpTo);
        }

        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT ISNULL(SUM(Amount), 0) FROM dbo.DoctorOpeningBalances WHERE DocId = @doc),
                (SELECT ISNULL(SUM(Amount), 0) FROM dbo.Records
                  WHERE DocId = @doc AND IsCancelled = 0 AND CaseDate >= @start AND CaseDate < @from),
                (SELECT ISNULL(SUM(GstAmount + AdjustmentsTotal), 0) FROM dbo.DoctorBills
                  WHERE DocId = @doc AND PeriodFrom >= @start AND PeriodTo < @from),
                (SELECT ISNULL(SUM(Amount), 0) FROM dbo.DoctorPayments
                  WHERE DocId = @doc AND IsVoided = 0 AND PaymentDate >= @start AND PaymentDate < @from),
                (SELECT ISNULL(SUM(Amount), 0) FROM dbo.DoctorPayments
                  WHERE DocId = @doc AND IsVoided = 0 AND PaymentDate >= @from AND PaymentDate <= @upto),
                (SELECT COUNT(*) FROM dbo.DoctorPayments
                  WHERE DocId = @doc AND IsVoided = 0 AND PaymentDate >= @from AND PaymentDate <= @upto);
            """;
        Add(command, "@doc", SqlDbType.Int, docId);
        AddDate(command, "@start", start);
        AddDate(command, "@from", from);
        AddDate(command, "@upto", paymentsUpTo);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        return new OldDueInfo(
            start,
            false,
            reader.GetDecimal(0),
            reader.GetDecimal(1) + reader.GetDecimal(2),
            reader.GetDecimal(3),
            reader.GetDecimal(4),
            reader.GetInt32(5),
            from,
            paymentsUpTo);
    }

    /// <summary>Opening balance, monthly work, bill extras and payments in date order (running balance is worked out by the page).</summary>
    public async Task<List<LedgerEntry>> GetStatementAsync(int docId, CancellationToken ct = default)
    {
        var start = await GetStartDateAsync();
        var today = Today;
        var entries = new List<LedgerEntry>();

        await using var connection = await OpenAsync(ct);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT Amount FROM dbo.DoctorOpeningBalances WHERE DocId = @doc;

                SELECT YEAR(CaseDate), MONTH(CaseDate), SUM(Amount), COUNT(DISTINCT CaseId), SUM(Units)
                FROM dbo.Records
                WHERE DocId = @doc AND IsCancelled = 0 AND CaseDate >= @start
                GROUP BY YEAR(CaseDate), MONTH(CaseDate);

                SELECT PeriodFrom, PeriodTo, GstAmount, AdjustmentsTotal, AdjustmentsText
                FROM dbo.DoctorBills
                WHERE DocId = @doc AND PeriodFrom >= @start AND (GstAmount <> 0 OR AdjustmentsTotal <> 0);

                SELECT p.PaymentId, p.DocId, d.DocName, p.PaymentDate, p.Amount, p.Mode, p.Reference, p.Notes,
                       p.IsVoided, p.VoidReason, p.CreatedAt, p.CreatedBy, p.VoidedAt, p.VoidedBy
                FROM dbo.DoctorPayments AS p
                JOIN dbo.Doctors AS d ON d.DocId = p.DocId
                WHERE p.DocId = @doc AND p.PaymentDate >= @start;
                """;
            Add(command, "@doc", SqlDbType.Int, docId);
            AddDate(command, "@start", start);

            await using var reader = await command.ExecuteReaderAsync(ct);

            var opening = 0m;
            if (await reader.ReadAsync(ct))
            {
                opening = reader.GetDecimal(0);
            }

            entries.Add(new LedgerEntry(start, LedgerEntryKind.Opening, "Opening balance", opening, 0,
                Detail: $"Pending on {start:dd MMM yyyy}"));

            await reader.NextResultAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var first = new DateOnly(reader.GetInt32(0), reader.GetInt32(1), 1);
                var last = first.AddMonths(1).AddDays(-1);
                var cases = reader.GetInt32(3);
                var units = reader.GetInt32(4);
                entries.Add(new LedgerEntry(
                    last > today ? today : last,
                    LedgerEntryKind.Work,
                    $"Work in {first:MMMM yyyy}" + (last > today ? " (so far)" : ""),
                    reader.GetDecimal(2),
                    0,
                    Detail: $"{cases} case{(cases == 1 ? "" : "s")} · {units} unit{(units == 1 ? "" : "s")}"));
            }

            await reader.NextResultAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var from = DateOnly.FromDateTime(reader.GetDateTime(0));
                var to = DateOnly.FromDateTime(reader.GetDateTime(1));
                var gst = reader.GetDecimal(2);
                var adjustments = reader.GetDecimal(3);
                var parts = new List<string>();
                if (gst != 0) parts.Add("GST");
                if (!reader.IsDBNull(4) && !string.IsNullOrWhiteSpace(reader.GetString(4))) parts.Add(reader.GetString(4));
                else if (adjustments != 0) parts.Add("Adjustments");
                entries.Add(new LedgerEntry(
                    to,
                    LedgerEntryKind.BillExtras,
                    $"Bill {from:dd MMM} – {to:dd MMM yyyy}: GST & adjustments",
                    gst + adjustments,
                    0,
                    Detail: string.Join(" · ", parts)));
            }

            await reader.NextResultAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var p = ReadPayment(reader);
                var detail = string.Join(" · ", new[] { p.Mode, p.Reference, p.Notes }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (p.IsVoided)
                {
                    detail = $"Voided: {p.VoidReason}" + (detail.Length > 0 ? " · " + detail : "");
                }

                entries.Add(new LedgerEntry(p.PaymentDate, LedgerEntryKind.Payment, "Payment received", 0, p.Amount, p.PaymentId, p.IsVoided, detail));
            }
        }

        // Same date: charges first, then payments.
        return entries
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Kind == LedgerEntryKind.Opening ? 0 : e.Kind == LedgerEntryKind.Payment ? 2 : 1)
            .ThenBy(e => e.PaymentId ?? 0)
            .ToList();
    }

    // ------------------------------------------------------------ saved bills

    /// <summary>
    /// Saves (or replaces) the bill for this doctor and period, so its GST and adjustments carry forward.
    /// A period is billed once: saved bills for the same doctor that overlap this period are replaced.
    /// Returns the periods that were replaced.
    /// </summary>
    public async Task<List<(DateOnly From, DateOnly To)>> SaveBillAsync(SaveBillRequest bill, string by, CancellationToken ct = default)
    {
        var replaced = new List<(DateOnly From, DateOnly To)>();
        await using var connection = await OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = """
                DELETE FROM dbo.DoctorBills
                OUTPUT DELETED.PeriodFrom, DELETED.PeriodTo
                WHERE DocId = @doc AND PeriodFrom <= @to AND PeriodTo >= @from
                  AND NOT (PeriodFrom = @from AND PeriodTo = @to);
                """;
            Add(delete, "@doc", SqlDbType.Int, bill.DocId);
            AddDate(delete, "@from", bill.PeriodFrom);
            AddDate(delete, "@to", bill.PeriodTo);
            await using var reader = await delete.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                replaced.Add((DateOnly.FromDateTime(reader.GetDateTime(0)), DateOnly.FromDateTime(reader.GetDateTime(1))));
            }
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            MERGE dbo.DoctorBills AS target
            USING (SELECT @doc AS DocId, @from AS PeriodFrom, @to AS PeriodTo) AS source
               ON target.DocId = source.DocId AND target.PeriodFrom = source.PeriodFrom AND target.PeriodTo = source.PeriodTo
            WHEN MATCHED THEN
                UPDATE SET Subtotal = @subtotal, GstAmount = @gst, AdjustmentsTotal = @adj, AdjustmentsText = @adjText,
                           OldDue = @oldDue, GrandTotal = @grand, SavedAt = SYSUTCDATETIME(), SavedBy = @by
            WHEN NOT MATCHED THEN
                INSERT (DocId, PeriodFrom, PeriodTo, Subtotal, GstAmount, AdjustmentsTotal, AdjustmentsText, OldDue, GrandTotal, SavedAt, SavedBy)
                VALUES (@doc, @from, @to, @subtotal, @gst, @adj, @adjText, @oldDue, @grand, SYSUTCDATETIME(), @by);
            """;
        Add(command, "@doc", SqlDbType.Int, bill.DocId);
        AddDate(command, "@from", bill.PeriodFrom);
        AddDate(command, "@to", bill.PeriodTo);
        AddMoney(command, "@subtotal", bill.Subtotal);
        AddMoney(command, "@gst", bill.GstAmount);
        AddMoney(command, "@adj", bill.AdjustmentsTotal);
        Add(command, "@adjText", SqlDbType.NVarChar, Trim(bill.AdjustmentsText, 1000), 1000);
        AddMoney(command, "@oldDue", bill.OldDue);
        AddMoney(command, "@grand", bill.GrandTotal);
        Add(command, "@by", SqlDbType.NVarChar, by, 256);
        await command.ExecuteNonQueryAsync(ct);

        await transaction.CommitAsync(ct);
        return replaced;
    }

    public async Task<SavedBill?> GetSavedBillAsync(int docId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        (await QueryBillsAsync("WHERE DocId = @doc AND PeriodFrom = @from AND PeriodTo = @to", docId, from, to, ct)).FirstOrDefault();

    public async Task<List<SavedBill>> GetSavedBillsAsync(int docId, CancellationToken ct = default) =>
        await QueryBillsAsync("WHERE DocId = @doc", docId, null, null, ct);

    private async Task<List<SavedBill>> QueryBillsAsync(string where, int docId, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var list = new List<SavedBill>();
        await using var connection = await OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT BillId, DocId, PeriodFrom, PeriodTo, Subtotal, GstAmount, AdjustmentsTotal, AdjustmentsText,
                   OldDue, GrandTotal, SavedAt, SavedBy
            FROM dbo.DoctorBills
            {where}
            ORDER BY PeriodFrom DESC, PeriodTo DESC;
            """;
        Add(command, "@doc", SqlDbType.Int, docId);
        if (from is { } f) AddDate(command, "@from", f);
        if (to is { } t) AddDate(command, "@to", t);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new SavedBill(
                reader.GetInt32(0),
                reader.GetInt32(1),
                DateOnly.FromDateTime(reader.GetDateTime(2)),
                DateOnly.FromDateTime(reader.GetDateTime(3)),
                reader.GetDecimal(4),
                reader.GetDecimal(5),
                reader.GetDecimal(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetDecimal(8),
                reader.GetDecimal(9),
                DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc),
                reader.GetString(11)));
        }

        return list;
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>Same "today" the rest of the app uses for bills.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);

    private async Task<SqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static DoctorPayment ReadPayment(SqlDataReader r) => new(
        r.GetInt32(0),
        r.GetInt32(1),
        r.GetString(2),
        DateOnly.FromDateTime(r.GetDateTime(3)),
        r.GetDecimal(4),
        r.GetString(5),
        r.IsDBNull(6) ? null : r.GetString(6),
        r.IsDBNull(7) ? null : r.GetString(7),
        r.GetBoolean(8),
        r.IsDBNull(9) ? null : r.GetString(9),
        DateTime.SpecifyKind(r.GetDateTime(10), DateTimeKind.Utc),
        r.GetString(11),
        r.IsDBNull(12) ? null : DateTime.SpecifyKind(r.GetDateTime(12), DateTimeKind.Utc),
        r.IsDBNull(13) ? null : r.GetString(13));

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    private static void Add(SqlCommand command, string name, SqlDbType type, object? value, int size = 0)
    {
        var parameter = command.Parameters.Add(name, type);
        if (size > 0)
        {
            parameter.Size = size;
        }

        parameter.Value = value ?? DBNull.Value;
    }

    private static void AddDate(SqlCommand command, string name, DateOnly value) =>
        Add(command, name, SqlDbType.Date, value.ToDateTime(TimeOnly.MinValue));

    private static void AddMoney(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 12;
        parameter.Scale = 2;
        parameter.Value = Math.Round(value, 2);
    }
}
