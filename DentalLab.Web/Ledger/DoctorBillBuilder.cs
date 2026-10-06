using System.Globalization;
using DentalLab.Application.Abstractions;
using DentalLab.Application.Models.Cases;
using DentalLab.Web.Payments;
using DentalLab.Web.Pdf;
using DentalLab.Web.Settings;

namespace DentalLab.Web.Ledger;

/// <summary>
/// Builds a doctor's bill PDF without opening the Doctor Bill page (used by the
/// Monthly Summary and Doctor Payments pages). Uses the same defaults as a freshly
/// generated bill: Case ID and patient hidden, type summary and watermark shown,
/// default note, UPI QR with the saved default UPI ID, and the automatic old due.
/// If this exact period was saved to the ledger, the saved GST, adjustments and
/// old due are used, so the PDF matches the bill that was issued.
/// </summary>
public sealed class DoctorBillBuilder(
    ICaseService caseService,
    IDoctorService doctorService,
    DoctorLedgerStore ledger,
    AppSettingsStore settings,
    PayLinkService payLinks)
{
    public const string DefaultNote = "Kindly clear your dues by the 10th of this month.";

    public sealed record BuiltBill(byte[] Pdf, string FileName, decimal GrandTotal);

    /// <param name="baseUri">The site address (NavigationManager.BaseUri), used for the "tap to pay" link.</param>
    public async Task<BuiltBill?> BuildAsync(int docId, DateOnly from, DateOnly to, string baseUri, CancellationToken ct = default)
    {
        var doctor = await doctorService.GetByIdAsync(docId, ct);
        if (doctor is null)
        {
            return null;
        }

        // ---- cases (same search the Doctor Bill page uses) ----
        var filter = new CaseFilter { DocId = docId, FromDate = from, ToDate = to, Status = "Active", PageNumber = 1, PageSize = 200 };
        var collected = new List<CaseListItem>();
        while (true)
        {
            var page = await caseService.SearchAsync(filter, ct);
            collected.AddRange(page.Items);
            if (!page.HasNextPage || filter.PageNumber >= 500)
            {
                break;
            }

            filter.PageNumber++;
        }

        var cases = collected
            .Where(c => c.DocId == docId && !c.IsCancelled && c.CaseDate >= from && c.CaseDate <= to)
            .GroupBy(c => c.CaseId).Select(g => g.First())
            .OrderBy(c => c.CaseDate)
            .ToList();
        var subtotal = cases.Sum(c => c.TotalAmount);

        // ---- old due, GST and adjustments ----
        decimal oldDue = 0, gst = 0;
        BillDueBreakdown? breakdown = null;
        var adjustments = new List<BillAdjustment>();

        var saved = await ledger.GetSavedBillAsync(docId, from, to, ct);
        if (saved is not null)
        {
            oldDue = saved.OldDue;
            gst = saved.GstAmount;
            if (saved.AdjustmentsTotal != 0)
            {
                adjustments.Add(new BillAdjustment("Adjustments", saved.AdjustmentsTotal));
            }
        }
        else
        {
            var due = await ledger.GetOldDueAsync(docId, from, DoctorLedgerStore.Today, ct);
            if (!due.BeforeTracking)
            {
                oldDue = due.OldDue;
                if (due.ReceivedInWindow != 0)
                {
                    breakdown = new BillDueBreakdown(due.PreviousBalance, due.ReceivedInWindow);
                }
            }
        }

        var grandTotal = subtotal + gst + oldDue + adjustments.Sum(a => a.Amount);

        // ---- UPI ----
        var upiId = await settings.GetAsync(BillDefaults.UpiIdKey, BillDefaults.UpiId);
        var payee = await settings.GetAsync(BillDefaults.PayeeNameKey, BillDefaults.PayeeName);
        BillUpi? upi = null;
        if (grandTotal > 0 && UpiPayment.IsValidUpiId(upiId))
        {
            var period = from.Year == to.Year && from.Month == to.Month
                ? from.ToString("MMMyyyy", CultureInfo.InvariantCulture)
                : $"{from:ddMMM}-{to:ddMMMyy}";
            var request = new UpiPayRequest(upiId.Trim(), string.IsNullOrWhiteSpace(payee) ? UpiPayment.DefaultPayeeName : payee.Trim(),
                Math.Round(grandTotal, 2), UpiPayment.CleanNote($"Bill {period} {doctor.DocName}"));
            var link = UpiPayment.BuildLink(request);
            var pageUrl = baseUri.TrimEnd('/') + "/pay/" + payLinks.CreateToken(request);
            upi = new BillUpi(request.UpiId, request.PayeeName, request.Note, pageUrl, UpiPayment.QrPng(link));
        }

        var model = new DoctorBillPdfModel
        {
            DoctorName = doctor.DocName,
            PeriodLabel = $"{from:dd MMM yyyy} – {to:dd MMM yyyy}",
            GeneratedAt = DateTime.Now,
            ShowCaseId = false,
            ShowPatient = false,
            ShowWatermark = true,
            ShowTypeSummary = true,
            ApplyGst = gst != 0,
            GstPercentage = gst != 0 && subtotal != 0 ? Math.Round(gst * 100m / subtotal, 2) : 0,
            GstAmount = gst,
            Subtotal = subtotal,
            OldDue = oldDue,
            DueBreakdown = breakdown,
            Adjustments = adjustments,
            Note = DefaultNote,
            Upi = upi,
            GrandTotal = grandTotal,
            Cases = cases.Select(c => new BillCase(
                c.CaseId, c.CaseDate, c.PatientName, c.PatientRef, c.TotalAmount,
                c.Lines.Select(l => new BillLine(l.CaseTypeCode, l.Scope, l.Arch, l.UR, l.UL, l.LR, l.LL, l.Units, l.UnitRate)).ToList()))
                .ToList(),
            TypeSummary = cases
                .SelectMany(c => c.Lines)
                .GroupBy(l => string.IsNullOrWhiteSpace(l.CaseTypeCode) ? "Unknown" : l.CaseTypeCode)
                .Select(g => new BillTypeSummary(g.Key, g.Sum(l => l.Units), g.Sum(l => l.Amount)))
                .OrderBy(t => t.Type)
                .ToList()
        };

        var bytes = await Task.Run(() => DoctorBillPdf.Generate(model), ct);
        return new BuiltBill(bytes, FileName(doctor.DocName, from, to), grandTotal);
    }

    /// <summary>e.g. DrAneesha_Bill_Sep2026.pdf for a whole month.</summary>
    public static string FileName(string doctorName, DateOnly from, DateOnly to)
    {
        var doctor = PdfKit.FileName(doctorName);
        var wholeMonth = from.Day == 1 && to.Year == from.Year && to.Month == from.Month;
        return (wholeMonth ? $"{doctor}_Bill_{from.ToString("MMMyyyy", CultureInfo.InvariantCulture)}" : $"{doctor}_Bill_{from:ddMMMyyyy}-{to:ddMMMyyyy}") + ".pdf";
    }
}
