using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GlobalSolutionCitizenTech.Api.Autenticacao;

/// <summary>Seção <c>Jwt</c> do appsettings.</summary>
public class ConfiguracaoJwt
{
    public const string Secao = "Jwt";
    public const int TamanhoMinimoChave = 32;

    public string Issuer { get; set; } = "GlobalSolutionCitizenTech";
    public string Audience { get; set; } = "citizentech-api";
    public int ExpiracaoMinutos { get; set; } = 60;

    /// <summary>Chave HMAC-SHA256. Nunca vai para o repositório em produção.</summary>
    public string SecretKey { get; set; } = "";

    public SymmetricSecurityKey ChaveAssinatura() => new(Encoding.UTF8.GetBytes(SecretKey));
}

/// <summary>Um consumidor da API (seção <c>Auth:Clientes</c>).</summary>
public class ClienteApi
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string Perfil { get; set; } = "";
}

public class ConfiguracaoAuth
{
    public const string Secao = "Auth";
    public List<ClienteApi> Clientes { get; set; } = [];
}

public record TokenRequest(string ClientId, string ClientSecret);

/// <param name="AccessToken">JWT para o header <c>Authorization: Bearer ...</c>.</param>
/// <param name="TokenType">Sempre <c>Bearer</c>.</param>
/// <param name="ExpiresIn">Validade em segundos.</param>
/// <param name="Perfil">Perfil do cliente: Sensor, Operador ou Admin.</param>
public record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, string Perfil);

/// <summary>Credenciais inválidas → HTTP 401.</summary>
public class CredenciaisInvalidasException() : Exception("Credenciais inválidas.");

public interface IEmissorToken
{
    TokenResponse Emitir(TokenRequest request);
}

/// <summary>Valida as credenciais do cliente contra a configuração e emite um JWT com o perfil dele.</summary>
public class EmissorToken(IOptions<ConfiguracaoJwt> jwt, IOptions<ConfiguracaoAuth> auth, TimeProvider relogio) : IEmissorToken
{
    public TokenResponse Emitir(TokenRequest request)
    {
        var cliente = auth.Value.Clientes.FirstOrDefault(c => c.ClientId == request.ClientId);

        // Compara mesmo quando o cliente não existe, em tempo constante, para não vazar quais ids são válidos.
        var segredoEsperado = cliente?.ClientSecret ?? Guid.NewGuid().ToString();
        if (!SegredosIguais(segredoEsperado, request.ClientSecret ?? "") || cliente is null)
            throw new CredenciaisInvalidasException();

        var config = jwt.Value;
        var agora = relogio.GetUtcNow();
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = config.Issuer,
            Audience = config.Audience,
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = agora.AddMinutes(config.ExpiracaoMinutos).UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, cliente.ClientId),
                new Claim(ClaimTypes.Role, cliente.Perfil),
            ]),
            SigningCredentials = new SigningCredentials(config.ChaveAssinatura(), SecurityAlgorithms.HmacSha256),
        });

        return new TokenResponse(token, "Bearer", config.ExpiracaoMinutos * 60, cliente.Perfil);
    }

    private static bool SegredosIguais(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(a)), SHA256.HashData(Encoding.UTF8.GetBytes(b)));
}
