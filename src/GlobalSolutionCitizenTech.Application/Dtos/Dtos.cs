using System.ComponentModel.DataAnnotations;
using GlobalSolutionCitizenTech.Domain.Entidades;
using GlobalSolutionCitizenTech.Domain.Enums;

namespace GlobalSolutionCitizenTech.Application.Dtos;

// ---------------------------------------------------------------- Comum

public record PaginaResponse<T>(IReadOnlyList<T> Itens, int Pagina, int TamanhoPagina, int Total);

// ---------------------------------------------------------------- Bairro

public record BairroRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Nome,
    [Required, StringLength(100, MinimumLength = 2)] string Cidade,
    [Required, RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "UF deve ter 2 letras.")] string Uf,
    [Range(-90, 90)] double Latitude,
    [Range(-180, 180)] double Longitude,
    [Range(0.1, 1000, ErrorMessage = "O limiar de chuva deve estar entre 0,1 e 1000 mm.")] decimal LimiarChuvaMm);

public record BairroResponse(Guid Id, string Nome, string Cidade, string Uf, double Latitude, double Longitude,
    decimal LimiarChuvaMm, DateTimeOffset CriadoEm);

// ---------------------------------------------------------------- Quadra

public record CriarQuadraRequest(
    [Required, StringLength(120, MinimumLength = 3)] string Nome,
    [Required] Guid? BairroId,
    [Required, RegularExpression(@"^\d{5}-?\d{3}$", ErrorMessage = "CEP deve ter 8 dígitos (ex.: 01310-100).")] string Cep,
    [StringLength(10)] string? Numero,
    [Range(-90, 90)] double Latitude,
    [Range(-180, 180)] double Longitude,
    [Range(1, 100000, ErrorMessage = "A capacidade deve estar entre 1 e 100000 m³.")] decimal CapacidadeM3);

public record AtualizarQuadraRequest(
    [Required, StringLength(120, MinimumLength = 3)] string Nome,
    [StringLength(10)] string? Numero,
    [Range(1, 100000)] decimal CapacidadeM3);

public record AlterarStatusQuadraRequest(
    [Required, EnumDataType(typeof(StatusQuadra))] StatusQuadra? Status);

public record QuadraResponse(Guid Id, string Nome, Guid BairroId, string Cep, string Logradouro, string? Numero,
    double Latitude, double Longitude, decimal CapacidadeM3, decimal NivelAtualPercentual, decimal VolumeRetidoM3,
    StatusQuadra Status, IReadOnlyList<StatusQuadra> ProximosStatusPermitidos, DateTimeOffset CriadoEm);

// ---------------------------------------------------------------- Sensor e leitura

public record CriarSensorRequest(
    [Required, StringLength(30, MinimumLength = 3)] string Codigo,
    [Required, EnumDataType(typeof(TipoSensor))] TipoSensor? Tipo,
    [Required] Guid? BairroId,
    Guid? QuadraId,
    [Range(0.1, 100)] decimal? Limiar);

public record SensorResponse(Guid Id, string Codigo, TipoSensor Tipo, string Unidade, Guid BairroId, Guid? QuadraId,
    decimal? Limiar, bool Ativo, decimal? UltimoValor, DateTimeOffset? UltimaLeituraEm);

public record AlterarAtivacaoSensorRequest([Required] bool? Ativo);

public record RegistrarLeituraRequest(
    [Range(0, 10000, ErrorMessage = "O valor da leitura deve estar entre 0 e 10000.")] decimal Valor);

public record LeituraResponse(Guid Id, Guid SensorId, decimal Valor, string Unidade, DateTimeOffset RegistradaEm);

/// <summary>Leitura registrada + o que o sistema fez em reação a ela.</summary>
public record LeituraProcessadaResponse(LeituraResponse Leitura, IReadOnlyList<string> AcoesExecutadas);

// ---------------------------------------------------------------- Robô

public record CriarRoboRequest(
    [Required, StringLength(30, MinimumLength = 3)] string Codigo,
    [Required, EnumDataType(typeof(TipoRobo))] TipoRobo? Tipo,
    [Required] Guid? BairroBaseId,
    [Range(0, 100)] int Bateria = 100);

public record RoboResponse(Guid Id, string Codigo, TipoRobo Tipo, Guid BairroBaseId, int Bateria, StatusRobo Status);

// ---------------------------------------------------------------- Missão

public record CriarMissaoRequest(
    [Required, EnumDataType(typeof(TipoMissao))] TipoMissao? Tipo,
    [Required] Guid? BairroId,
    Guid? SensorId,
    [EnumDataType(typeof(PrioridadeMissao))] PrioridadeMissao Prioridade = PrioridadeMissao.Normal,
    [StringLength(300)] string? Descricao = null);

public record MissaoResponse(Guid Id, TipoMissao Tipo, Guid BairroId, Guid? SensorId, Guid? RoboId, string? RoboCodigo,
    PrioridadeMissao Prioridade, StatusMissao Status, string Descricao, DateTimeOffset CriadoEm,
    DateTimeOffset? IniciadaEm, DateTimeOffset? ConcluidaEm);

