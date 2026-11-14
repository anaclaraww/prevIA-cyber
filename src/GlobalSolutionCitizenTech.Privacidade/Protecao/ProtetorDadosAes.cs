using System.Security.Cryptography;
using System.Text;
using GlobalSolutionCitizenTech.Application.Privacidade;
using Microsoft.Extensions.Options;

namespace GlobalSolutionCitizenTech.Privacidade.Protecao;

/// <summary>
/// Cifra AES-256-GCM (telefone, condição de saúde) e hash HMAC-SHA256 (CPF, para achar duplicidade sem
/// guardar o número).
/// </summary>
/// <remarks>
/// Formato cifrado: <c>v1:</c> + Base64(nonce 12 | tag 16 | texto cifrado). O prefixo de versão permite
/// trocar de algoritmo ou de chave sem perder o que já está gravado.
/// GCM autentica: um valor adulterado no banco falha ao decifrar em vez de virar lixo silencioso.
/// </remarks>
public sealed class ProtetorDadosAes : IProtetorDados
{
    private const string Versao = "v1:";
    private const int TamanhoNonce = 12;
    private const int TamanhoTag = 16;

    private readonly byte[] _chave;
    private readonly byte[] _chaveHash;

    public ProtetorDadosAes(IOptions<ConfiguracaoPrivacidade> opcoes)
    {
        var c = opcoes.Value;
        _chave = LerChave(c.ChaveCriptografia, "Privacidade:ChaveCriptografia", tamanhoExato: 32);
        _chaveHash = LerChave(c.ChaveHash, "Privacidade:ChaveHash", tamanhoExato: null);
    }

    public string Cifrar(string texto)
    {
        var claro = Encoding.UTF8.GetBytes(texto);
        var saida = new byte[TamanhoNonce + TamanhoTag + claro.Length];
        var nonce = saida.AsSpan(0, TamanhoNonce);
        var tag = saida.AsSpan(TamanhoNonce, TamanhoTag);
        var cifrado = saida.AsSpan(TamanhoNonce + TamanhoTag);

        RandomNumberGenerator.Fill(nonce);
        using var aes = new AesGcm(_chave, TamanhoTag);
        aes.Encrypt(nonce, claro, cifrado, tag);
        return Versao + Convert.ToBase64String(saida);
    }

    public string Decifrar(string valor)
    {
        if (!valor.StartsWith(Versao, StringComparison.Ordinal))
            throw new CryptographicException("Formato de dado cifrado desconhecido.");

        var bytes = Convert.FromBase64String(valor[Versao.Length..]);
        if (bytes.Length < TamanhoNonce + TamanhoTag)
            throw new CryptographicException("Dado cifrado truncado.");

        var claro = new byte[bytes.Length - TamanhoNonce - TamanhoTag];
        using var aes = new AesGcm(_chave, TamanhoTag);
        aes.Decrypt(bytes.AsSpan(0, TamanhoNonce), bytes.AsSpan(TamanhoNonce + TamanhoTag),
            bytes.AsSpan(TamanhoNonce, TamanhoTag), claro);
        return Encoding.UTF8.GetString(claro);
    }

    /// <summary>HMAC (e não SHA puro): só há 10⁹ CPFs possíveis, um hash sem chave sai por força bruta em minutos.</summary>
    public string Hash(string texto) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_chaveHash, Encoding.UTF8.GetBytes(texto)));

    private static byte[] LerChave(string base64, string nome, int? tamanhoExato)
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException($"{nome} não configurada. Gere com: openssl rand -base64 32");

        byte[] chave;
        try { chave = Convert.FromBase64String(base64); }
        catch (FormatException) { throw new InvalidOperationException($"{nome} não está em Base64."); }

        if (tamanhoExato is { } t && chave.Length != t)
            throw new InvalidOperationException($"{nome} deve ter {t} bytes (tem {chave.Length}).");
        if (chave.Length < 32)
            throw new InvalidOperationException($"{nome} deve ter pelo menos 32 bytes.");
        return chave;
    }
}
