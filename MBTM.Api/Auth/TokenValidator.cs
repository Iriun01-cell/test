using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace MBTM.Api.Auth;

/// <summary>認証済みユーザー(トークンの oid クレーム)。</summary>
public sealed record AuthenticatedUser(string UserId, string? Name);

/// <summary>認証結果。User が null の場合、StatusCode(401 / 403)を返す。</summary>
public sealed record AuthResult(AuthenticatedUser? User, int StatusCode);

/// <summary>
/// Microsoft Entra External ID が発行したアクセストークンを検証する。
/// 発行元・宛先(audience)・有効期限・署名・スコープ(access_as_user)を確認する。
/// </summary>
public sealed class TokenValidator
{
    private const string RequiredScope = "access_as_user";

    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;
    private readonly string[] _validAudiences;
    private readonly ILogger<TokenValidator> _logger;
    private readonly JwtSecurityTokenHandler _handler = new() { MapInboundClaims = false };

    public TokenValidator(ILogger<TokenValidator> logger)
    {
        _logger = logger;

        // 例: https://<サブドメイン>.ciamlogin.com/<テナントID>
        var authority = GetRequired("ENTRA_AUTHORITY").TrimEnd('/');
        var apiClientId = GetRequired("ENTRA_API_CLIENT_ID");

        // v2.0 トークンの aud はクライアントID、v1.0 の場合は api://<クライアントID> になるため両方を許可する
        _validAudiences = [apiClientId, $"api://{apiClientId}"];

        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{authority}/v2.0/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });
    }

    public async Task<AuthResult> ValidateAsync(HttpRequest req, CancellationToken ct = default)
    {
        var header = req.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return new AuthResult(null, StatusCodes.Status401Unauthorized);
        }

        var token = header["Bearer ".Length..].Trim();

        try
        {
            var config = await _configManager.GetConfigurationAsync(ct);
            var parameters = new TokenValidationParameters
            {
                ValidIssuer = config.Issuer,
                ValidAudiences = _validAudiences,
                IssuerSigningKeys = config.SigningKeys,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            };

            var principal = _handler.ValidateToken(token, parameters, out _);

            var scopes = (principal.FindFirst("scp")?.Value ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!scopes.Contains(RequiredScope))
            {
                return new AuthResult(null, StatusCodes.Status403Forbidden);
            }

            var userId = principal.FindFirst("oid")?.Value ?? principal.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return new AuthResult(null, StatusCodes.Status401Unauthorized);
            }

            return new AuthResult(new AuthenticatedUser(userId, principal.FindFirst("name")?.Value), 200);
        }
        catch (SecurityTokenException ex)
        {
            // トークン不正(期限切れ・署名不正・audience不一致など)。トークン自体はログに出さない
            _logger.LogWarning("トークン検証に失敗しました: {Reason}", ex.GetType().Name);
            return new AuthResult(null, StatusCodes.Status401Unauthorized);
        }
    }

    private static string GetRequired(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"設定 {name} が未設定です。");
}
