using DentalLab.Web.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DentalLab.Web.Pdf;

public static class MonthlySummaryPdf
{
    /// <param name="showPrices">False hides every money figure (shares then use units).</param>
    public static byte[] Generate(MonthlySummaryReport r, string monthLabel, string preparedBy, DateTime generatedAt, bool showPrices)
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

                page.Header().Element(c => Letterhead(c, r, monthLabel, preparedBy, generatedAt));
                page.Content().PaddingTop(10).Element(c => Body(c, r, monthLabel, showPrices));
                page.Footer().Element(c => Footer(c, monthLabel));
            });
        })
        .WithMetadata(new DocumentMetadata
        {
            Title = $"Monthly production summary - {monthLabel}",
            Author = "Natural Dental Labs",
            Creator = "Natural Dental Labs"
        })
        .GeneratePdf();
    }

    // ------------------------------------------------------------------ header / footer

    private static void Letterhead(IContainer c, MonthlySummaryReport r, string monthLabel, string preparedBy, DateTime generatedAt)
    {
        c.BorderBottom(2).BorderColor(PdfKit.Teal).PaddingBottom(8).Row(row =>
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
                    col.Item().Text("Natural Dental Labs").FontSize(9.5f).SemiBold().FontColor(PdfKit.Teal);
                    col.Item().Text("Monthly Production Summary").FontFamily(PdfKit.HeadingFont).FontSize(16).Bold();
                    col.Item().Text($"{monthLabel}  ·  {r.From:dd MMM} – {r.To:dd MMM yyyy}").FontColor(PdfKit.Muted);
                });
            });

            row.ConstantItem(200).AlignMiddle().Column(col =>
            {
                Meta(col, "Generated", generatedAt.ToString("dd MMM yyyy, hh:mm tt"));
                Meta(col, "Prepared by", preparedBy);
                Meta(col, "Compared with", r.PrevLabel);
            });
        });
    }

    private static void Meta(ColumnDescriptor col, string label, string value) =>
        col.Item().AlignRight().Text(t =>
        {
            t.Span(label + "  ").FontColor(PdfKit.Muted).FontSize(8);
            t.Span(value).SemiBold().FontSize(8.5f);
        });

    private static void Footer(IContainer c, string monthLabel)
    {
        c.PaddingTop(6).BorderTop(0.5f).BorderColor(PdfKit.Line).PaddingTop(4).Row(r =>
        {
            r.RelativeItem().Text($"Natural Dental Labs · Monthly Production Summary · {monthLabel} · Active cases only")
                .FontSize(7.5f).FontColor(PdfKit.Muted);
            r.AutoItem().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(PdfKit.Muted));
                t.Span("Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        });
    }

    // ------------------------------------------------------------------ body

    private static void Body(IContainer c, MonthlySummaryReport r, string monthLabel, bool showPrices)
    {
        c.Column(col =>
        {
            col.Spacing(14);

            if (r.Cases == 0)
            {
                col.Item().PaddingVertical(40).AlignCenter().Text($"No active cases in {monthLabel}.").FontColor(PdfKit.Muted);
                return;
            }

            col.Item().Element(x => Kpis(x, r, showPrices));
            col.Item().Element(x => Highlights(x, r));
            col.Item().EnsureSpace(60).Element(x => Split(x, r, showPrices));
            col.Item().Element(x => TypesTable(x, r, showPrices));
            col.Item().Element(x => DoctorsTable(x, r, showPrices));
            col.Item().EnsureSpace(150).Element(x => Daily(x, r));
            if (r.EnteredBy.Count > 0)
            {
                col.Item().EnsureSpace(80).Element(x => EnteredBy(x, r));
            }
        });
    }

    private static void Kpis(IContainer c, MonthlySummaryReport r, bool showPrices)
    {
        var tiles = new List<(string Label, string Value, string Sub, bool Main)>
        {
            ("Cases", PdfKit.Number(r.Cases), Delta(r.Cases, r.PrevCases), false),
            ("Units", PdfKit.Number(r.Units), Delta(r.Units, r.PrevUnits), false)
        };
        if (showPrices) tiles.Add(("Production value", PdfKit.Money(r.Amount), Delta(r.Amount, r.PrevAmount), true));
        tiles.Add(("Doctors", r.Doctors.ToString(), Delta(r.Doctors, r.PrevDoctors), false));
        tiles.Add(("Units per case", r.UnitsPerCase.ToString("0.0"), $"{r.PatientsCount} patients", false));
        if (showPrices) tiles.Add(("Value per case", PdfKit.Money0(r.AmountPerCase), $"{PdfKit.Money0(r.AmountPerUnit)} per unit", false));

        c.Table(t =>
        {
            t.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.RelativeColumn(); cols.RelativeColumn(); });
            foreach (var tile in tiles)
            {
                t.Cell().Padding(3).Element(cell =>
                {
                    var box = tile.Main
                        ? cell.Background(PdfKit.Teal).CornerRadius(8).Padding(9)
                        : cell.Background(PdfKit.TealTint).CornerRadius(8).Padding(9);
                    box.Column(col =>
                    {
                        col.Item().Text(tile.Label).FontSize(8).SemiBold().FontColor(tile.Main ? "#C7EFE8" : PdfKit.Muted);
                        col.Item().Text(tile.Value).FontFamily(PdfKit.HeadingFont).FontSize(15).Bold().FontColor(tile.Main ? Colors.White : PdfKit.Ink);
                        col.Item().Text(tile.Sub).FontSize(7.5f).FontColor(tile.Main ? "#E8FFFB" : DeltaColor(tile.Sub));
                    });
                });
            }
        });
    }

    private static void Highlights(IContainer c, MonthlySummaryReport r)
    {
        var items = new (string, string)[]
        {
            ("Top doctor", r.TopDoctor),
            ("Top case type", r.TopType),
            ("Busiest day", r.BusiestDay),
            ("Working days with cases", $"{r.ActiveDays} of {r.DaysInMonth}"),
            ("Doctors new vs last month", r.NewDoctors),
            ("Cancelled this month", r.Cancelled.ToString())
        };

        c.Border(0.5f).BorderColor(PdfKit.Line).Table(t =>
        {
            t.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.RelativeColumn(); cols.RelativeColumn(); });
            foreach (var (label, value) in items)
            {
                t.Cell().Border(0.5f).BorderColor(PdfKit.Line).PaddingVertical(5).PaddingHorizontal(7).Column(col =>
                {
                    col.Item().Text(label).FontSize(7.5f).FontColor(PdfKit.Muted);
                    col.Item().Text(value).SemiBold();
                });
            }
        });
    }

    private static void Split(IContainer c, MonthlySummaryReport r, bool showPrices)
    {
        c.Column(col =>
        {
            SectionTitle(col, "Units by charging basis");

            var tooth = Math.Max(r.ToothUnits, 0);
            var arch = Math.Max(r.ArchUnits, 0);
            col.Item().Height(12).Row(bar =>
            {
                if (tooth + arch == 0)
                {
                    bar.RelativeItem().Background(PdfKit.LineSoft);
                    return;
                }
                if (tooth > 0) bar.RelativeItem(tooth).Background(PdfKit.Teal);
                if (arch > 0) bar.RelativeItem(arch).Background(PdfKit.Gold);
            });

            col.Item().PaddingTop(5).Row(row =>
            {
                row.ConstantItem(9).AlignMiddle().Height(7).Width(7).Background(PdfKit.Teal);
                row.ConstantItem(4);
                row.RelativeItem().Text(t =>
                {
                    t.Span("Per tooth  ");
                    t.Span($"{PdfKit.Number(r.ToothUnits)} units ({Pct(r.ToothUnits, r.Units)}%)").SemiBold();
                    if (showPrices) t.Span($"  ·  {PdfKit.Money(r.ToothAmount)}").FontColor(PdfKit.Muted);
                });
                row.ConstantItem(9).AlignMiddle().Height(7).Width(7).Background(PdfKit.Gold);
                row.ConstantItem(4);
                row.RelativeItem().Text(t =>
                {
                    t.Span("Per arch  ");
                    t.Span($"{PdfKit.Number(r.ArchUnits)} units ({Pct(r.ArchUnits, r.Units)}%)").SemiBold();
                    if (showPrices) t.Span($"  ·  {PdfKit.Money(r.ArchAmount)}").FontColor(PdfKit.Muted);
                });
            });
        });
    }

    private static void TypesTable(IContainer c, MonthlySummaryReport r, bool showPrices)
    {
        c.Column(col =>
        {
            SectionTitle(col, "Case types");
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(52);
                    cols.RelativeColumn(2);
                    cols.ConstantColumn(40);
                    cols.ConstantColumn(38);
                    cols.ConstantColumn(38);
                    if (showPrices) { cols.ConstantColumn(62); cols.ConstantColumn(72); }
                    cols.ConstantColumn(80);
                });

                t.Header(h =>
                {
                    Head(h.Cell(), "Code");
                    Head(h.Cell(), "Case type");
                    Head(h.Cell(), "Basis");
                    Head(h.Cell(), "Cases", true);
                    Head(h.Cell(), "Units", true);
                    if (showPrices) { Head(h.Cell(), "Avg rate", true); Head(h.Cell(), "Amount", true); }
                    Head(h.Cell(), "Share");
                });

                foreach (var row in r.Types)
                {
                    var share = showPrices ? Pct(row.Amount, r.Amount) : Pct(row.Units, r.Units);
                    Body(t.Cell()).Text(row.Code).SemiBold().FontColor(PdfKit.TealDark);
                    Body(t.Cell()).Text(row.Name);
                    Body(t.Cell()).Text(row.Scope == 'A' ? "Arch" : "Tooth");
                    Body(t.Cell()).AlignRight().Text(row.Cases.ToString());
                    Body(t.Cell()).AlignRight().Text(row.Units.ToString());
                    if (showPrices)
                    {
                        Body(t.Cell()).AlignRight().Text(PdfKit.Money(row.Units == 0 ? 0 : row.Amount / row.Units));
                        Body(t.Cell()).AlignRight().Text(PdfKit.Money(row.Amount));
                    }
                    Body(t.Cell()).Element(x => ShareBar(x, share));
                }

                Foot(t.Cell().ColumnSpan(3)).Text($"Total ({r.Types.Count} types)").Bold();
                Foot(t.Cell()).AlignRight().Text(r.Cases.ToString()).Bold();
                Foot(t.Cell()).AlignRight().Text(r.Units.ToString()).Bold();
                if (showPrices)
                {
                    Foot(t.Cell()).AlignRight().Text(PdfKit.Money(r.AmountPerUnit)).Bold();
                    Foot(t.Cell()).AlignRight().Text(PdfKit.Money(r.Amount)).Bold();
                }
                Foot(t.Cell()).Text("100%").Bold();
            });
            col.Item().PaddingTop(3).Text("\"Cases\" counts cases that contain the type; one case can contain several types.")
                .FontSize(7.5f).FontColor(PdfKit.Muted);
        });
    }

    private static void DoctorsTable(IContainer c, MonthlySummaryReport r, bool showPrices)
    {
        c.Column(col =>
        {
            SectionTitle(col, "Doctors");
            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(22);
                    cols.RelativeColumn(2);
                    cols.ConstantColumn(38);
                    cols.ConstantColumn(38);
                    if (showPrices) cols.ConstantColumn(72);
                    cols.ConstantColumn(58);
                    cols.ConstantColumn(66);
                    cols.ConstantColumn(80);
                });

                t.Header(h =>
                {
                    Head(h.Cell(), "#", true);
                    Head(h.Cell(), "Doctor");
                    Head(h.Cell(), "Cases", true);
                    Head(h.Cell(), "Units", true);
                    if (showPrices) Head(h.Cell(), "Amount", true);
                    Head(h.Cell(), "Main type");
                    Head(h.Cell(), "vs last month", true);
                    Head(h.Cell(), "Share");
                });

                for (var i = 0; i < r.DoctorRows.Count; i++)
                {
                    var d = r.DoctorRows[i];
                    var share = showPrices ? Pct(d.Amount, r.Amount) : Pct(d.Units, r.Units);
                    var delta = showPrices ? Delta(d.Amount, d.PrevAmount) : Delta(d.Units, d.PrevUnits);

                    Body(t.Cell()).AlignRight().Text((i + 1).ToString());
                    Body(t.Cell()).Text(d.Name).SemiBold();
                    Body(t.Cell()).AlignRight().Text(d.Cases.ToString());
                    Body(t.Cell()).AlignRight().Text(d.Units.ToString());
                    if (showPrices) Body(t.Cell()).AlignRight().Text(PdfKit.Money(d.Amount));
                    Body(t.Cell()).Text(d.TopType);
                    Body(t.Cell()).AlignRight().Text(ShortDelta(delta)).FontSize(8).FontColor(DeltaColor(delta));
                    Body(t.Cell()).Element(x => ShareBar(x, share));
                }

                Foot(t.Cell().ColumnSpan(2)).Text($"Total ({r.Doctors} doctors)").Bold();
                Foot(t.Cell()).AlignRight().Text(r.Cases.ToString()).Bold();
                Foot(t.Cell()).AlignRight().Text(r.Units.ToString()).Bold();
                if (showPrices) Foot(t.Cell()).AlignRight().Text(PdfKit.Money(r.Amount)).Bold();
                Foot(t.Cell());
                var totalDelta = showPrices ? Delta(r.Amount, r.PrevAmount) : Delta(r.Units, r.PrevUnits);
                Foot(t.Cell()).AlignRight().Text(ShortDelta(totalDelta)).FontSize(8).Bold().FontColor(DeltaColor(totalDelta));
                Foot(t.Cell()).Text("100%").Bold();
            });
        });
    }

    private static void Daily(IContainer c, MonthlySummaryReport r)
    {
        const float chartHeight = 90;      // tallest bar
        const float labelRoom = 14;        // space kept for the number above the bar

        c.Column(col =>
        {
            SectionTitle(col, "Daily activity  (units received per day)");

            col.Item().Border(0.5f).BorderColor(PdfKit.Line).Padding(5).Row(row =>
            {
                row.Spacing(2);
                foreach (var day in r.Days)
                {
                    var barHeight = r.MaxDayUnits == 0 ? 0 : Math.Max(day.Units > 0 ? 3 : 0, day.Units * chartHeight / r.MaxDayUnits);
                    row.RelativeItem().Column(dc =>
                    {
                        var area = dc.Item().Height(chartHeight + labelRoom);
                        if (day.IsSunday) area = area.Background(PdfKit.GoldTint);
                        area.AlignBottom().Column(bc =>
                        {
                            if (day.Units > 0)
                            {
                                bc.Item().AlignCenter().Text(day.Units.ToString()).FontSize(5.5f).FontColor(PdfKit.Muted);
                            }
                            if (barHeight > 0)
                            {
                                bc.Item().Height((float)barHeight).PaddingHorizontal(1).Background(PdfKit.Teal);
                            }
                        });
                        dc.Item().AlignCenter().Text(day.Date.Day.ToString()).FontSize(5.5f).FontColor(PdfKit.Muted);
                    });
                }
            });

            col.Item().PaddingTop(4).Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(PdfKit.Muted));
                t.Span("Shaded columns are Sundays.   Weekly totals:  ");
                foreach (var w in r.Weeks)
                {
                    t.Span($"{w.Label}: ");
                    t.Span($"{w.Units} units / {w.Cases} cases").SemiBold().FontColor(PdfKit.Ink);
                    t.Span("     ");
                }
            });
        });
    }

    private static void EnteredBy(IContainer c, MonthlySummaryReport r)
    {
        c.Column(col =>
        {
            SectionTitle(col, "Cases entered by");
            col.Item().Width(380).Table(t =>
            {
                t.ColumnsDefinition(cols => { cols.RelativeColumn(2); cols.ConstantColumn(40); cols.ConstantColumn(40); cols.ConstantColumn(80); });
                t.Header(h =>
                {
                    Head(h.Cell(), "User");
                    Head(h.Cell(), "Cases", true);
                    Head(h.Cell(), "Units", true);
                    Head(h.Cell(), "Share");
                });
                foreach (var u in r.EnteredBy)
                {
                    Body(t.Cell()).Text(u.Name);
                    Body(t.Cell()).AlignRight().Text(u.Cases.ToString());
                    Body(t.Cell()).AlignRight().Text(u.Units.ToString());
                    Body(t.Cell()).Element(x => ShareBar(x, Pct(u.Cases, r.Cases)));
                }
            });
        });
    }

    // ------------------------------------------------------------------ pieces

    private static void SectionTitle(ColumnDescriptor col, string title) =>
        col.Item().PaddingBottom(5).BorderLeft(3).BorderColor(PdfKit.Gold).PaddingLeft(6)
            .Text(title).FontFamily(PdfKit.HeadingFont).FontSize(11).Bold();

    private static void Head(IContainer cell, string text, bool right = false)
    {
        var c = cell.Background(PdfKit.TealTint).BorderBottom(0.8f).BorderColor("#CFE3DF").PaddingVertical(4).PaddingHorizontal(4);
        (right ? c.AlignRight() : c).Text(text).FontSize(7.5f).SemiBold().FontColor(PdfKit.TealDark);
    }

    private static IContainer Body(IContainer cell) =>
        cell.BorderBottom(0.5f).BorderColor(PdfKit.LineSoft).PaddingVertical(3.5f).PaddingHorizontal(4).AlignMiddle();

    private static IContainer Foot(IContainer cell) =>
        cell.Background(PdfKit.GoldTint).BorderTop(1.2f).BorderColor(PdfKit.Teal).PaddingVertical(4).PaddingHorizontal(4).AlignMiddle();

    private static void ShareBar(IContainer c, int share)
    {
        c.Row(row =>
        {
            row.RelativeItem().AlignMiddle().Height(6).Row(bar =>
            {
                var filled = Math.Clamp(share, 0, 100);
                if (filled > 0) bar.RelativeItem(filled).Background(PdfKit.Teal);
                if (filled < 100) bar.RelativeItem(100 - filled).Background(PdfKit.LineSoft);
            });
            row.ConstantItem(28).AlignRight().Text($"{share}%").FontSize(7.5f).FontColor(PdfKit.Muted);
        });
    }

    private static int Pct(decimal part, decimal total) =>
        total <= 0 ? 0 : (int)Math.Round(part * 100m / total, MidpointRounding.AwayFromZero);

    private static string Delta(decimal current, decimal previous)
    {
        if (previous == 0) return current == 0 ? "no change" : "new this month";
        var change = Math.Round((current - previous) * 100m / previous, 1);
        if (change == 0) return "same as last month";
        return $"{(change > 0 ? "+" : "\u2212")}{Math.Abs(change):0.#}% vs last month";
    }

    private static string ShortDelta(string delta) => delta.Replace(" vs last month", "");

    private static string DeltaColor(string delta) =>
        delta.StartsWith('+') ? PdfKit.Success : delta.StartsWith('\u2212') ? PdfKit.Danger : PdfKit.Muted;
}
