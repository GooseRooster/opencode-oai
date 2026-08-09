using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OpencodeOai.Options;

namespace OpencodeOai.Auth;

public sealed class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";

    private readonly IOptionsMonitor<BridgeOptions> _bridge;

    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptionsMonitor<BridgeOptions> bridge)
        : base(options, logger, encoder)
    {
        _bridge = bridge;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configured = _bridge.CurrentValue.ApiKey;

        // Auth disabled when no key configured (parity with npm bridge).
        if (string.IsNullOrEmpty(configured))
        {
            return Task.FromResult(AuthenticateResult.Success(BuildTicket("anonymous")));
        }

        if (!Request.Headers.TryGetValue("Authorization", out var header) || header.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header"));
        }

        var raw = header.ToString();
        var token = raw.StartsWith("Bearer ", StringComparison.Ordinal) ? raw[7..] : raw;

        var provided  = Encoding.UTF8.GetBytes(token);
        var expected  = Encoding.UTF8.GetBytes(configured);

        if (provided.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(provided, expected))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        return Task.FromResult(AuthenticateResult.Success(BuildTicket("api-key")));
    }

    private AuthenticationTicket BuildTicket(string name)
    {
        var identity  = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, name) }, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationTicket(principal, Scheme.Name);
    }
}
