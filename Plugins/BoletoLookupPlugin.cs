using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SkTrailCourse.Plugins;

public class BoletoLookupPlugin
{
    private readonly string _dataFile = Path.Combine("data", "boletos.json");

    public record Empresa(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("nome_fantasia")] string NomeFantasia,
        [property: JsonPropertyName("razao_social")] string RazaoSocial,
        [property: JsonPropertyName("contato_email")] string ContatoEmail,
        [property: JsonPropertyName("telefone")] string Telefone
    );

  public record Boleto(
    [property: JsonPropertyName("boleto_id")] string BoletoId,
    [property: JsonPropertyName("emissor_id")] string EmissorId,
    [property: JsonPropertyName("valor")] decimal Valor,
    [property: JsonPropertyName("vencimento")] string Vencimento,
    [property: JsonPropertyName("pagavel_para")] string PagavelPara,
    [property: JsonPropertyName("documento_pagavel")] string DocumentoPagavel,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("descricao")] string Descricao  // ← NOVO CAMPO
);

    public record BoletoData(
        [property: JsonPropertyName("empresas")] List<Empresa> Empresas,
        [property: JsonPropertyName("boletos")] List<Boleto> Boletos
    );

    [KernelFunction, Description("Buscar boletos por nome do cliente")]
    public async Task<string> SearchByCustomerName(
        [Description("Nome completo do cliente para buscar boletos")] string nomeCliente)
    {
        try
        {
            if (!File.Exists(_dataFile))
            {
                return $"❌ Arquivo de dados não encontrado.";
            }

            var json = await File.ReadAllTextAsync(_dataFile);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var data = JsonSerializer.Deserialize<BoletoData>(json, options);

            if (data == null)
            {
                return "❌ Erro: Não foi possível ler os dados do arquivo.";
            }

            if (data.Boletos == null || data.Empresas == null)
            {
                return "❌ Estrutura de dados inválida no arquivo.";
            }

            // Busca case-insensitive por correspondência parcial no nome
            var boletosCliente = data.Boletos
                .Where(b => ContainsName(b.PagavelPara, nomeCliente))
                .ToList();

            if (!boletosCliente.Any())
            {
                // Tenta busca mais flexível
                var boletosFlexiveis = data.Boletos
                    .Where(b => ContainsFlexibleName(b.PagavelPara, nomeCliente))
                    .ToList();
                    
                if (boletosFlexiveis.Any())
                {
                    boletosCliente = boletosFlexiveis;
                }
            }

            if (!boletosCliente.Any())
                return $"❌ Nenhum boleto encontrado para '{nomeCliente}'.";

            var resultados = new List<string>();
            
        foreach (var boleto in boletosCliente)
        {
            var empresa = data.Empresas.FirstOrDefault(e => e.Id == boleto.EmissorId);
            var nomeEmpresa = empresa?.NomeFantasia ?? "Empresa não encontrada";
            var contato = empresa?.ContatoEmail ?? "Contato não disponível";

            var descricaoFormatada = !string.IsNullOrEmpty(boleto.Descricao) 
                ? $"\n   📝 Descrição: {boleto.Descricao}" 
                : "";

            // Detecta se a Zoop é intermediária
            var isIntermediaria = nomeEmpresa.Contains("Zoop") && 
                                !string.IsNullOrEmpty(boleto.Descricao) &&
                                boleto.Descricao.Length > 10;

            var avisoIntermediaria = isIntermediaria 
                ? $"\n   💡 A Zoop é a plataforma de pagamentos. O estabelecimento real é mencionado na descrição acima."
                : "";

            resultados.Add(
                $"📄 Boleto {boleto.BoletoId} - R$ {boleto.Valor:F2} (vencimento {boleto.Vencimento})\n" +
                $"   Emitido por: {nomeEmpresa}\n" +
                $"   Contato: {contato}\n" +
                $"   Status: {boleto.Status}{descricaoFormatada}{avisoIntermediaria}"
            );
        }

            return $"✅ Encontramos {boletosCliente.Count} boleto(s) para '{nomeCliente}':\n\n" +
                   string.Join("\n\n", resultados);
        }
        catch (JsonException jex)
        {
            return $"❌ Erro no formato do arquivo de dados: {jex.Message}";
        }
        catch (Exception ex)
        {
            return $"❌ Erro ao consultar boletos: {ex.Message}";
        }
    }

    private bool ContainsName(string target, string search)
    {
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(search))
            return false;

        // Remove acentos e converte para minúsculas
        var targetClean = RemoveAccents(target).ToLower();
        var searchClean = RemoveAccents(search).ToLower();

        // Remove espaços extras e divide em partes
        var targetParts = targetClean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var searchParts = searchClean.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Verifica se todas as partes do nome de busca estão no nome alvo
        return searchParts.All(searchPart => 
            targetParts.Any(targetPart => 
                targetPart.Contains(searchPart)));
    }

    private bool ContainsFlexibleName(string target, string search)
    {
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(search))
            return false;

        // Remove acentos e converte para minúsculas
        var targetClean = RemoveAccents(target).ToLower();
        var searchClean = RemoveAccents(search).ToLower();

        // Busca mais flexível - apenas algumas partes precisam coincidir
        var targetParts = targetClean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var searchParts = searchClean.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Pelo menos 2 partes devem coincidir (ou todas se houver menos de 3)
        var minMatches = searchParts.Length >= 3 ? 2 : searchParts.Length;
        var matches = searchParts.Count(searchPart => 
            targetParts.Any(targetPart => targetPart.Contains(searchPart)));

        return matches >= minMatches;
    }

    private string RemoveAccents(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }

    [KernelFunction, Description("Listar todas as empresas cadastradas")]
    public async Task<string> ListCompanies()
    {
        try
        {
            if (!File.Exists(_dataFile))
                return $"❌ Arquivo de dados não encontrado.";

            var json = await File.ReadAllTextAsync(_dataFile);
            
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var data = JsonSerializer.Deserialize<BoletoData>(json, options);

            if (data?.Empresas == null || !data.Empresas.Any())
                return "❌ Nenhuma empresa cadastrada";

            var empresasFormatadas = data.Empresas.Select((e, index) => 
                $"{index + 1}. {e.NomeFantasia} ({e.RazaoSocial})\n   📧 {e.ContatoEmail} | 📞 {e.Telefone}");

            return "🏢 Empresas cadastradas:\n\n" + string.Join("\n\n", empresasFormatadas);
        }
        catch (Exception ex)
        {
            return $"❌ Erro ao listar empresas: {ex.Message}";
        }
    }
}