using Microsoft.SemanticKernel;
using System.Text.Json;

namespace SkTrailCourse.Infra;

public class AIIntentRouter
{
    private readonly Kernel _kernel;

    public AIIntentRouter(Kernel kernel)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    public async Task<(string? plugin, string? function, KernelArguments args)> RouteAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (null, null, new KernelArguments());

        var args = new KernelArguments();

        // PRIMEIRO: Verificação direta para consultas de boleto (antes da IA)
        if (ContainsBoletoKeywords(input))
        {
            return ("BoletoLookup", "SearchByCustomerName", new KernelArguments());
        }

        try
        {
            var prompt = @$"
Analise a entrada do usuário e determine qual função deve ser chamada:

Entrada do usuário: {input}

Opções disponíveis:
- Plugin 'Disputes':
  • AddDispute(complaint) - Para RECLAMAÇÕES sobre cobranças indevidas, fraudes, problemas com compras
  • ListDisputes() - Listar todas as disputas  
  • UpdateDisputeStatus(id, newStatus) - Atualizar status
  • DeleteDispute(id) - Excluir disputa
  • ShowDispute(id) - Mostrar detalhes

- Plugin 'BoletoLookup':
  • SearchByCustomerName(nomeCliente) - Para CONSULTAS de boletos, verificar origem de cobranças, identificar empresas

REGRAS DE DECISÃO:
- Use 'BoletoLookup' quando o usuário quer SABER a origem de uma cobrança, verificar um boleto, identificar qual empresa emitiu
- Use 'Disputes' quando o usuário quer RECLAMAR sobre uma cobrança indevida

Exemplos:
- 'verifiquei uma compra de 150 reais no boleto' → BoletoLookup
- 'não reconheço essa cobrança no meu extrato' → BoletoLookup  
- 'quem emitiu esse boleto?' → BoletoLookup
- 'quero reclamar de uma cobrança indevida' → Disputes
- 'fraude na minha fatura' → Disputes

Responda SOMENTE com JSON:

{{
  ""plugin"": ""NomeDoPlugin"",
  ""function"": ""NomeDaFuncao"",
  ""parameters"": {{ ""param1"": ""valor1"", ""param2"": ""valor2"" }}
}}

Use exatamente estes nomes de plugin: 'Disputes' ou 'BoletoLookup'";

            var result = await _kernel.InvokePromptAsync(prompt);
            var response = result.ToString().Trim();

            // Limpa markdown se houver
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
                        // Remove "Plugin " do nome se existir
                        var cleanPlugin = routeInfo.Plugin.Replace("Plugin ", "").Replace("plugin ", "").Trim();

                        // Processa parâmetros
                        if (routeInfo.Parameters != null)
                        {
                            foreach (var param in routeInfo.Parameters)
                            {
                                args[param.Key] = param.Value?.ToString();
                            }
                        }

                        // Garante que AddDispute sempre tenha o complaint
                        if (routeInfo.Function.Equals("AddDispute", StringComparison.OrdinalIgnoreCase) && !args.ContainsKey("complaint"))
                        {
                            args["complaint"] = input;
                        }

                        return (cleanPlugin, routeInfo.Function, args);
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

        // Fallback padrão para novas reclamações
        args["complaint"] = input;
        return ("Disputes", "AddDispute", args);
    }

    private bool ContainsBoletoKeywords(string input)
    {
        var boletoKeywords = new[] { "boleto", "boletos", "cobrança", "cobrancas", "compra no", "pagamento", "fatura", "verifiquei", "encontrei", "vi uma", "identifiquei", "qual empresa", "quem emitiu", "origem da", "desse valor", "desta cobrança" };
        var lowerInput = input.ToLower();

        return boletoKeywords.Any(keyword => lowerInput.Contains(keyword));
    }

    private class RouteInfo
    {
        public string? Plugin { get; set; }
        public string? Function { get; set; }
        public Dictionary<string, object?>? Parameters { get; set; }
    }
}