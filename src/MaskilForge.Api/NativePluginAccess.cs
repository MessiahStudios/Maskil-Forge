using System.Net;

namespace MaskilForge.Api;

public static class NativePluginAccess
{
    public static bool IsLocalRequest(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null || !IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address)) return false;
        var host = context.Request.Host.Host;
        if (!host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
            !(IPAddress.TryParse(host.Trim('[', ']'), out var hostIp) && IPAddress.IsLoopback(hostIp))) return false;
        // Same-origin browser calls and the existing localhost Vite development origin only.
        var origin = context.Request.Headers.Origin.ToString();
        if (origin.Length == 0) return true; // Native local clients, such as a command-line smoke test.
        return origin == $"{context.Request.Scheme}://{context.Request.Host}" ||
            origin is "http://localhost:5173" or "http://127.0.0.1:5173";
    }
}
