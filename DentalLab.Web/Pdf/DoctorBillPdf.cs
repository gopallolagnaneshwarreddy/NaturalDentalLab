using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DentalLab.Web.Pdf;

/// <summary>Everything the Doctor Bill PDF shows (already calculated by the bill page).</summary>
public sealed class DoctorBillPdfModel
{
    public string DoctorName { get; init; } = "-";
    public string PeriodLabel { get; init; } = "";
    public DateTime GeneratedAt { get; init; } = DateTime.Now;
    public string? Gstin { get; init; }

    public bool ShowCaseId { get; init; } = true;
    public bool ShowPatient { get; init; } = true;
    public bool ShowWatermark { get; init; } = true;
    public bool ShowTypeSummary { get; init; } = true;

    public bool ApplyGst { get; init; }
    public decimal GstPercentage { get; init; }
    public decimal GstAmount { get; init; }
    public decimal Subtotal { get; init; }
    public decimal OldDue { get; init; }

    /// <summary>When set, "Previous balance" and "Payments received" are printed instead of one "Old due" line.</summary>
    public BillDueBreakdown? DueBreakdown { get; init; }
    public decimal GrandTotal { get; init; }

    /// <summary>Signed: negative is subtracted ("Paid by cash" -500), positive is added.</summary>
    public List<BillAdjustment> Adjustments { get; init; } = [];

    /// <summary>"Please pay your bills to 9966684787 (PhonePe)", or null to hide.</summary>
    public string? PaymentCaption { get; init; }

    /// <summary>Optional note printed at the end of the bill.</summary>
    public string? Note { get; init; }

    /// <summary>UPI QR code and pay link, or null to leave them out.</summary>
    public BillUpi? Upi { get; init; }

    public List<BillCase> Cases { get; init; } = [];
    public List<BillTypeSummary> TypeSummary { get; init; } = [];
}

public sealed record BillCase(int CaseId, DateOnly Date, string? Patient, string? PatientRef, decimal Total, List<BillLine> Lines);

public sealed record BillLine(string Code, char Scope, char? Arch, string? UR, string? UL, string? LR, string? LL, int Units, decimal UnitRate);

public sealed record BillTypeSummary(string Type, int Units, decimal Amount);

public sealed record BillAdjustment(string Description, decimal Amount);

/// <summary>Old due split into what was pending before the bill period and what has been paid since.</summary>
public sealed record BillDueBreakdown(decimal PreviousBalance, decimal Received);

/// <param name="PayPageUrl">https link to the public Pay page (opens the UPI app on a phone).</param>
/// <param name="QrPng">QR code of the upi:// link.</param>
public sealed record BillUpi(string UpiId, string PayeeName, string Note, string PayPageUrl, byte[] QrPng);

