using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BLL.Services.Interfaces;
using Common.DTOs;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace BLL.Services.Implements
{
    public class ResidenceReportDocxGenerator : IResidenceReportDocxGenerator
    {
        private const string TemplateFolder = "DocumentForm";
        private const string VietnameseTemplateName = "CT01.docx";
        private const string ForeignTemplateName = "A17.docx";

        private static class PlaceholderTags
        {
            public const string ApartmentTitle = "{{ApartmentTitle}}";
            public const string ApartmentAddress = "{{ApartmentAddress}}";
            public const string LandlordName = "{{LandlordName}}";
            public const string LandlordPhone = "{{LandlordPhone}}";
            public const string TenantFullName = "{{TenantFullName}}";
            public const string TenantSex = "{{TenantSex}}";
            public const string TenantNationality = "{{TenantNationality}}";
            public const string TenantPassportId = "{{TenantPassportId}}";
            public const string TenantPhone = "{{TenantPhone}}";
            public const string TenantEmail = "{{TenantEmail}}";
            public const string CheckInDate = "{{CheckInDate}}";
            public const string CheckOutDate = "{{CheckOutDate}}";
            public const string StayPeriod = "{{StayPeriod}}";
            public const string Day = "{{Day}}";
            public const string Month = "{{Month}}";
            public const string Year = "{{Year}}";
        }

        public Task<byte[]> GenerateAsync(TemporaryResidenceReportDetailsDto details, CancellationToken cancellationToken = default)
        {
            if (details == null)
            {
                throw new ArgumentNullException(nameof(details));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var templatePath = ResolveTemplatePath(details.TenantNationality);

            using var templateStream = File.OpenRead(templatePath);
            using var outputStream = new MemoryStream();
            templateStream.CopyTo(outputStream);
            outputStream.Position = 0;

            using (var document = WordprocessingDocument.Open(outputStream, true))
            {
                ApplyTemplate(document, details);
                document.MainDocumentPart?.Document.Save();
            }

            return Task.FromResult(outputStream.ToArray());
        }

        private static string ResolveTemplatePath(string? tenantNationality)
        {
            var fileName = string.Equals(tenantNationality, "VN", StringComparison.OrdinalIgnoreCase)
                ? VietnameseTemplateName
                : ForeignTemplateName;

            var baseDirectory = AppContext.BaseDirectory;
            var candidatePaths = new[]
            {
                Path.Combine(baseDirectory, TemplateFolder, fileName),
                Path.Combine(Directory.GetCurrentDirectory(), TemplateFolder, fileName),
            };

            foreach (var candidatePath in candidatePaths)
            {
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            throw new FileNotFoundException($"Template file '{fileName}' was not found in the DocumentForm folder.");
        }

        private static void ApplyTemplate(WordprocessingDocument document, TemporaryResidenceReportDetailsDto details)
        {
            var body = document.MainDocumentPart?.Document.Body;
            if (body == null)
            {
                throw new InvalidOperationException("DOCX template is missing a document body.");
            }

            var placeholderValues = BuildPlaceholderValues(details);
            ReplacePlaceholders(body, placeholderValues);
        }

        private static Dictionary<string, string> BuildPlaceholderValues(TemporaryResidenceReportDetailsDto details)
        {
            var reportDate = details.ReportDate ?? DateOnly.FromDateTime(DateTime.Now);

            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [PlaceholderTags.ApartmentTitle] = details.ApartmentTitle ?? string.Empty,
                [PlaceholderTags.ApartmentAddress] = BuildApartmentAddress(details),
                [PlaceholderTags.LandlordName] = details.LandlordFullName ?? string.Empty,
                [PlaceholderTags.LandlordPhone] = details.LandlordPhone ?? string.Empty,
                [PlaceholderTags.TenantFullName] = details.TenantFullName ?? "N/A",
                [PlaceholderTags.TenantSex] = details.TenantSex ?? string.Empty,
                [PlaceholderTags.TenantNationality] = details.TenantNationality ?? string.Empty,
                [PlaceholderTags.TenantPassportId] = details.TenantPassportId ?? string.Empty,
                [PlaceholderTags.TenantPhone] = details.TenantPhone ?? string.Empty,
                [PlaceholderTags.TenantEmail] = details.TenantEmail ?? string.Empty,
                [PlaceholderTags.CheckInDate] = FormatDate(details.CheckInDate),
                [PlaceholderTags.CheckOutDate] = FormatDate(details.CheckOutDate),
                [PlaceholderTags.StayPeriod] = $"{FormatDate(details.CheckInDate)} - {FormatDate(details.CheckOutDate)}",
                [PlaceholderTags.Day] = reportDate.Day.ToString("00"),
                [PlaceholderTags.Month] = reportDate.Month.ToString("00"),
                [PlaceholderTags.Year] = reportDate.Year.ToString(),
            };
        }

        private static void ReplacePlaceholders(OpenXmlCompositeElement root, IReadOnlyDictionary<string, string> placeholderValues)
        {
            foreach (var paragraph in root.Descendants<Paragraph>())
            {
                var originalText = paragraph.InnerText;
                if (string.IsNullOrEmpty(originalText))
                {
                    continue;
                }

                var replacedText = originalText;

                foreach (var (placeholder, value) in placeholderValues)
                {
                    replacedText = replacedText.Replace(placeholder, value ?? string.Empty, StringComparison.Ordinal);
                }

                if (string.Equals(originalText, replacedText, StringComparison.Ordinal))
                {
                    continue;
                }

                SetParagraphText(paragraph, replacedText);
            }
        }

        private static void SetParagraphText(Paragraph paragraph, string text)
        {
            paragraph.RemoveAllChildren<Run>();

            paragraph.AppendChild(new Run(new Text(text ?? string.Empty)
            {
                Space = SpaceProcessingModeValues.Preserve
            }));
        }

        private static string BuildApartmentAddress(TemporaryResidenceReportDetailsDto details)
        {
            var addressParts = new List<string>();

            if (!string.IsNullOrWhiteSpace(details.ApartmentAddress))
            {
                addressParts.Add(details.ApartmentAddress);
            }

            if (!string.IsNullOrWhiteSpace(details.ApartmentDistrict))
            {
                addressParts.Add(details.ApartmentDistrict);
            }

            if (!string.IsNullOrWhiteSpace(details.ApartmentCity))
            {
                addressParts.Add(details.ApartmentCity);
            }

            return addressParts.Count == 0 ? string.Empty : string.Join(", ", addressParts);
        }

        private static string FormatDate(DateOnly? date)
        {
            return date.HasValue ? date.Value.ToString("dd/MM/yyyy") : string.Empty;
        }
    }
}