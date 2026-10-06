using DentalLab.Web.Ledger;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DentalLab.Web.Pdf;

/// <summary>One statement line with its running balance.</summary>
public sealed record StatementLine(LedgerEntry Entry, decimal Balance);

/// <summary>PDFs for the Doctor Payments page: doctor statement, dues report and payments list.</summary>
public static class LedgerPdf
{
    // ------------------------------------------------------------------ statement

    public static byte[] Statement(string doctorName, IReadOnlyList<StatementLine> lines, DoctorDue? due, DateOnly trackingStart, DateTime generatedAt)
    {
        PdfKit.Initialize();
        var balance = lines.Count > 0 ? lines[^1].Balance : 0m;

        return Build($"Statement - {doctorName}", "Doctor Statement", doctorName,
            $"From {trackingStart:dd MMM yyyy} to {generatedAt:dd MMM yyyy}", generatedAt, col =>
            {
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                    Tile(t.Cell(), "Pending from bills", Signed(due?.PendingFromBills ?? balance), "#FBEAE8", PdfKit.Danger);
                    Tile(t.Cell(), "This month so far", PdfKit.Money(due?.ChargesThisMonth ?? 0), PdfKit.TealTint, PdfKit.Ink);
                    Tile(t.Cell(), "Balance now", Signed(balance), PdfKit.Teal, "#FFFFFF");
                });

                col.Item().PaddingTop(10).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(62);
                        c.RelativeColumn();
                        c.ConstantColumn(72);
                        c.ConstantColumn(72);
                        c.ConstantColumn(78);
                    });
                    t.Header(h =>
                    {
                        Head(h.Cell(), "Date");
                        Head(h.Cell(), "Particulars");
                        Head(h.Cell(), "Charges", true);
                        Head(h.Cell(), "Received", true);
                        Head(h.Cell(), "Balance", true);
                    });

                    foreach (var line in lines)
                    {
                        var e = line.Entry;
                        var faded = e.IsVoided ? PdfKit.Muted : PdfKit.Ink;
                        Body(t.Cell()).Text(e.Date.ToString("dd MMM yy")).FontColor(faded);
                        Body(t.Cell()).Column(pc =>
                        {
                            var title = pc.Item().Text(e.Description + (e.IsVoided ? " (void)" : "")).SemiBold().FontColor(faded);
                            if (e.IsVoided) title.Strikethrough();
                            if (!string.IsNullOrWhiteSpace(e.Detail))
                            {
                                pc.Item().Text(e.Detail).FontSize(7.5f).FontColor(PdfKit.Muted);
                            }
                        });
                        Body(t.Cell()).AlignRight().Text(e.Charge != 0 ? Signed(e.Charge) : "").FontColor(faded);
                        var paid = Body(t.Cell()).AlignRight().Text(e.Payment != 0 ? PdfKit.Money(e.Payment) : "").FontColor(e.IsVoided ? PdfKit.Muted : PdfKit.Success);
                        if (e.IsVoided) paid.Strikethrough();
                        Body(t.Cell()).AlignRight().Text(Signed(line.Balance)).SemiBold();
                    }

                    Foot(t.Cell().ColumnSpan(4)).Text("Balance now").Bold();
                    Foot(t.Cell()).AlignRight().Text(Signed(balance)).Bold();
                });

                col.Item().PaddingTop(6).Text("Voided payments are shown struck through and are not counted. A minus balance is an advance in the doctor's favour.")
                    .FontSize(7.5f).FontColor(PdfKit.Muted);
            });
    }

    // ------------------------------------------------------------------ dues report

    public static byte[] DuesReport(IReadOnlyList<DoctorDue> dues, DateOnly trackingStart, DateTime generatedAt)
    {
        PdfKit.Initialize();
        var withDues = dues.Where(d => Math.Abs(d.PendingFromBills) > 0.004m || Math.Abs(d.TotalBalance) > 0.004m)
            .OrderByDescending(d => d.PendingFromBills).ThenBy(d => d.DoctorName).ToList();
        var pending = withDues.Where(d => d.PendingFromBills > 0).Sum(d => d.PendingFromBills);

        return Build("Doctor dues report", "Doctor Dues Report", $"As of {generatedAt:dd MMM yyyy}",
            $"Dues tracked from {trackingStart:dd MMM yyyy}", generatedAt, col =>
            {
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                    Tile(t.Cell(), "Pending from bills", PdfKit.Money(pending), "#FBEAE8", PdfKit.Danger);
                    Tile(t.Cell(), "Work this month (not billed)", PdfKit.Money(withDues.Sum(d => d.ChargesThisMonth)), PdfKit.TealTint, PdfKit.Ink);
                    Tile(t.Cell(), "Total outstanding", Signed(withDues.Sum(d => d.TotalBalance)), PdfKit.Teal, "#FFFFFF");
                });

                col.Item().PaddingTop(10).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(22);
                        c.RelativeColumn();
                        c.ConstantColumn(80);
                        c.ConstantColumn(80);
                        c.ConstantColumn(80);
                        c.ConstantColumn(66);
                    });
                    t.Header(h =>
                    {
                        Head(h.Cell(), "#", true);
                        Head(h.Cell(), "Doctor");
                        Head(h.Cell(), "Pending from bills", true);
                        Head(h.Cell(), "This month", true);
                        Head(h.Cell(), "Total balance", true);
                        Head(h.Cell(), "Last payment", true);
                    });

                    for (var i = 0; i < withDues.Count; i++)
                    {
                        var d = withDues[i];
                        Body(t.Cell()).AlignRight().Text((i + 1).ToString());
                        Body(t.Cell()).Text(d.DoctorName + (d.IsActive ? "" : " (inactive)")).SemiBold();
                        Body(t.Cell()).AlignRight().Text(Signed(d.PendingFromBills)).FontColor(d.PendingFromBills > 0.004m ? PdfKit.Danger : PdfKit.Success);
                        Body(t.Cell()).AlignRight().Text(PdfKit.Money(d.ChargesThisMonth));
                        Body(t.Cell()).AlignRight().Text(Signed(d.TotalBalance)).SemiBold();
                        Body(t.Cell()).AlignRight().Text(d.LastPaymentDate?.ToString("dd MMM yy") ?? "-").FontColor(PdfKit.Muted);
                    }

                    Foot(t.Cell().ColumnSpan(2)).Text($"Total ({withDues.Count} doctors)").Bold();
                    Foot(t.Cell()).AlignRight().Text(Signed(withDues.Sum(d => d.PendingFromBills))).Bold();
                    Foot(t.Cell()).AlignRight().Text(PdfKit.Money(withDues.Sum(d => d.ChargesThisMonth))).Bold();
                    Foot(t.Cell()).AlignRight().Text(Signed(withDues.Sum(d => d.TotalBalance))).Bold();
                    Foot(t.Cell());
                });

                col.Item().PaddingTop(6).Text("Pending from bills = earlier months after all payments; it is added to each doctor's next bill as Old due. A minus amount is an advance.")
                    .FontSize(7.5f).FontColor(PdfKit.Muted);
            });
    }

    // ------------------------------------------------------------------ payments list

    public static byte[] Payments(IReadOnlyList<DoctorPayment> payments, DateOnly month, DateTime generatedAt)
    {
        PdfKit.Initialize();
        var counted = payments.Where(p => !p.IsVoided).ToList();

        return Build($"Payments received {month:MMM yyyy}", "Payments Received", month.ToString("MMMM yyyy"),
            $"{counted.Count} payments · {PdfKit.Money(counted.Sum(p => p.Amount))}", generatedAt, col =>
            {
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                    Tile(t.Cell(), "Total received", PdfKit.Money(counted.Sum(p => p.Amount)), "#E7F6EE", PdfKit.Success);
                    Tile(t.Cell(), "Payments", counted.Count.ToString(), PdfKit.TealTint, PdfKit.Ink);
                    Tile(t.Cell(), "Doctors who paid", counted.Select(p => p.DocId).Distinct().Count().ToString(), PdfKit.TealTint, PdfKit.Ink);
                });

                col.Item().PaddingTop(10).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(58);
                        c.RelativeColumn(1.4f);
                        c.ConstantColumn(62);
                        c.RelativeColumn();
                        c.ConstantColumn(74);
                    });
                    t.Header(h =>
                    {
                        Head(h.Cell(), "Date");
                        Head(h.Cell(), "Doctor");
                        Head(h.Cell(), "Paid by");
                        Head(h.Cell(), "Reference / note");
                        Head(h.Cell(), "Amount", true);
                    });

                    foreach (var p in payments)
                    {
                        var faded = p.IsVoided ? PdfKit.Muted : PdfKit.Ink;
                        Body(t.Cell()).Text(p.PaymentDate.ToString("dd MMM yy")).FontColor(faded);
                        Body(t.Cell()).Text(p.DoctorName).SemiBold().FontColor(faded);
                        Body(t.Cell()).Text(p.Mode).FontColor(faded);
                        var note = string.Join(" · ", new[] { p.Reference, p.Notes }.Where(x => !string.IsNullOrWhiteSpace(x)));
                        if (p.IsVoided) note = $"VOID: {p.VoidReason}" + (note.Length > 0 ? " · " + note : "");
                        Body(t.Cell()).Text(note).FontSize(8).FontColor(p.IsVoided ? PdfKit.Danger : PdfKit.Muted);
                        var amount = Body(t.Cell()).AlignRight().Text(PdfKit.Money(p.Amount)).SemiBold().FontColor(p.IsVoided ? PdfKit.Muted : PdfKit.Success);
                        if (p.IsVoided) amount.Strikethrough();
                    }

                    Foot(t.Cell().ColumnSpan(4)).Text("Total received (voided payments not counted)").Bold();
                    Foot(t.Cell()).AlignRight().Text(PdfKit.Money(counted.Sum(p => p.Amount))).Bold();
                });
            });
    }

    // ------------------------------------------------------------------ shared layout

    private static byte[] Build(string title, string heading, string subject, string subline, DateTime generatedAt, Action<ColumnDescriptor> content)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(12, Unit.Millimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontFamily(PdfKit.Font).FontSize(9).FontColor(PdfKit.Ink));

                page.Header().BorderBottom(2).BorderColor(PdfKit.Teal).PaddingBottom(8).Row(row =>
                {
                    row.RelativeItem().Row(brand =>
                    {
                        if (PdfKit.Logo.Length > 0)
                        {
                            brand.ConstantItem(44).Image(PdfKit.Logo);
                            brand.ConstantItem(10);
                        }

                        brand.RelativeItem().AlignMiddle().Column(c =>
                        {
                            c.Item().Text("Natural Dental Labs").FontSize(9.5f).SemiBold().FontColor(PdfKit.Teal);
                            c.Item().Text(heading).FontFamily(PdfKit.HeadingFont).FontSize(16).Bold();
                            c.Item().Text(subline).FontColor(PdfKit.Muted);
                        });
                    });

                    row.ConstantItem(190).AlignMiddle().Column(c =>
                    {
                        c.Item().AlignRight().Text(subject).FontFamily(PdfKit.HeadingFont).FontSize(11).Bold();
                        c.Item().AlignRight().Text($"Generated {generatedAt:dd MMM yyyy, hh:mm tt}").FontSize(8).FontColor(PdfKit.Muted);
                    });
                });

                page.Content().PaddingTop(10).Column(content);

                page.Footer().PaddingTop(6).BorderTop(0.5f).BorderColor(PdfKit.Line).PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text("Natural Dental Labs · " + heading).FontSize(7.5f).FontColor(PdfKit.Muted);
                    r.AutoItem().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(PdfKit.Muted));
                        t.Span("Page ");
                        t.CurrentPageNumber();
                        t.Span(" of ");
                        t.TotalPages();
                    });
                });
            });
        })
        .WithMetadata(new DocumentMetadata { Title = title, Author = "Natural Dental Labs", Creator = "Natural Dental Labs" })
        .GeneratePdf();
    }

    private static void Tile(IContainer cell, string label, string value, string back, string fore)
    {
        cell.Padding(3).Background(back).CornerRadius(8).Padding(9).Column(c =>
        {
            c.Item().Text(label).FontSize(8).SemiBold().FontColor(fore == "#FFFFFF" ? "#C7EFE8" : PdfKit.Muted);
            c.Item().Text(value).FontFamily(PdfKit.HeadingFont).FontSize(14).Bold().FontColor(fore);
        });
    }

    private static void Head(IContainer cell, string text, bool right = false)
    {
        var c = cell.Background(PdfKit.TealTint).BorderBottom(0.8f).BorderColor("#CFE3DF").PaddingVertical(4).PaddingHorizontal(4);
        (right ? c.AlignRight() : c).Text(text).FontSize(7.5f).SemiBold().FontColor(PdfKit.TealDark);
    }

    private static IContainer Body(IContainer cell) =>
        cell.BorderBottom(0.5f).BorderColor(PdfKit.LineSoft).PaddingVertical(3.5f).PaddingHorizontal(4).AlignMiddle();

    private static IContainer Foot(IContainer cell) =>
        cell.Background(PdfKit.GoldTint).BorderTop(1.2f).BorderColor(PdfKit.Teal).PaddingVertical(4).PaddingHorizontal(4).AlignMiddle();

    private static string Signed(decimal value) => value < 0 ? "−" + PdfKit.Money(-value) : PdfKit.Money(value);
}