public static class DoctorBillPdf
{
    public static byte[] Generate(DoctorBillPdfModel m)
    {
        PdfKit.Initialize();

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(12, Unit.Millimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontFamily(PdfKit.Font).FontSize(9).FontColor(PdfKit.Ink));

                if (m.ShowWatermark && PdfKit.Watermark.Length > 0)
                {
                    page.Background().AlignCenter().AlignMiddle().Width(320).Image(PdfKit.Watermark);
                }

                page.Header().Element(c => Letterhead(c, m));
                page.Content().PaddingTop(10).Element(c => Body(c, m));
                page.Footer().Element(Footer);   // thank-you + signature on every page
            });
        })
        .WithMetadata(new DocumentMetadata
        {
            Title = $"Doctor bill - {m.DoctorName} - {m.PeriodLabel}",
            Author = "Natural Dental Labs",
            Creator = "Natural Dental Labs"
        })
        .GeneratePdf();
    }

    // ------------------------------------------------------------------ header

    private static void Letterhead(IContainer container, DoctorBillPdfModel m)
    {
        container.BorderBottom(2).BorderColor(PdfKit.Teal).PaddingBottom(8).Row(row =>
        {
            row.RelativeItem().Row(brand =>
            {
                if (PdfKit.Logo.Length > 0)
                {
                    brand.ConstantItem(46).Image(PdfKit.Logo);
                    brand.ConstantItem(10);
                }

                brand.RelativeItem().AlignMiddle().Column(col =>
                {
                    col.Item().Text("Natural Dental Labs").FontFamily(PdfKit.HeadingFont).FontSize(16).Bold().FontColor(PdfKit.TealDark);
                    col.Item().Text("Doctor bill").FontSize(10).FontColor(PdfKit.Muted);
                });
            });

            row.ConstantItem(230).AlignMiddle().Column(col =>
            {
                col.Spacing(1);
                if (!string.IsNullOrWhiteSpace(m.Gstin))
                {
                    MetaLine(col, "GSTIN", m.Gstin!);
                }

                MetaLine(col, "Bill for", m.DoctorName);
                MetaLine(col, "Period", m.PeriodLabel);
                MetaLine(col, "Generated", m.GeneratedAt.ToString("dd MMM yyyy"));
            });
        });
    }

    private static void MetaLine(ColumnDescriptor col, string label, string value)
    {
        col.Item().AlignRight().Text(t =>
        {
            t.Span(label + ":  ").FontColor(PdfKit.Muted);
            t.Span(value).SemiBold();
        });
    }

    // ------------------------------------------------------------------ body

    private static void Body(IContainer container, DoctorBillPdfModel m)
    {
        container.Column(col =>
        {
            col.Spacing(12);

            if (m.Cases.Sum(c => c.Lines.Count) == 0)
            {
                col.Item().PaddingVertical(30).AlignCenter()
                    .Text("No active cases were found for this doctor in the selected period.")
                    .FontColor(PdfKit.Muted);
                return;
            }

            col.Item().Element(c => CasesTable(c, m));
            // EnsureSpace: start on the next page when little room is left,
            // but still allow splitting if a section is ever taller than a page.
            col.Item().EnsureSpace(140).Element(c => Totals(c, m));
            col.Item().EnsureSpace(90).Element(c => Closing(c, m));
        });
    }

    private static void CasesTable(IContainer container, DoctorBillPdfModel m)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.ConstantColumn(28);                       // S.No.
                if (m.ShowCaseId) cols.ConstantColumn(40);     // Case ID
                cols.ConstantColumn(52);                       // Date
                if (m.ShowPatient) cols.RelativeColumn(1.1f);  // Patient
                cols.RelativeColumn(1.4f);                     // Teeth / Arch
                cols.ConstantColumn(50);                       // Type
                cols.ConstantColumn(32);                       // Units
                cols.ConstantColumn(56);                       // Unit amount
                cols.ConstantColumn(64);                       // Total
            });

            table.Header(h =>
            {
                HeadCell(h.Cell(), "S.No.");
                if (m.ShowCaseId) HeadCell(h.Cell(), "Case ID");
                HeadCell(h.Cell(), "Date");
                if (m.ShowPatient) HeadCell(h.Cell(), "Patient");
                HeadCell(h.Cell(), "Teeth / Arch");
                HeadCell(h.Cell(), "Type");
                HeadCell(h.Cell(), "Units", right: true);
                HeadCell(h.Cell(), "Unit amount", right: true);
                HeadCell(h.Cell(), "Total amount", right: true);
            });

            var serial = 1;
            foreach (var item in m.Cases)
            {
                var span = (uint)Math.Max(item.Lines.Count, 1);
                for (var i = 0; i < item.Lines.Count; i++)
                {
                    var line = item.Lines[i];

                    if (i == 0)
                    {
                        Cell(table.Cell().RowSpan(span)).Text((serial++).ToString());
                        if (m.ShowCaseId) Cell(table.Cell().RowSpan(span)).Text("#" + item.CaseId).FontColor(PdfKit.TealDark).SemiBold();
                        Cell(table.Cell().RowSpan(span)).Text(item.Date.ToString("dd/MM/yyyy"));
                        if (m.ShowPatient)
                        {
                            Cell(table.Cell().RowSpan(span)).Column(c =>
                            {
                                c.Item().Text(string.IsNullOrWhiteSpace(item.Patient) ? "-" : item.Patient);
                                if (!string.IsNullOrWhiteSpace(item.PatientRef))
                                {
                                    c.Item().Text(item.PatientRef).FontSize(7.5f).FontColor(PdfKit.Muted);
                                }
                            });
                        }
                    }

                    Cell(table.Cell()).Element(c => Teeth(c, line));
                    Cell(table.Cell()).Text(line.Code).SemiBold().FontColor(PdfKit.TealDark);
                    Cell(table.Cell()).AlignRight().Text(line.Units.ToString());
                    Cell(table.Cell()).AlignRight().Text(line.UnitRate.ToString("N2"));

                    if (i == 0)
                    {
                        Cell(table.Cell().RowSpan(span)).AlignRight().Text(item.Total.ToString("N2")).SemiBold();
                    }
                }
            }
        });
    }

    /// <summary>Small dental cross: UR | UL over LR | LL (dentist's view), or the arch name.</summary>
    private static void Teeth(IContainer container, BillLine line)
    {
        if (line.Scope == 'A')
        {
            container.AlignMiddle().Text(line.Arch == 'U' ? "UPPER ARCH" : "LOWER ARCH").FontSize(8).SemiBold();
            return;
        }

        container.Table(t =>
        {
            t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });

            void Q(string? value, bool left, bool top)
            {
                IContainer cell = t.Cell();
                if (left) cell = cell.BorderRight(0.8f).BorderColor(PdfKit.Ink);
                if (top) cell = cell.BorderBottom(0.8f).BorderColor(PdfKit.Ink);
                var text = cell.PaddingHorizontal(2).PaddingVertical(1);
                (left ? text.AlignRight() : text.AlignLeft())
                    .Text(string.IsNullOrWhiteSpace(value) ? " " : value).FontSize(7.5f).SemiBold();
            }

            Q(line.UR, left: true, top: true);
            Q(line.UL, left: false, top: true);
            Q(line.LR, left: true, top: false);
            Q(line.LL, left: false, top: false);
        });
    }

    private static void HeadCell(IContainer cell, string text, bool right = false)
    {
        var c = cell.Background(PdfKit.TealTint).Border(0.5f).BorderColor(PdfKit.Line).PaddingVertical(4).PaddingHorizontal(4);
        (right ? c.AlignRight() : c).Text(text).FontSize(8).SemiBold().FontColor(PdfKit.TealDark);
    }

    private static IContainer Cell(IContainer cell) =>
        cell.Border(0.5f).BorderColor(PdfKit.Line).PaddingVertical(3).PaddingHorizontal(4).AlignMiddle();

    // ------------------------------------------------------------------ totals

    private static void Totals(IContainer container, DoctorBillPdfModel m)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(left =>
            {
                if (!m.ShowTypeSummary || m.TypeSummary.Count == 0)
                {
                    return;
                }

                left.Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(1.2f); c.RelativeColumn(); c.RelativeColumn(1.2f); });

                    t.Header(h =>
                    {
                        h.Cell().ColumnSpan(3).Background(PdfKit.TealTint).Border(0.5f).BorderColor(PdfKit.Line)
                            .Padding(4).AlignCenter().Text("Case type summary").SemiBold().FontColor(PdfKit.TealDark);
                        HeadCell(h.Cell(), "Type");
                        HeadCell(h.Cell(), "Total units", right: true);
                        HeadCell(h.Cell(), "Total amount", right: true);
                    });

                    foreach (var s in m.TypeSummary)
                    {
                        Cell(t.Cell()).Text(s.Type).SemiBold().FontColor(PdfKit.TealDark);
                        Cell(t.Cell()).AlignRight().Text(s.Units.ToString());
                        Cell(t.Cell()).AlignRight().Text(PdfKit.Money(s.Amount));
                    }

                    Cell(t.Cell().Background(PdfKit.GoldTint)).Text("Total").Bold();
                    Cell(t.Cell().Background(PdfKit.GoldTint)).AlignRight().Text(m.TypeSummary.Sum(x => x.Units).ToString()).Bold();
                    Cell(t.Cell().Background(PdfKit.GoldTint)).AlignRight().Text(PdfKit.Money(m.TypeSummary.Sum(x => x.Amount))).Bold();
                });
            });

            row.ConstantItem(18);

            row.ConstantItem(220).Column(col =>
            {
                SummaryRow(col, "Current period total", PdfKit.Money(m.Subtotal));
                if (m.ApplyGst)
                {
                    SummaryRow(col, $"CGST ({m.GstPercentage / 2:0.##}%)", PdfKit.Money(m.GstAmount / 2), PdfKit.Muted);
                    SummaryRow(col, $"SGST ({m.GstPercentage / 2:0.##}%)", PdfKit.Money(m.GstAmount / 2), PdfKit.Muted);
                }

                if (m.DueBreakdown is { } due)
                {
                    SummaryRow(col, "Previous balance", Balance(due.PreviousBalance));
                    SummaryRow(col, "Payments received", PdfKit.Signed(-due.Received), PdfKit.Success);
                }
                else
                {
                    SummaryRow(col, m.OldDue < 0 ? "Advance (credit)" : "Old due", Balance(m.OldDue));
                }
                foreach (var adj in m.Adjustments)
                {
                    SummaryRow(col, adj.Description, PdfKit.Signed(adj.Amount), adj.Amount < 0 ? PdfKit.Danger : PdfKit.Success);
                }

                col.Item().PaddingTop(4).Background(PdfKit.Teal).PaddingVertical(7).PaddingHorizontal(9).Row(r =>
                {
                    r.RelativeItem().Text("Grand total").FontColor(Colors.White).SemiBold();
                    r.AutoItem().Text(PdfKit.Money(m.GrandTotal)).FontFamily(PdfKit.HeadingFont).FontColor(Colors.White).Bold().FontSize(11.5f);
                });
            });
        });
    }

    /// <summary>An advance (negative balance) gets a minus sign.</summary>
    private static string Balance(decimal value) => value < 0 ? "\u2212 " + PdfKit.Money(-value) : PdfKit.Money(value);

    private static void SummaryRow(ColumnDescriptor col, string label, string value, string? color = null)
    {
        col.Item().BorderBottom(0.5f).BorderColor(PdfKit.LineSoft).PaddingVertical(4).PaddingHorizontal(2).Row(r =>
        {
            var l = r.RelativeItem().Text(label);
            var v = r.AutoItem().Text(value).SemiBold();
            if (color is not null)
            {
                l.FontColor(color);
                v.FontColor(color);
            }
        });
    }

    private static void Closing(IContainer container, DoctorBillPdfModel m)
    {
        container.PaddingTop(8).Column(col =>
        {
            col.Spacing(8);

            col.Item().Column(words =>
            {
                words.Item().Text("Amount in words").FontSize(7.5f).SemiBold().FontColor(PdfKit.Muted);
                words.Item().Text($"Rupees {PdfKit.AmountInWords(m.GrandTotal)} Only." + (m.GrandTotal < 0 ? " (credit in your favour)" : "")).Italic();
            });

            if (!string.IsNullOrWhiteSpace(m.PaymentCaption))
            {
                col.Item().AlignLeft().Background(PdfKit.TealTint).Border(0.8f).BorderColor("#9FD3C9").CornerRadius(6)
                    .PaddingVertical(5).PaddingHorizontal(9)
                    .Text(m.PaymentCaption).SemiBold().FontColor(PdfKit.TealDark);
            }

            if (m.Upi is { } upi)
            {
                col.Item().EnsureSpace(100).Element(c => UpiBox(c, upi, m.GrandTotal));
            }

            if (!string.IsNullOrWhiteSpace(m.Note))
            {
                col.Item().BorderLeft(2.5f).BorderColor(PdfKit.Gold).Background(PdfKit.GoldTint).PaddingVertical(5).PaddingHorizontal(8)
                    .Column(n =>
                    {
                        n.Item().Text("Note").FontSize(7.5f).SemiBold().FontColor(PdfKit.Muted);
                        n.Item().Text(m.Note);
                    });
            }
        });
    }

    /// <summary>QR code (scan with any UPI app) + a link that opens the Pay page.</summary>
    private static void UpiBox(IContainer container, BillUpi upi, decimal amount)
    {
        container.Border(0.8f).BorderColor("#9FD3C9").Background(PdfKit.TealTint).CornerRadius(8).Padding(7).Row(row =>
        {
            row.ConstantItem(78).Hyperlink(upi.PayPageUrl).Background(Colors.White).Padding(2).Image(upi.QrPng);
            row.ConstantItem(10);
            row.RelativeItem().AlignMiddle().Column(col =>
            {
                col.Spacing(2);
                col.Item().Text("Scan & pay with any UPI app").FontFamily(PdfKit.HeadingFont).Bold().FontColor(PdfKit.TealDark);
                col.Item().Hyperlink(upi.PayPageUrl)
                    .Text($"Tap here to pay {PdfKit.Money(amount)}").SemiBold().Underline().FontColor(PdfKit.Teal);
                col.Item().Text(t =>
                {
                    t.Span("UPI ID: ").FontColor(PdfKit.Muted).FontSize(8);
                    t.Span(upi.UpiId).SemiBold().FontSize(8);
                    t.Span("  ·  " + upi.PayeeName).FontColor(PdfKit.Muted).FontSize(8);
                });
                col.Item().Text("Note: " + upi.Note).FontSize(8).FontColor(PdfKit.Muted);
            });
        });
    }

    private static void Footer(IContainer container)
    {
        container.PaddingTop(6).BorderTop(0.5f).BorderColor(PdfKit.Line).PaddingTop(5).Row(r =>
        {
            r.RelativeItem().AlignBottom().Text("Thank you for partnering with Natural Dental Labs.").FontColor("#3D453F");
            r.AutoItem().AlignBottom().PaddingHorizontal(10)
                .Text("\u2013 M Chandramohan Reddy").FontFamily(PdfKit.SignatureFont).Bold().FontSize(14).FontColor(PdfKit.Ink);
            r.ConstantItem(62).AlignBottom().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(PdfKit.Muted));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        });
    }
}
