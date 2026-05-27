using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Short_termApartmentAPI.Middlewares
{
    public class ApiExceptionMiddleware(
        RequestDelegate next,
        ILogger<ApiExceptionMiddleware> logger,
        IHostEnvironment environment
    )
    {
        private readonly RequestDelegate _next = next;
        private readonly ILogger<ApiExceptionMiddleware> _logger = logger;
        private readonly IHostEnvironment _environment = environment;

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (DbUpdateException ex) when (IsDuplicateDatabaseException(ex))
            {
                var traceId = context.TraceIdentifier;
                var message = GetDuplicateDatabaseMessage(ex);

                _logger.LogWarning(ex, "Duplicate database record. TraceId: {TraceId}", traceId);

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = StatusCodes.Status409Conflict;

                var response = new ApiResponse<string>(message)
                {
                    Success = false
                };

                var json = JsonSerializer.Serialize(response);
                await context.Response.WriteAsync(json);
            }
            catch (Exception ex)
            {
                var traceId = context.TraceIdentifier;
                _logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}", traceId);
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                var errorData = new ErrorDetailsDto
                {
                    TraceId = traceId,
                    ExceptionType = ex.GetType().Name,
                    Path = context.Request.Path,
                    TimestampUtc = DateTime.UtcNow,
                    StackTrace = _environment.IsDevelopment() ? ex.StackTrace : null
                };

                var response = new ApiResponse<ErrorDetailsDto>(errorData, "An unexpected error occurred.")
                {
                    Success = false
                };
                var json = JsonSerializer.Serialize(response);
                await context.Response.WriteAsync(json);
            }
        }

        private static bool IsDuplicateDatabaseException(DbUpdateException ex)
        {
            if (ex.InnerException is MySqlException mysqlEx && mysqlEx.Number == 1062)
            {
                return true;
            }

            var message = ex.InnerException?.Message ?? ex.Message;
            return message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                || message.Contains("unique", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetDuplicateDatabaseMessage(DbUpdateException ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return string.IsNullOrWhiteSpace(message)
                ? "A duplicate record already exists."
                : message;
        }
    }

    public sealed class ErrorDetailsDto
    {
        public string? TraceId { get; set; }
        public string? ExceptionType { get; set; }
        public string? Path { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string? StackTrace { get; set; }
    }

    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }

        public ApiResponse() { }

        public ApiResponse(T? data, string? message = null)
        {
            Success = true;
            Data = data;
            Message = message;
        }

        public ApiResponse(string message)
        {
            Success = false;
            Message = message;
        }

    }
}
