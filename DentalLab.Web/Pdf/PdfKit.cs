using System.Globalization;
using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace DentalLab.Web.Pdf;

/// <summary>
/// One-time QuestPDF setup plus the shared look (fonts, colours, logo) used by
/// every PDF the app produces. Call <see cref="Initialize"/> once at startup.
/// </summary>
public static class PdfKit
{
    // Same typefaces as the web app (screen and print), embedded from Pdf/Fonts.
    public const string Font = "Inter";
    public const string HeadingFont = "Plus Jakarta Sans";
    public const string SignatureFont = "Caveat";

    // Brand colours (same as the web app)
    public const string Teal = "#0F766E";
    public const string TealDark = "#0B5852";
    public const string TealTint = "#E6F6F4";
    public const string Gold = "#B8873A";
    public const string GoldTint = "#FAF1DE";
    public const string Ink = "#1D2A28";
    public const string Muted = "#6B7C78";
    public const string Line = "#DCE6E3";
    public const string LineSoft = "#EDF2F1";
    public const string Danger = "#C0362C";
    public const string Success = "#1A8A5E";

    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");
    private static readonly object InitLock = new();
    private static volatile bool initialized;

    public static byte[] Logo { get; private set; } = [];
    public static byte[] Watermark { get; private set; } = [];

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (initialized)
            {
                return;
            }

            Setup();
            initialized = true;
        }
    }

    private static void Setup()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        // A rare character missing from the font (e.g. in a patient name) shows
        // as a blank instead of stopping the whole PDF.
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;

        var assembly = typeof(PdfKit).Assembly;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                FontManager.RegisterFont(stream);
            }
        }

        Logo = ReadResource(assembly, "logo.png");
        Watermark = ReadResource(assembly, "watermark.png");
    }

    private static byte[] ReadResource(Assembly assembly, string fileName)
    {
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));
        if (name is null)
        {
            return [];
        }

        using var stream = assembly.GetManifestResourceStream(name)!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    // ------------------------------------------------------------------ formatting

    public static string Money(decimal value) => "\u20b9" + value.ToString("N2", India);

    public static string Money0(decimal value) => "\u20b9" + value.ToString("N0", India);

    public static string Number(decimal value) => value.ToString("N0", India);

    /// <summary>"− ₹500.00" for negatives, "+ ₹500.00" for positives.</summary>
    public static string Signed(decimal value) =>
        value < 0 ? "\u2212 " + Money(Math.Abs(value)) : "+ " + Money(value);

    /// <summary>Safe file name: letters, digits, '-' and '_' only.</summary>
    public static string FileName(string raw)
    {
        var cleaned = new string(raw.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "Document" : cleaned;
    }

    /// <summary>Indian-system words for a rupee amount, e.g. "One Lakh Twenty Thousand".</summary>
    public static string AmountInWords(decimal number)
    {
        var amount = (long)Math.Floor(Math.Abs(number));
        if (amount == 0)
        {
            return "Zero";
        }

        var words = "";
        void Part(long divisor, string label)
        {
            if (amount / divisor > 0)
            {
                words += Small(amount / divisor) + " " + label + " ";
                amount %= divisor;
            }
        }

        Part(10000000, "Crore");
        Part(100000, "Lakh");
        Part(1000, "Thousand");
        Part(100, "Hundred");

        if (amount > 0)
        {
            if (words != "") words += "and ";
            words += Small(amount);
        }

        return words.Trim();
    }

    private static string Small(long number)
    {
        string[] units = ["Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
            "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"];
        string[] tens = ["Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"];

        if (number >= 100)
        {
            // Crore part can exceed 99 (e.g. 150 crore)
            return (Small(number / 100) + " Hundred" + (number % 100 > 0 ? " " + Small(number % 100) : "")).Trim();
        }

        if (number < 20) return units[number];
        var result = tens[number / 10];
        if (number % 10 > 0) result += "-" + units[number % 10];
        return result;
    }
}
