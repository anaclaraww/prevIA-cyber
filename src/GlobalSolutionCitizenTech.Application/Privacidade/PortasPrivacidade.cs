using GlobalSolutionCitizenTech.Domain.Entidades;

namespace GlobalSolutionCitizenTech.Application.Privacidade;

/// <summary>Proteção de dados pessoais em repouso (implementação: AES-256-GCM + HMAC-SHA256).</summary>
public interface IProtetorDados
{
    /// <summary>Cifra o texto. Cada chamada gera um nonce novo: o mesmo valor cifrado duas vezes dá textos diferentes.</summary>
    string Cifrar(string textoClaro);

    string Decifrar(string textoCifrado);

    /// <summary>Hash com chave (HMAC): determinístico, para buscar duplicidade sem guardar o valor.</summary>
    string Hash(string valor);
}

/// <summary>Resultado da tradução de um evento técnico para o cidadão.</summary>
/// <param name="Mensagem">Até 2 frases em linguagem simples.</param>
/// <param name="TraduzidoPor">Modelo de IA usado, ou <c>offline</c>.</param>
public record TraducaoRisco(string Mensagem, string TraduzidoPor);

/// <summary>
/// Traduz um evento técnico de segurança para linguagem de cidadão leigo
/// (implementações: Claude via API da Anthropic, e textos offline de reserva).
/// </summary>
public interface ITradutorRisco
{
    Task<TraducaoRisco> TraduzirAsync(EventoSeguranca evento, CancellationToken ct);
}
