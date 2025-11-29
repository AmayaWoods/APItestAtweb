using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Text.Json.Serialization;

// -----------------------------------------------------------------------------
// DECLARAÇÕES (devem vir ANTES dos top-level statements)
// -----------------------------------------------------------------------------

public record ResumoRequest([property: JsonPropertyName("ResumoConversa")] string ResumoConversa);

public record ParseResult(
    string CPF,
    string NOME,
    string TELEFONE,
    string ENDERECO,
    string CEP,
    string NUMERO_DA_NOTA,
    string DATA_DE_COMPRA,
    string NUMERO_DE_SERIE
);

public static class ParserHelpers
{
    public static ParseResult ExtrairCampos(string texto)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant;
        texto = Regex.Replace(texto, @"\s+", " ").Trim();

        string nextLabels = @"(?=\s+(CPF:|NOME:|TELEFONE:|ENDEREÇO:|NÚMERO DA NOTA E DATA DE COMPRA:|NÚMERO DE SÉRIE:)|$)";

        string ExtrairEntre(string label)
        {
            var pattern = $@"{Regex.Escape(label)}[:\s]*?(?<val>.*?){nextLabels}";
            var m = Regex.Match(texto, pattern, options);
            return m.Success ? m.Groups["val"].Value.Trim() : string.Empty;
        }

        var cpfMatch = Regex.Match(texto, @"CPF[:\s]*?(?<cpf>\d{11})", options);
        var cpf = cpfMatch.Success ? cpfMatch.Groups["cpf"].Value : "";

        var nome = ExtrairEntre("NOME");

        var telefone = ExtrairEntre("TELEFONE");
        if (string.IsNullOrEmpty(telefone))
        {
            var telMatch = Regex.Match(texto, @"\(?\d{2}\)?\s?\d{4,5}[-\s]?\d{4}", options);
            if (telMatch.Success) telefone = telMatch.Value;
        }

        var endereco = ExtrairEntre("ENDEREÇO");

        var cep = "";
        var cepMatch = Regex.Match(endereco, @"\b(\d{8})\b");
        if (cepMatch.Success) cep = cepMatch.Groups[1].Value;
        else
        {
            cepMatch = Regex.Match(texto, @"\b(\d{8})\b");
            if (cepMatch.Success) cep = cepMatch.Groups[1].Value;
        }

        var notaDataBlock = ExtrairEntre("NÚMERO DA NOTA E DATA DE COMPRA");
        string numeroNota = "";
        string dataCompra = "";

        if (!string.IsNullOrEmpty(notaDataBlock))
        {
            var notaMatch = Regex.Match(notaDataBlock, @"(?<nota>\d+)", options);
            if (notaMatch.Success) numeroNota = notaMatch.Groups["nota"].Value;

            var dataMatch = Regex.Match(notaDataBlock, @"(?<data>\d{2}/\d{2}/\d{4})", options);
            if (!dataMatch.Success)
                dataMatch = Regex.Match(notaDataBlock, @"(?<data>\d{2}/\d{2}/\d{2,4})", options);
            if (dataMatch.Success) dataCompra = dataMatch.Groups["data"].Value;
        }
        else
        {
            var notaMatch = Regex.Match(texto, @"NÚMERO.*NOTA[:\s]*?(?<nota>\d+)", options);
            if (notaMatch.Success) numeroNota = notaMatch.Groups["nota"].Value;

            var dataMatch = Regex.Match(texto, @"(?<data>\d{2}/\d{2}/\d{4})", options);
            if (dataMatch.Success) dataCompra = dataMatch.Groups["data"].Value;
        }

        var numSerie = ExtrairEntre("NÚMERO DE SÉRIE");
        if (string.IsNullOrEmpty(numSerie))
        {
            var serieMatch = Regex.Match(texto, @"\b(\d{10,})\b", options);
            if (serieMatch.Success) numSerie = serieMatch.Groups[1].Value;
        }

        string Clean(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().Trim(',', ':');

        return new ParseResult(
            CPF: Clean(cpf),
            NOME: Clean(nome),
            TELEFONE: Clean(telefone),
            ENDERECO: Clean(endereco),
            CEP: Clean(cep),
            NUMERO_DA_NOTA: Clean(numeroNota),
            DATA_DE_COMPRA: Clean(dataCompra),
            NUMERO_DE_SERIE: Clean(numSerie)
        );
    }
}

// -----------------------------------------------------------------------------
// TOP-LEVEL STATEMENTS (tudo abaixo é a aplicação que inicia e roda)
// -----------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapPost("/parse", async (HttpRequest request) =>
{
    var body = await request.ReadFromJsonAsync<ResumoRequest>();
    if (body == null || string.IsNullOrWhiteSpace(body.ResumoConversa))
        return Results.BadRequest(new { error = "ResumoConversa é obrigatório." });

    var texto = body.ResumoConversa;
    var resultado = ParserHelpers.ExtrairCampos(texto);
    return Results.Ok(resultado);
});

app.MapGet("/", () => "API ativa. POST /parse com JSON {\"ResumoConversa\":\"...\"}.");

app.Run();
