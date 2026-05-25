using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
            public const string LandlordNationalID = "{{LandlordNationalID}}";
            public const string LandlordPhone = "{{LandlordPhone}}";
            public const string TenantFullName = "{{TenantFullName}}";
            public const string TenantDateOfBirth = "{{TenantDateOfBirth}}";
            public const string TenantDobLegacy = "{{TenantDOB}}";
            public const string TenantNationalID = "{{TenantNationalID}}";
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

            var occupants = GetNormalizedOccupants(details);
            var isVietnameseTemplate = string.Equals(details.TenantNationality, "VN", StringComparison.OrdinalIgnoreCase);

            if (occupants.Count > 1)
            {
                var zipBytes = BuildOccupantZip(details, occupants, isVietnameseTemplate);
                return Task.FromResult(zipBytes);
            }

            var primaryDetails = CreateDetailsForOccupant(details, occupants[0]);
            // For single-occupant file we pass null for `occupants` so placeholders in the template are used.
            var docxBytes = GenerateSingleDocx(primaryDetails, null, isVietnameseTemplate);
            return Task.FromResult(docxBytes);
        }

        private static byte[] BuildOccupantZip(TemporaryResidenceReportDetailsDto baseDetails, IReadOnlyList<ResidenceReportOccupantDto> occupants, bool isVietnameseTemplate)
        {
            using var zipStream = new MemoryStream();
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var occupant in occupants)
                {
                    var occupantDetails = CreateDetailsForOccupant(baseDetails, occupant);
                    var docxBytes = GenerateSingleDocx(occupantDetails, null, isVietnameseTemplate: isVietnameseTemplate);
                    var entryName = $"residence-report-{baseDetails.BookingId}-occupant-{occupant.Order:00}.docx";
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var contentStream = new MemoryStream(docxBytes);
                    contentStream.CopyTo(entryStream);
                }
            }

            return zipStream.ToArray();
        }

        private static byte[] GenerateSingleDocx(TemporaryResidenceReportDetailsDto details, IReadOnlyList<ResidenceReportOccupantDto>? occupants, bool isVietnameseTemplate = false)
        {
            var templatePath = ResolveTemplatePath(details.TenantNationality);

            using var templateStream = File.OpenRead(templatePath);
            using var outputStream = new MemoryStream();
            templateStream.CopyTo(outputStream);
            outputStream.Position = 0;

            using (var document = WordprocessingDocument.Open(outputStream, true))
            {
                ApplyTemplate(document, details, isVietnameseTemplate);
                if (occupants != null && occupants.Count > 1)
                {
                    PopulateForeignOccupantRows(document, occupants);
                }

                document.MainDocumentPart?.Document.Save();
            }

            return outputStream.ToArray();
        }

        private static TemporaryResidenceReportDetailsDto CreateDetailsForOccupant(TemporaryResidenceReportDetailsDto source, ResidenceReportOccupantDto occupant)
        {
            return new TemporaryResidenceReportDetailsDto
            {
                ReportId = source.ReportId,
                BookingId = source.BookingId,
                LandlordId = source.LandlordId,
                TenantId = source.TenantId,
                TenantFullName = occupant.FullName,
                TenantPassportId = occupant.PassportId ?? string.Empty,
                TenantDateOfBirth = occupant.DateOfBirth ?? source.TenantDateOfBirth,
                TenantNationalIdCardNumber = occupant.NationalIdCardNumber,
                TenantNationality = occupant.Nationality ?? source.TenantNationality,
                TenantPhone = occupant.Phone,
                TenantEmail = occupant.Email,
                TenantSex = occupant.Sex,
                LandlordFullName = source.LandlordFullName,
                LandlordNationalIdCardNumber = source.LandlordNationalIdCardNumber,
                LandlordPhone = source.LandlordPhone,
                ApartmentTitle = source.ApartmentTitle,
                ApartmentAddress = source.ApartmentAddress,
                ApartmentDistrict = source.ApartmentDistrict,
                ApartmentCity = source.ApartmentCity,
                CheckInDate = source.CheckInDate,
                CheckOutDate = source.CheckOutDate,
                ReportedToPolice = source.ReportedToPolice,
                ReportDate = source.ReportDate,
                ReportNumber = source.ReportNumber,
                OccupantCount = source.OccupantCount,
                Occupants = source.Occupants
            };
        }

        private static List<ResidenceReportOccupantDto> GetNormalizedOccupants(TemporaryResidenceReportDetailsDto details)
        {
            if (details.Occupants.Count > 0)
            {
                return details.Occupants.OrderBy(o => o.Order).ToList();
            }

            return new List<ResidenceReportOccupantDto>
            {
                new ResidenceReportOccupantDto
                {
                    Order = 1,
                    IsPrimary = true,
                    FullName = details.TenantFullName,
                    PassportId = details.TenantPassportId,
                    NationalIdCardNumber = details.TenantNationalIdCardNumber,
                    Nationality = details.TenantNationality,
                    Sex = details.TenantSex,
                    Phone = details.TenantPhone,
                    Email = details.TenantEmail
                }
            };
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

        private static void ApplyTemplate(WordprocessingDocument document, TemporaryResidenceReportDetailsDto details, bool isVietnameseTemplate = false)
        {
            var body = document.MainDocumentPart?.Document.Body;
            if (body == null)
            {
                throw new InvalidOperationException("DOCX template is missing a document body.");
            }

            var placeholderValues = BuildPlaceholderValues(details, isVietnameseTemplate);
            ReplacePlaceholders(body, placeholderValues);
            PopulateNationalIdTablesIfAvailable(document, details);
        }

        public static void PopulateTwoTables(WordprocessingDocument document, string twelveDigitNumber1, string twelveDigitNumber2)
        {
            if (document?.MainDocumentPart == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var normalizedPartA = NormalizeNationalIdToTwelveDigits(twelveDigitNumber1);
            var normalizedPartB = NormalizeNationalIdToTwelveDigits(twelveDigitNumber2);

            // Validate 12-digit inputs because each digit maps to one table cell.
            if (normalizedPartA == null || normalizedPartB == null)
            {
                throw new ArgumentException("Input must be exactly 12 digits.");
            }

            var tableA = GetTableByContentControlTag(document.MainDocumentPart, "Table_PartA")
                ?? throw new InvalidOperationException("Table_PartA not found.");
            var tableB = GetTableByContentControlTag(document.MainDocumentPart, "Table_PartB")
                ?? throw new InvalidOperationException("Table_PartB not found.");

            PopulateTableRowWithString(tableA, normalizedPartA);
            PopulateTableRowWithString(tableB, normalizedPartB);
        }

        private static Dictionary<string, string> BuildPlaceholderValues(TemporaryResidenceReportDetailsDto details, bool isVietnameseTemplate = false)
        {
            var reportDate = details.ReportDate ?? DateOnly.FromDateTime(DateTime.Now);
            var tenantDobValue = FormatDate(details.TenantDateOfBirth);
            var tenantSex = ConvertSexToVietnamese(details.TenantSex);

            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [PlaceholderTags.ApartmentTitle] = details.ApartmentTitle ?? string.Empty,
                [PlaceholderTags.ApartmentAddress] = BuildApartmentAddress(details),
                [PlaceholderTags.LandlordName] = details.LandlordFullName ?? string.Empty,
                [PlaceholderTags.LandlordNationalID] = details.LandlordNationalIdCardNumber ?? string.Empty,
                [PlaceholderTags.LandlordPhone] = details.LandlordPhone ?? string.Empty,
                [PlaceholderTags.TenantFullName] = details.TenantFullName ?? "N/A",
                [PlaceholderTags.TenantDateOfBirth] = tenantDobValue,
                [PlaceholderTags.TenantDobLegacy] = tenantDobValue,
                ["TenantDateOfBirth"] = tenantDobValue,
                ["TenantDOB"] = tenantDobValue,
                [PlaceholderTags.TenantNationalID] = details.TenantNationalIdCardNumber ?? string.Empty,
                [PlaceholderTags.TenantSex] = tenantSex ?? string.Empty,
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

        private static string ConvertSexToVietnamese(string? sex)
        {
            if (string.IsNullOrWhiteSpace(sex))
            {
                return string.Empty;
            }

            return sex.ToLower() switch
            {
                "male" or "m" => "Nam",
                "female" or "f" => "Nữ",
                _ => sex  // Return original if no match
            };
        }

        private static void PopulateNationalIdTablesIfAvailable(WordprocessingDocument document, TemporaryResidenceReportDetailsDto details)
        {
            if (document.MainDocumentPart == null)
            {
                return;
            }

            var tenantNationalId = NormalizeNationalIdToTwelveDigits(details.TenantNationalIdCardNumber);
            var landlordNationalId = NormalizeNationalIdToTwelveDigits(details.LandlordNationalIdCardNumber);

            var tableA = GetTableByContentControlTag(document.MainDocumentPart, "Table_PartA");
            var tableB = GetTableByContentControlTag(document.MainDocumentPart, "Table_PartB");

            if (tableA != null && tenantNationalId != null)
            {
                PopulateTableRowWithString(tableA, tenantNationalId);
            }

            if (tableB != null && landlordNationalId != null)
            {
                PopulateTableRowWithString(tableB, landlordNationalId);
            }
        }

        private static void PopulateForeignOccupantRows(WordprocessingDocument document, IReadOnlyList<ResidenceReportOccupantDto> occupants)
        {
            if (document.MainDocumentPart == null || occupants.Count <= 1)
            {
                return;
            }

            var table = GetTableByContentControlTag(document.MainDocumentPart, "Occupants_Table");
            if (table == null)
            {
                return;
            }

            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count == 0)
            {
                return;
            }

            var templateRow = rows.Last();
            for (var index = 1; index < occupants.Count; index++)
            {
                var rowClone = (TableRow)templateRow.CloneNode(true);
                FillOccupantRow(rowClone, occupants[index]);
                table.AppendChild(rowClone);
            }

            FillOccupantRow(templateRow, occupants[0]);
        }

        private static void FillOccupantRow(TableRow row, ResidenceReportOccupantDto occupant, bool isVietnameseTemplate = false)
        {
            var cells = row.Elements<TableCell>().ToList();
            if (cells.Count == 0)
            {
                return;
            }

            var occupantSex = ConvertSexToVietnamese(occupant.Sex);

            SetCell(cells, 0, occupant.Order.ToString("00"));
            SetCell(cells, 1, occupant.FullName ?? string.Empty);
            SetCell(cells, 2, occupant.Nationality ?? string.Empty);
            SetCell(cells, 3, occupant.PassportId ?? string.Empty);
            SetCell(cells, 4, occupant.NationalIdCardNumber ?? string.Empty);
            SetCell(cells, 5, occupantSex ?? string.Empty);  // Use converted value
            SetCell(cells, 6, occupant.Phone ?? string.Empty);
        }

        private static void SetCell(List<TableCell> cells, int index, string value)
        {
            if (index < cells.Count)
            {
                UpdateCellText(cells[index], value);
            }
        }

        private static Table? GetTableByContentControlTag(MainDocumentPart mainPart, string tag)
        {
            var sdt = mainPart.Document.Descendants<SdtBlock>()
                .FirstOrDefault(s =>
                    s.SdtProperties != null &&
                    string.Equals(s.SdtProperties.GetFirstChild<Tag>()?.Val?.Value, tag, StringComparison.Ordinal));

            if (sdt != null)
            {
                return sdt.Ancestors<Table>().FirstOrDefault()
                    ?? sdt.Descendants<Table>().FirstOrDefault();
            }

            var tagElement = mainPart.Document.Descendants<Tag>()
                .FirstOrDefault(t => string.Equals(t.Val?.Value, tag, StringComparison.Ordinal));

            return tagElement?.Ancestors<Table>().FirstOrDefault();
        }

        private static void PopulateTableRowWithString(Table table, string data)
        {
            var firstRow = table.Descendants<TableRow>().FirstOrDefault();
            if (firstRow == null)
            {
                return;
            }

            var cells = firstRow.Elements<TableCell>().ToList();
            for (var i = 0; i < cells.Count && i < data.Length; i++)
            {
                UpdateCellText(cells[i], data[i].ToString());
            }
        }

        private static void UpdateCellText(TableCell cell, string newText)
        {
            var paragraph = cell.Elements<Paragraph>().FirstOrDefault() ?? cell.AppendChild(new Paragraph());
            var run = paragraph.Elements<Run>().FirstOrDefault() ?? paragraph.AppendChild(new Run());
            var text = run.Elements<Text>().FirstOrDefault() ?? run.AppendChild(new Text());
            text.Text = newText;
            text.Space = SpaceProcessingModeValues.Preserve;
        }

        private static bool IsTwelveDigitNumber(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length == 12 && value.All(char.IsDigit);
        }

        private static string? NormalizeNationalIdToTwelveDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var digitsOnly = new string(value.Where(char.IsDigit).ToArray());
            return IsTwelveDigitNumber(digitsOnly) ? digitsOnly : null;
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
            // Get the first run to copy its formatting properties
            var firstRun = paragraph.Elements<Run>().FirstOrDefault();
            var runProperties = firstRun?.RunProperties != null
                ? (RunProperties)firstRun.RunProperties.CloneNode(true)
                : new RunProperties();

            // Remove all runs
            paragraph.RemoveAllChildren<Run>();

            // Create new run with preserved formatting
            var newRun = new Run();
            newRun.AppendChild(runProperties);
            newRun.AppendChild(new Text(text ?? string.Empty)
            {
                Space = SpaceProcessingModeValues.Preserve
            });


            paragraph.AppendChild(newRun);
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