using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Short_termApartmentAPI.Swagger;

/// <summary>
/// Operation filter to annotate endpoints secured by [Authorize] in Swagger UI.
/// Adds 401/403 responses, a bearer security requirement and appends required roles to the operation description.
/// </summary>
public class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var actionAttrs = context.MethodInfo?.GetCustomAttributes(true) ?? new object[0];
        var controllerAttrs = context.MethodInfo?.DeclaringType?.GetCustomAttributes(true) ?? new object[0];

        var authorizeAttributes = actionAttrs.OfType<AuthorizeAttribute>().Concat(controllerAttrs.OfType<AuthorizeAttribute>()).ToList();

        if (!authorizeAttributes.Any())
        {
            return;
        }

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized" });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden" });

        var bearerScheme = new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        };

        operation.Security ??= new System.Collections.Generic.List<OpenApiSecurityRequirement>();
        var requirement = new OpenApiSecurityRequirement
        {
            [bearerScheme] = new string[] { }
        };
        operation.Security.Add(requirement);

        var roles = authorizeAttributes.Select(a => a.Roles).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToList();
        if (roles.Any())
        {
            var rolesText = string.Join(", ", roles);
            var note = $"\n\nRequired roles: {rolesText}";
            operation.Description = (operation.Description ?? string.Empty) + note;
        }
    }
}
