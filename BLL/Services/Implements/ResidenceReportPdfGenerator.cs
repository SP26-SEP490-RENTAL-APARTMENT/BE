using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using BLL.Services.Interfaces;
using Common.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BLL.Services.Implements
{
    public class ResidenceReportPdfGenerator : IResidenceReportPdfGenerator
    {
        public Task<byte[]> GenerateAsync(TemporaryResidenceReportDetailsDto details, CancellationToken cancellationToken = default)
        {
            if (details == null) throw new ArgumentNullException(nameof(details));

            var isVietnamese = string.Equals(details.TenantNationality, "VN", StringComparison.OrdinalIgnoreCase);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Content().Column(column =>
                    {
                        column.Spacing(5);

                        column.Item()
                            .AlignCenter()
                            .Text(isVietnamese ? "TỜ KHAI TẠM TRÚ (CT01)" : "NA17 - TEMPORARY RESIDENCE FORM")
                            .FontSize(16)
                            .Bold();

                        column.Item().Text(text =>
                        {
                            text.Span("Mã báo cáo / Report No: ").SemiBold();
                            text.Span(details.ReportNumber ?? "N/A");
                        });

                        column.Item().Text(text =>
                        {
                            text.Span("Ngày báo cáo / Report Date: ").SemiBold();
                            var date = details.ReportDate?.ToDateTime(TimeOnly.MinValue).ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));
                            text.Span(date ?? "N/A");
                        });

                        column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        column.Item().Text(isVietnamese ? "1. Thông tin người tạm trú" : "1. Temporary resident information").Bold();

                        column.Item().Text(text =>
                        {
                            text.Span(isVietnamese ? "Họ tên / Full name: " : "Full name: ").SemiBold();
                            text.Span(details.TenantFullName ?? "N/A");
                        });

                        column.Item().Text(text =>
                        {
                            text.Span(isVietnamese ? "Quốc tịch / Nationality: " : "Nationality: ").SemiBold();
                            text.Span(details.TenantNationality);
                        });

                        column.Item().Text(text =>
                        {
                            text.Span(isVietnamese ? "Số hộ chiếu / Passport No: " : "Passport No: ").SemiBold();
                            text.Span(details.TenantPassportId);
                        });

                        column.Item().Text(text =>
                        {
                            text.Span(isVietnamese ? "Số điện thoại / Phone: " : "Phone: ").SemiBold();
                            text.Span(details.TenantPhone ?? "N/A");
                        });

                        column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        column.Item().Text(isVietnamese ? "2. Thông tin nơi tạm trú" : "2. Temporary residence location").Bold();

                        column.Item().Text(text =>
                        {
                            text.Span(isVietnamese ? "Tên căn hộ / Apartment: " : "Apartment: ").SemiBold();
                            text.Span(details.ApartmentTitle);
                        });

                        column.Item().Text(text =>
                        {
                            text.Span(isVietnamese ? "Địa chỉ / Address: " : "Address: ").SemiBold();
                            text.Span(details.ApartmentAddress ?? "");
                            if (!string.IsNullOrWhiteSpace(details.ApartmentDistrict))
                            {
                                text.Span(", ");
                                text.Span(details.ApartmentDistrict);
                            }
                            if (!string.IsNullOrWhiteSpace(details.ApartmentCity))
                            {
                                text.Span(", ");
                                text.Span(details.ApartmentCity);
                            }
                        });

                        column.Item().Text(text =>
                        {
                            text.Span(isVietnamese ? "Thời gian tạm trú / Stay period: " : "Stay period: ").SemiBold();
                            var from = details.CheckInDate.ToDateTime(TimeOnly.MinValue).ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));
                            var to = details.CheckOutDate.ToDateTime(TimeOnly.MinValue).ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));
                            text.Span($"{from} - {to}");
                        });

                        column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        column.Item().Text(isVietnamese ? "3. Thông tin chủ cơ sở lưu trú" : "3. Landlord / host information").Bold();

                        column.Item().Text(text =>
                        {
                            text.Span(isVietnamese ? "Họ tên / Full name: " : "Full name: ").SemiBold();
                            text.Span(details.LandlordFullName ?? "N/A");
                        });

                        column.Item().Text(text =>
                        {
                            text.Span(isVietnamese ? "Số điện thoại / Phone: " : "Phone: ").SemiBold();
                            text.Span(details.LandlordPhone ?? "N/A");
                        });

                        column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        column.Item().Text(isVietnamese ? "4. Thông tin báo cáo" : "4. Reporting information").Bold();

                        column.Item().Text(text =>
                        {
                            text.Span(isVietnamese ? "Đã báo công an / Reported to police: " : "Reported to police: ").SemiBold();
                            var value = details.ReportedToPolice == true ? (isVietnamese ? "Có" : "Yes") : (isVietnamese ? "Không" : "No");
                            text.Span(value);
                        });

                        column.Item().Text(isVietnamese
                            ? "(Biểu mẫu này chỉ mang tính chất tham khảo, có thể cần chỉnh sửa để khớp với mẫu CT01/NA17 chính thức)."
                            : "(This generated form is for reference only and may need adjustment to fully match official CT01/NA17 templates.)")
                            .FontSize(9)
                            .Italic();
                    });
                });
            });

            var pdfBytes = document.GeneratePdf();
            return Task.FromResult(pdfBytes);
        }
    }
}
