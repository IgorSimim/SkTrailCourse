using Microsoft.SemanticKernel;
using System.Text.Json;

namespace SkTrailCourse.Infra;

public class AIIntentRouter
{
    private readonly Kernel _kernel;
    private readonly Dictionary<string, List<string>> _pluginFunctions;

    public AIIntentRouter(Kernel kernel)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _pluginFunctions = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "Disputes", new List<string>
                { "AddDispute", "ListDisputes", "UpdateDisputeStatus", "DeleteDispute", "ShowDispute", "AnalyzeDispute" }
            },
            { "Support", new List<string>
                { "CheckTransaction", "GetRefundPolicy", "ContactSupport", "GenerateReport" }
            }
        };
    }

    public async Task<(string? plugin, string? function, KernelArguments args)> RouteAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (null, null, new KernelArguments());

        var args = new KernelArguments();

        try
        {
            var prompt = @$"
Você é um assistente especializado em identificar intenções de usuários relacionadas a disputas financeiras, cobranças indevidas e suporte ao cliente.

Analise a entrada do usuário e determine qual função deve ser chamada de acordo com as seguintes opções disponíveis:

Plugin Disputes (Disputas/Reclamações):
- AddDispute(complaint) - Adicionar nova reclamação
- ListDisputes() - Listar todas as disputas
- UpdateDisputeStatus(id, newStatus) - Atualizar status da disputa
- DeleteDispute(id) - Excluir disputa
- ShowDispute(id) - Mostrar detalhes de uma disputa
- AnalyzeDispute(transactionDetails) - Analisar transação suspeita

Plugin Support (Suporte):
- CheckTransaction(transactionId) - Verificar detalhes da transação
- GetRefundPolicy() - Consultar política de reembolso
- ContactSupport(issue) - Entrar em contato com suporte
- GenerateReport(period) - Gerar relatório de disputas

Entrada do usuário: {input}

IMPORTANTE: Responda SOMENTE com o objeto JSON.
NÃO use ```json, nem texto fora do JSON.

Exemplo de resposta correta para disputa:
{{
  ""plugin"": ""Disputes"",
  ""function"": ""AddDispute"",
  ""parameters"": {{ ""complaint"": ""Não reconheço a cobrança de 39,90 da FitEasy"" }}
}}

Exemplo para verificar transação:
{{
  ""plugin"": ""Support"", 
  ""function"": ""CheckTransaction"",
  ""parameters"": {{ ""transactionId"": ""TXN12345"" }}
}}

Exemplo para análise de disputa:
{{
  ""plugin"": ""Disputes"",
  ""function"": ""AnalyzeDispute"", 
  ""parameters"": {{ ""transactionDetails"": ""Cobrança duplicada da Loja XPTO no valor de R$ 150,00"" }}
}}
";

            var result = await _kernel.InvokePromptAsync(prompt);
            var response = result.ToString().Trim();

            // Limpa markdown se o modelo ignorar instrução
            response = response
                .Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                .Replace("```", "")
                .Trim();

            // Extrai JSON
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonRaw = response.Substring(jsonStart, jsonEnd - jsonStart + 1);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };

                try
                {
                    var routeInfo = JsonSerializer.Deserialize<RouteInfo>(jsonRaw, options);

                    if (routeInfo != null && !string.IsNullOrEmpty(routeInfo.Plugin) && !string.IsNullOrEmpty(routeInfo.Function))
                    {
                        if (_pluginFunctions.TryGetValue(routeInfo.Plugin, out var functions) &&
                            functions.Contains(routeInfo.Function, StringComparer.OrdinalIgnoreCase))
                        {
                            // Processa parâmetros
                            if (routeInfo.Parameters != null)
                            {
                                foreach (var param in routeInfo.Parameters)
                                {
                                    // Índices numéricos para ID
                                    if (routeInfo.Function is "UpdateDisputeStatus" or "DeleteDispute" or "ShowDispute")
                                    {
                                        if (int.TryParse(param.Value?.ToString(), out var idx))
                                            args[param.Key] = idx;
                                        else
                                            args[param.Key] = param.Value?.ToString();
                                    }
                                    else
                                    {
                                        args[param.Key] = param.Value?.ToString();
                                    }
                                }
                            }

                            // Fallbacks inteligentes para disputas
                            if (routeInfo.Function.Equals("AddDispute", StringComparison.OrdinalIgnoreCase) && !args.ContainsKey("complaint"))
                            {
                                // Extrai automaticamente a reclamação do input
                                args["complaint"] = ExtractComplaint(input);
                            }

                            if (routeInfo.Function.Equals("AnalyzeDispute", StringComparison.OrdinalIgnoreCase) && !args.ContainsKey("transactionDetails"))
                            {
                                args["transactionDetails"] = input;
                            }

                            return (routeInfo.Plugin, routeInfo.Function, args);
                        }
                    }
                }
                catch (JsonException jex)
                {
                    Console.WriteLine($"Erro ao analisar JSON: {jex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro no roteador: {ex.Message}");
        }

        return (null, null, args);
    }

    private string ExtractComplaint(string input)
    {
        // Palavras-chave que indicam uma reclamação
        var complaintKeywords = new[]
        {
            "não reconheço", "cobrança indevida", "não foi eu", "não autorizei",
            "cobrança errada", "valor incorreto", "não contratei", "disputa",
            "reclamação", "problema com cobrança", "estorno", "chargeback"
        };

        // Se contém palavras-chave de reclamação, usa o input completo
        if (complaintKeywords.Any(keyword => input.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return input;
        }

        // Tenta extrair contexto após palavras específicas
        var contextKeywords = new[] { "reclamação", "disputa", "problema", "erro" };
        foreach (var keyword in contextKeywords)
        {
            var keywordIndex = input.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (keywordIndex >= 0)
            {
                return input[(keywordIndex + keyword.Length)..].Trim();
            }
        }

        // Fallback: usa o input completo
        return input;
    }

    private class RouteInfo
    {
        public string? Plugin { get; set; }
        public string? Function { get; set; }
        public Dictionary<string, object?>? Parameters { get; set; }
    }
}