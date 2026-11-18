using System.Security.Claims;
using GlobalSolutionCitizenTech.Api.Erros;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GlobalSolutionCitizenTech.Api.Autenticacao;

public static class AutenticacaoExtensions
{
    /// <summary>
    /// JWT Bearer + autorização por perfil. Por padrão, todo endpoint exige token
    /// (fallback policy); só o que tiver <c>[AllowAnonymous]</c> fica aberto.
    /// </summary>
    public static IServiceCollection AddAutenticacaoJwt(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ConfiguracaoJwt>(configuration.GetSection(ConfiguracaoJwt.Secao));
        services.Configure<ConfiguracaoAuth>(configuration.GetSection(ConfiguracaoAuth.Secao));
        services.AddSingleton<IEmissorToken, EmissorToken>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Lido só quando a primeira requisição chega: a ferramenta de migrations do EF sobe o host sem a chave.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<ConfiguracaoJwt>>((o, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.ChaveAssinatura(),
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = "sub",
                    ClockSkew = TimeSpan.Zero,
                    // Só o algoritmo que a API emite: barra troca de algoritmo (alg "none" ou outro) no cabeçalho.
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                };
                o.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new ErroResponse(false, CodigosErro.NaoAutenticado,
                            "Token ausente, inválido ou expirado. Obtenha um em POST /api/v1/auth/token.",
                            context.ErrorDescription, context.HttpContext.TraceIdentifier));
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        // Sem ecoar o perfil do token: quem recebe o 403 não precisa saber como a autorização é montada.
                        await context.Response.WriteAsJsonAsync(new ErroResponse(false, CodigosErro.SemPermissao,
                            "O perfil do token não tem permissão para esta operação.",
                            null, context.HttpContext.TraceIdentifier));
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    /// <summary>Falha no startup, com mensagem clara, se a chave do JWT não estiver configurada.</summary>
    public static void ValidarConfiguracaoJwt(this WebApplication app)
    {
        var jwt = app.Services.GetRequiredService<IOptions<ConfiguracaoJwt>>().Value;
        if (string.IsNullOrWhiteSpace(jwt.SecretKey) || jwt.SecretKey.Length < ConfiguracaoJwt.TamanhoMinimoChave)
            throw new InvalidOperationException(
                $"Jwt:SecretKey não configurada ou com menos de {ConfiguracaoJwt.TamanhoMinimoChave} caracteres. " +
                "Defina a variável de ambiente Jwt__SecretKey.");

        var auth = app.Services.GetRequiredService<IOptions<ConfiguracaoAuth>>().Value;
        var invalidos = auth.Clientes.Where(c => !Perfis.Todos.Contains(c.Perfil)).Select(c => c.ClientId).ToList();
        if (invalidos.Count > 0)
            throw new InvalidOperationException(
                $"Auth:Clientes com perfil inválido: {string.Join(", ", invalidos)}. Use {string.Join(", ", Perfis.Todos)}.");
    }
}