public record MissaoConcluidaResponse(MissaoResponse Missao, MissaoResponse? ProximaMissaoDoRobo);

// ---------------------------------------------------------------- Alerta e risco

public record AlertaResponse(Guid Id, Guid BairroId, Guid? QuadraId, NivelRisco Nivel, string Mensagem, bool Ativo,
    DateTimeOffset CriadoEm, DateTimeOffset? EncerradoEm);

public record AvaliacaoRiscoResponse(Guid Id, Guid BairroId, decimal ChuvaPrevistaMm, int HorasConsideradas,
    decimal LimiarChuvaMm, int EntupimentosAbertos, NivelRisco Nivel, DateTimeOffset AvaliadaEm);

public record ResultadoAvaliacaoRiscoResponse(AvaliacaoRiscoResponse Avaliacao, IReadOnlyList<string> AcoesExecutadas);

// ---------------------------------------------------------------- Painel

public record PainelBairroResponse(
    BairroResponse Bairro,
    AvaliacaoRiscoResponse? UltimaAvaliacao,
    IReadOnlyList<QuadraResponse> Quadras,
    IReadOnlyList<RoboResponse> Robos,
    IReadOnlyList<MissaoResponse> MissoesAbertas,
    IReadOnlyList<AlertaResponse> AlertasAtivos,
    decimal VolumeTotalRetidoM3);

// ---------------------------------------------------------------- Simulador

public record LeituraSimuladaResponse(string Sensor, TipoSensor Tipo, decimal Valor, string Unidade);

public record ResultadoCicloSimulacaoResponse(int Ciclo, DateTimeOffset ExecutadoEm,
    GlobalSolutionCitizenTech.Application.Simulacao.CenarioSimulacao Cenario,
    IReadOnlyList<LeituraSimuladaResponse> Leituras, IReadOnlyList<string> AcoesExecutadas);

/// <param name="Habilitado">Se o ciclo automático em background está ligado.</param>
/// <param name="IntervaloSegundos">Intervalo entre ciclos automáticos.</param>
/// <param name="Cenario">Normal ou ChuvaForte.</param>
/// <param name="CiclosExecutados">Ciclos executados desde que a API subiu.</param>
/// <param name="ChuvaPrevistaSimuladaMm">Previsão fixa no lugar do Open-Meteo, se configurada.</param>
public record StatusSimuladorResponse(bool Habilitado, int IntervaloSegundos,
    GlobalSolutionCitizenTech.Application.Simulacao.CenarioSimulacao Cenario, int CiclosExecutados,
    decimal? ChuvaPrevistaSimuladaMm);

// ---------------------------------------------------------------- Mapeamentos

public static class Mapeamentos
{
    public static BairroResponse ToResponse(this Bairro b) =>
        new(b.Id, b.Nome, b.Cidade, b.Uf, b.Latitude, b.Longitude, b.LimiarChuvaMm, b.CriadoEm);

    public static QuadraResponse ToResponse(this Quadra q) =>
        new(q.Id, q.Nome, q.BairroId, q.Cep, q.Logradouro, q.Numero, q.Latitude, q.Longitude, q.CapacidadeM3,
            q.NivelAtualPercentual, q.VolumeRetidoM3, q.Status,
            Enum.GetValues<StatusQuadra>().Where(q.PodeTransicionarPara).ToList(), q.CriadoEm);

    public static SensorResponse ToResponse(this Sensor s) =>
        new(s.Id, s.Codigo, s.Tipo, s.Unidade, s.BairroId, s.QuadraId, s.Limiar, s.Ativo, s.UltimoValor, s.UltimaLeituraEm);

    public static LeituraResponse ToResponse(this Leitura l, TipoSensor tipo) =>
        new(l.Id, l.SensorId, l.Valor, Sensor.UnidadeDe(tipo), l.RegistradaEm);

    public static RoboResponse ToResponse(this Robo r) =>
        new(r.Id, r.Codigo, r.Tipo, r.BairroBaseId, r.Bateria, r.Status);

    public static MissaoResponse ToResponse(this Missao m) =>
        new(m.Id, m.Tipo, m.BairroId, m.SensorId, m.RoboId, m.Robo?.Codigo, m.Prioridade, m.Status, m.Descricao,
            m.CriadoEm, m.IniciadaEm, m.ConcluidaEm);

    public static AlertaResponse ToResponse(this Alerta a) =>
        new(a.Id, a.BairroId, a.QuadraId, a.Nivel, a.Mensagem, a.Ativo, a.CriadoEm, a.EncerradoEm);

    public static AvaliacaoRiscoResponse ToResponse(this AvaliacaoRisco a) =>
        new(a.Id, a.BairroId, a.ChuvaPrevistaMm, a.HorasConsideradas, a.LimiarChuvaMm, a.EntupimentosAbertos,
            a.Nivel, a.CriadoEm);
}
