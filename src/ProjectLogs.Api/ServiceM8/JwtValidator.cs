using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ProjectLogs.Api.ServiceM8;

public static class JwtValidator
{
    /// <summary>
    /// Validates a ServiceM8 JWT (HS256 signed with the add-on app secret)
    /// and deserializes the payload into a <see cref="ServiceM8Event"/>.
    /// </summary>
    public static async Task<ServiceM8Event?> ValidateAndDecodeAsync(string jwt, string appSecret)
    {
        var handler = new JsonWebTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appSecret));

        var result = await handler.ValidateTokenAsync(jwt, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false, // SM8 tokens don't carry standard exp/nbf
            IssuerSigningKey = key,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        });

        if (!result.IsValid)
            return null;

        // SM8 JWT payloads use nested objects (auth, eventArgs) rather than flat
        // claims, so we decode the raw payload JSON rather than reading claims.
        var token = handler.ReadJsonWebToken(jwt);
        var payloadJson = Base64UrlEncoder.Decode(token.EncodedPayload);
        return JsonSerializer.Deserialize<ServiceM8Event>(payloadJson);
    }
}
