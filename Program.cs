using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapPost("/extract", async (RequestData req) =>
{
    string texto = req?.ResumoConversa ?? string.Empty;

    string Extract(string pattern, int group = 1)
    {
        var m = Regex.Match(texto, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return m.Success ? m.Groups[group].Value.Trim() : string.Empty;
    }

    string onlyDigits(string s) => Regex.Replace(s ?? string.Empty, @"\D", "");

    // Patterns
    var cpfRaw = Extract(@"\bCPF[:\s]*([\d\.\-]{11,14})\b");
    var telefoneRaw = Extract(@"\bTELEFONE[:\s]*(\(?\d{2}\)?\s*\d{4,5}-?\d{4})\b");
    var nomeRaw = Extract(@"\bNOME[:\s]*([A-Za-zÀ-ÖØ-öø-ÿ\.\'\-\s]+?)(?=\s{2,}|CPF:|TELEFONE:|ENDEREÇO:|NÚMERO DA NOTA|NÚMERO DE SÉRIE|$)");
    var enderecoRaw = Extract(@"\bENDEREÇO[:\s]*(.+?)(?=\s{2,}|CEP:|NÚMERO DA NOTA|NÚMERO DE SÉRIE|$)");
    var cepRaw = Extract(@"\bCEP[:\s]*([\d\-\s]{7,9})\b");
    if (string.IsNullOrEmpty(cepRaw))
    {
        // tentar achar um CEP de 8 dígitos no texto
        var m = Regex.Match(texto, @"\b(\d{8})\b");
        if (m.Success) cepRaw = m.Groups[1].Value;
    }
    var cepDigits = onlyDigits(cepRaw);
    var numeroNota = Extract(@"NÚMERO\s*(?:DA\s*)?NOTA[:\s\-]*([0-9]+)");
    var dataCompra = Extract(@"(\d{2}\/\d{2}\/\d{4})"); // pegar primeira data
    var numeroSerie = Extract(@"NÚMERO\s*DE\s*SÉRIE[:\s]*([A-Za-z0-9\-]{6,})");

    // Normalizações
    var cpf = onlyDigits(cpfRaw);
    var telefone = onlyDigits(telefoneRaw);
    var cep = cepDigits;

    // ViaCEP lookup (opcional, se tiver CEP)
    ViaCepResponse? viaCep = null;
    if (!string.IsNullOrEmpty(cep) && cep.Length == 8)
    {
        try
        {
            using var http = new HttpClient();
            var json = await http.GetStringAsync($"https://viacep.com.br/ws/{cep}/json/");
            viaCep = JsonSerializer.Deserialize<ViaCepResponse>(json);
        }
        catch { /* não falhar a API se ViaCEP indisponível */ }
    }

    var result = new
    {
        CPF = cpf,
        Nome = nomeRaw,
        Telefone = telefone,
        Endereco = enderecoRaw,
        CEP = cep,
        ViaCep = viaCep,
        NumeroNota = numeroNota,
        DataCompra = dataCompra,
        NumeroSerie = numeroSerie
    };

    return Results.Json(result);
});

app.Run();

public record RequestData([property: JsonPropertyName("resumoConversa")] string ResumoConversa);

public record ViaCepResponse
{
    [JsonPropertyName("cep")] public string? Cep { get; init; }
    [JsonPropertyName("logradouro")] public string? Logradouro { get; init; }
    [JsonPropertyName("complemento")] public string? Complemento { get; init; }
    [JsonPropertyName("bairro")] public string? Bairro { get; init; }
    [JsonPropertyName("localidade")] public string? Localidade { get; init; }
    [JsonPropertyName("uf")] public string? Uf { get; init; }
    [JsonPropertyName("erro")] public bool? Erro { get; init; }
}
