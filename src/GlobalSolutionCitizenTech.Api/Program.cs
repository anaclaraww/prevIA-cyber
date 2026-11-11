using System.Text.Json.Serialization;
using GlobalSolutionCitizenTech.Api.Autenticacao;
using GlobalSolutionCitizenTech.Api.Erros;
using GlobalSolutionCitizenTech.Api.Simulacao;
using GlobalSolutionCitizenTech.Application;
using GlobalSolutionCitizenTech.Infrastructure;
using GlobalSolutionCitizenTech.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o =>
    {
        // Erros de validação do [ApiController] no mesmo envelope dos demais erros.
        o.InvalidModelStateResponseFactory = context =>
        {
            var erros = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    e => e.Key,
                    e => e.Value!.Errors.Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "Valor inválido." : x.ErrorMessage).ToArray());

            return new BadRequestObjectResult(new ErroResponse(false, CodigosErro.Validacao,
                "Os dados enviados são inválidos. Verifique o payload e tente novamente.", erros,
                context.HttpContext.TraceIdentifier));
        };
    });

builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAutenticacaoJwt(builder.Configuration);
builder.Services.AddHostedService<SimuladorBackgroundService>();

builder.Services.AddHealthChecks()
    .AddCheck<BancoHealthCheck>("postgres");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GlobalSolutionCitizenTech API",
        Version = "v1",
        Description = "Backend das quadras inteligentes contra alagamentos: sensores, previsão de risco, " +
                      "robôs de drenagem, quadras-reservatório e alertas.\n\n" +
                      "**Autenticação:** obtenha um token em `POST /api/v1/auth/token`, clique em **Authorize** e cole o token."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Token JWT obtido em POST /api/v1/auth/token (cole só o token, sem a palavra Bearer)."
    });
    c.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", documento)] = []
    });

    foreach (var xml in Directory.GetFiles(AppContext.BaseDirectory, "GlobalSolutionCitizenTech.*.xml"))
        c.IncludeXmlComments(xml, includeControllerXmlComments: true);
});

var app = builder.Build();

app.ValidarConfiguracaoJwt();
await PrepararBancoAsync(app);

app.UseMiddleware<TratamentoErrosMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "GlobalSolutionCitizenTech API v1");
    c.DocumentTitle = "GlobalSolutionCitizenTech API";
    c.EnablePersistAuthorization();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription().AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

app.Run();

static async Task PrepararBancoAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var config = app.Configuration;

    if (config.GetValue("Database:AplicarMigrations", true))
        await db.Database.MigrateAsync();

    if (config.GetValue("Database:Seed", false))
        await DadosDemonstracao.PopularAsync(db);
}

/// <summary>Verifica se o PostgreSQL responde. Exposto em <c>/health</c>.</summary>
internal sealed class BancoHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        await db.Database.CanConnectAsync(ct)
            ? HealthCheckResult.Healthy("PostgreSQL acessível.")
            : HealthCheckResult.Unhealthy("PostgreSQL inacessível.");
}

public partial class Program;
