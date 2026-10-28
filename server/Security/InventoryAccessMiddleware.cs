using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Ggd.Api.Data;

namespace Ggd.Api.Security;

public sealed class InventoryAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, InventoryDbContext db, IAntiforgery antiforgery)
    {
        if (!context.Request.Path.StartsWithSegments("/api")) { await next(context); return; }
        context.Response.Headers.CacheControl = "no-cache, no-store";
        context.Response.Headers.Pragma = "no-cache";
        var accountRoute = context.Request.Path.StartsWithSegments("/api/auth");
        if (!accountRoute && context.User.Identity?.IsAuthenticated != true)
        { context.Response.StatusCode = 401; return; }
        var writes = !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method);
        if (writes && accountRoute && !context.Request.IsHttps)
        { await Reject(context, 400, "Use the HTTPS address to manage your account."); return; }
        if (writes)
        {
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            { await Reject(context, 400, "Your security token expired. Reload the app and try again."); return; }
        }
        if (accountRoute) { await next(context); return; }

        var workspaceValues = new List<Guid>();
        if (context.Request.Query.TryGetValue("workspaceId", out var queryValue))
        {
            if (!Guid.TryParse(queryValue, out var id)) { await Reject(context, 400, "A valid household is required."); return; }
            workspaceValues.Add(id);
        }
        if (writes && context.Request.HasJsonContentType())
        {
            context.Request.EnableBuffering(bufferThreshold: 65536, bufferLimit: 16 * 1024 * 1024);
            try
            {
                using var body = await JsonDocument.ParseAsync(context.Request.Body);
                if (body.RootElement.ValueKind != JsonValueKind.Object)
                { await Reject(context, 400, "Invalid request."); return; }
                var found = false;
                // Match the JSON serializer's case-insensitive binding, including duplicate keys.
                foreach (var property in body.RootElement.EnumerateObject().Where(p => p.Name.Equals("workspaceId", StringComparison.OrdinalIgnoreCase)))
                {
                    if (property.Value.ValueKind != JsonValueKind.String || !property.Value.TryGetGuid(out var id))
                    { await Reject(context, 400, "A valid household is required."); return; }
                    found = true; workspaceValues.Add(id);
                }
                if (!found) { await Reject(context, 400, "A valid household is required."); return; }
            }
            catch (JsonException) { await Reject(context, 400, "Invalid request."); return; }
            finally { context.Request.Body.Position = 0; }
        }
        else if (writes && context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync();
            if (!Guid.TryParse(form["workspaceId"], out var id)) { await Reject(context, 400, "A valid household is required."); return; }
            workspaceValues.Add(id);
        }
        if (workspaceValues.Count == 0 || workspaceValues.Distinct().Count() != 1)
        { await Reject(context, 403, "Household access denied."); return; }
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !await db.WorkspaceMembers.AsNoTracking().AnyAsync(x => x.UserId == userId && x.WorkspaceId == workspaceValues[0]))
        { await Reject(context, 403, "Household access denied."); return; }
        await next(context);
    }

    private static async Task Reject(HttpContext context, int status, string message)
    { context.Response.StatusCode = status; await context.Response.WriteAsync(message); }
}
