using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using BLL.Services.Interfaces;
using Common.DTOs;
using Common.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BLL.Services.Implements;

public sealed class FptPassportRecognitionService : IFptPassportRecognitionService
{
    private readonly HttpClient _httpClient;
    private readonly FptIdRecognitionOptions _options;

    public FptPassportRecognitionService(HttpClient httpClient, IOptions<FptIdRecognitionOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<FptIdRecognitionResult> RecognizeAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Passport image is required.");
        }

        using var stream = file.OpenReadStream();
        using var multipart = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        if (!string.IsNullOrWhiteSpace(file.ContentType))
        {
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        }

        multipart.Add(fileContent, "image", file.FileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.PassportEndpoint)
        {
            Content = multipart
        };
        request.Headers.Add("api-key", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new ArgumentException(ExtractProviderFailureMessage(raw, "Passport recognition service is unavailable. Please try again."));
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(raw);
        }
        catch (JsonException)
        {
            throw new ArgumentException("Passport recognition returned an invalid response. Please upload a clearer image.");
        }

        using (doc)
        {
            var root = doc.RootElement;
            var errorCode = TryGetInt(root, "errorCode") ?? -1;
            var errorMessage = TryGetString(root, "errorMessage") ?? string.Empty;

            if (errorCode != 0)
            {
                throw new ArgumentException(ExtractProviderFailureMessage(errorMessage, MapProviderError(errorCode, errorMessage)));
            }

            if (!root.TryGetProperty("data", out var dataElement) ||
                dataElement.ValueKind != JsonValueKind.Array ||
                dataElement.GetArrayLength() == 0)
            {
                throw new ArgumentException("Unable to detect passport information. Please upload a clearer image.");
            }

            var data = dataElement[0];
            var result = new FptIdRecognitionResult
            {
                Success = true,
                ErrorCode = 0,
                ErrorMessage = string.Empty,
                PassportNumber = TryGetString(data, "passport_number") ?? TryGetString(data, "id_number"),
                FullName = TryGetString(data, "name"),
                DateOfBirth = TryGetString(data, "dob"),
                PlaceOfBirth = TryGetString(data, "pob"),
                Sex = TryGetString(data, "sex"),
                IdNumber = TryGetString(data, "id_number"),
                IssueDate = TryGetString(data, "doi"),
                ExpiryDate = TryGetString(data, "doe")
            };

            foreach (var property in data.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array)
                {
                    continue;
                }

                var asString = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    _ => property.Value.GetRawText()
                };

                if (!string.IsNullOrWhiteSpace(asString))
                {
                    result.ExtractedFields[property.Name] = asString;
                }

                if (property.Name.EndsWith("_prob", StringComparison.OrdinalIgnoreCase) &&
                    TryParseProbability(asString, out var prob))
                {
                    result.FieldConfidences[property.Name] = prob;
                }
            }

            var coreConfidences = new List<double>();
            AddCoreConfidence(result.FieldConfidences, "passport_number_prob", coreConfidences);
            AddCoreConfidence(result.FieldConfidences, "name_prob", coreConfidences);
            AddCoreConfidence(result.FieldConfidences, "dob_prob", coreConfidences);

            if (coreConfidences.Count == 0 && result.FieldConfidences.Count > 0)
            {
                coreConfidences.AddRange(result.FieldConfidences.Values);
            }

            result.OverallConfidence = coreConfidences.Count > 0 ? coreConfidences.Average() : 0;
            return result;
        }
    }

    private static void AddCoreConfidence(IReadOnlyDictionary<string, double> source, string key, List<double> destination)
    {
        if (source.TryGetValue(key, out var value))
        {
            destination.Add(value);
        }
    }

    private static bool TryParseProbability(string? value, out double probability)
    {
        probability = 0;
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        if (parsed < 0)
        {
            return false;
        }

        probability = parsed > 1 ? parsed / 100d : parsed;
        return true;
    }

    private static int? TryGetInt(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var el))
        {
            return null;
        }

        return el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetInt32(out var value) ? value : null,
            JsonValueKind.String => int.TryParse(el.GetString(), out var value) ? value : null,
            _ => null
        };
    }

    private static string? TryGetString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var el))
        {
            return null;
        }

        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static string MapProviderError(int errorCode, string providerMessage)
    {
        return errorCode switch
        {
            1 => "Invalid passport OCR request. Please try uploading the image again.",
            2 => "Cannot crop the passport. Please ensure all 4 corners are visible.",
            3 => "Passport not detected or image quality is too low.",
            5 => "Passport OCR request is missing image URL.",
            6 => "Passport OCR could not open the provided image URL.",
            7 => "Uploaded file is not a valid image.",
            8 => "Image data is corrupted or unsupported.",
            _ => string.IsNullOrWhiteSpace(providerMessage)
                ? "Passport recognition failed. Please upload a clearer image."
                : providerMessage
        };
    }

    private static string ExtractProviderFailureMessage(string? rawResponse, string fallbackMessage)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return fallbackMessage;
        }

        try
        {
            using var document = JsonDocument.Parse(rawResponse);
            var root = document.RootElement;
            var providerMessage = TryGetString(root, "errorMessage");
            if (!string.IsNullOrWhiteSpace(providerMessage))
            {
                return providerMessage;
            }
        }
        catch (JsonException)
        {
            // Fall back to raw body when provider returns non-JSON text.
        }

        return string.IsNullOrWhiteSpace(rawResponse) ? fallbackMessage : rawResponse.Trim();
    }
}