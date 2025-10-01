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

        try
        {
            var prompt = @$"
Analise a entrada do usuário e determine qual função deve ser chamada:

Entrada do usuário: {input}

Opções disponíveis no plugin Disputes:
- AddDispute(complaint) - Adicionar nova reclamação
- ListDisputes() - Listar todas as disputas  
- UpdateDisputeStatus(id, newStatus) - Atualizar status
- DeleteDispute(id) - Excluir disputa
- ShowDispute(id) - Mostrar detalhes

Responda SOMENTE com JSON:

{{
  ""plugin"": ""Disputes"",
  ""function"": ""NomeDaFuncao"",
  ""parameters"": {{ ""param1"": ""valor1"", ""param2"": ""valor2"" }}
}}";

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

                        return (routeInfo.Plugin, routeInfo.Function, args);
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

    private class RouteInfo
    {
        public string? Plugin { get; set; }
        public string? Function { get; set; }
        public Dictionary<string, object?>? Parameters { get; set; }
    }
}