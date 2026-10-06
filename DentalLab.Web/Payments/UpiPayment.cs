using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using QRCoder;

namespace DentalLab.Web.Payments;

/// <summary>What a doctor is asked to pay: who to, how much, and a short note.</summary>
public sealed record UpiPayRequest(string UpiId, string PayeeName, decimal Amount, string Note);

/// <summary>
/// Builds standard UPI payment links (upi://pay?...) and QR codes.
/// Any UPI app (PhonePe, Google Pay, Paytm, BHIM...) opens them with the
/// payee, amount and note already filled in; the payer only enters their PIN.
/// </summary>
public static partial class UpiPayment
{
    public const string DefaultUpiId = "9966684787@ybl";
    public const string DefaultPayeeName = "Natural Dental Labs";

    /// <summary>Most apps cut the note around 50 characters.</summary>
    public const int MaxNoteLength = 50;

    [GeneratedRegex(@"^[a-zA-Z0-9.\-_]{2,256}@[a-zA-Z][a-zA-Z0-9.\-]{1,64}$")]
    private static partial Regex UpiIdRegex();

    public static bool IsValidUpiId(string? upiId) =>
        !string.IsNullOrWhiteSpace(upiId) && UpiIdRegex().IsMatch(upiId.Trim());

    /// <summary>Keeps the note short and plain so every app shows it correctly.</summary>
    public static string CleanNote(string? note)
    {
        var text = new string((note ?? "").Where(c => c < 128 && !char.IsControl(c)).ToArray());
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length <= MaxNoteLength ? text : text[..MaxNoteLength].TrimEnd();
    }

    /// <summary>upi://pay?pa=...&amp;pn=...&amp;am=...&amp;cu=INR&amp;tn=...</summary>
    public static string BuildLink(UpiPayRequest r, string scheme = "upi://pay")
    {
        var query = new StringBuilder();
        query.Append("pa=").Append(Escape(r.UpiId.Trim()).Replace("%40", "@"));
        query.Append("&pn=").Append(Escape(r.PayeeName.Trim()));
        query.Append("&am=").Append(r.Amount.ToString("0.00", CultureInfo.InvariantCulture));
        query.Append("&cu=INR");
        var note = CleanNote(r.Note);
        if (note.Length > 0)
        {
            query.Append("&tn=").Append(Escape(note));
        }

        return scheme + "?" + query;
    }

    /// <summary>
    /// App-specific versions of the same link. iPhones have no "choose an app"
    /// screen for upi:// links, so these open a particular app directly.
    /// </summary>
    public static IReadOnlyList<(string App, string Link)> AppLinks(UpiPayRequest r) =>
    [
        ("PhonePe", BuildLink(r, "phonepe://pay")),
        ("Google Pay", BuildLink(r, "tez://upi/pay")),
        ("Paytm", BuildLink(r, "paytmmp://pay"))
    ];

    /// <summary>PNG QR code of the UPI link (scan with any UPI app).</summary>
    public static byte[] QrPng(string text, int pixelsPerModule = 10)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        using var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }

    public static string QrDataUri(string text) =>
        "data:image/png;base64," + Convert.ToBase64String(QrPng(text, 8));

    public static string AmountLabel(decimal amount) =>
        "\u20b9" + amount.ToString("N2", CultureInfo.GetCultureInfo("en-IN"));

    private static string Escape(string value) => Uri.EscapeDataString(value);
}

/// <summary>
/// Signed links to the public /pay page. The pay details are sealed inside the
/// link with ASP.NET Core data protection, so nobody can make a fake
/// "Natural Dental Labs" pay page on this website with another UPI ID or amount.
/// </summary>
public sealed class PayLinkService(IDataProtectionProvider provider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

    private readonly ITimeLimitedDataProtector protector =
        provider.CreateProtector("DentalLab.Web.Payments.PayLink.v1").ToTimeLimitedDataProtector();

    public string CreateToken(UpiPayRequest request) =>
        protector.Protect(JsonSerializer.Serialize(request), Lifetime);

    /// <summary>Returns the request, or null when the link is broken, edited or expired.</summary>
    public UpiPayRequest? ReadToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var json = protector.Unprotect(token);
            var request = JsonSerializer.Deserialize<UpiPayRequest>(json);
            return request is not null && UpiPayment.IsValidUpiId(request.UpiId) && request.Amount > 0
                ? request
                : null;
        }
        catch
        {
            return null;
        }
    }
}
