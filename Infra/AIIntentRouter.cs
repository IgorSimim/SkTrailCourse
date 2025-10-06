using Microsoft.SemanticKernel;
using System.Text.Json;
using System.Text.RegularExpressions;

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
ANALISE a intenção real do usuário e determine a função correta:

ENTRADA DO USUÁRIO: ""{input}""

FUNÇÕES DISPONÍVEIS:

1. Disputes.AddDispute - PARA: Nova reclamação, fraude, cobrança indevida, problema não relatado antes
2. Disputes.EditDispute - PARA: Corrigir, atualizar, modificar uma reclamação JÁ EXISTENTE (quando menciona ID ou se refere a uma reclamação anterior)
3. Disputes.ListDisputes - PARA: Listar, ver, mostrar todas as reclamações
4. Disputes.DeleteDispute - PARA: Excluir, remover, apagar, deletar uma reclamação específica
5. Disputes.ShowDispute - PARA: Detalhes, informações específicas de uma reclamação
6. BoletoLookup.SearchByCustomerName - PARA: Consultar, verificar, identificar origem de boletos/cobranças

ANÁLISE DE INTENÇÃO - PERGUNTAS CRÍTICAS:

- O usuário quer EXCLUIR/REMOVER/DELETAR uma reclamação? → DeleteDispute
- O usuário quer CRIAR uma NOVA reclamação? → AddDispute  
- O usuário quer ATUALIZAR/MODIFICAR/CORRIGIR uma reclamação existente? → EditDispute
- O usuário quer apenas CONSULTAR/VER informações de boletos? → SearchByCustomerName
- O usuário quer LISTAR todas as reclamações? → ListDisputes
- O usuário quer VER DETALHES de uma reclamação específica? → ShowDispute

EXEMPLOS DE INTENÇÃO:

- ""quero reclamar de uma cobrança da Netflix"" → NOVA reclamação → AddDispute
- ""excluir a reclamação d8794da0"" → EXCLUIR → DeleteDispute (id: ""d8794da0"")
- ""remover d8794da0"" → EXCLUIR → DeleteDispute (id: ""d8794da0"")
- ""deletar minha reclamação"" → EXCLUIR → DeleteDispute
- ""atualizar a reclamação abc123 para valor 500"" → EDITAR → EditDispute (id: ""abc123"", correctionText: ""valor 500"")
- ""lista minhas reclamações"" → LISTAR → ListDisputes
- ""ver detalhes da d8794da0"" → DETALHES → ShowDispute (id: ""d8794da0"")
- ""consultar boletos da Zoop"" → CONSULTAR → SearchByCustomerName

EXTRAÇÃO DE PARÂMETROS:
- SEMPRE extraia 'id' quando mencionado (padrão: 6-8 caracteres alfanuméricos)
- Para EditDispute: extraia 'correctionText' (o texto da correção)
- Para AddDispute: use o texto completo como 'complaint'

RESPONDA SOMENTE COM JSON:

{{
  ""plugin"": ""Disputes"",
  ""function"": ""AddDispute"",
  ""parameters"": {{ 
    ""id"": ""valor_ou_null"", 
    ""complaint"": ""valor_ou_null"",
    ""correctionText"": ""valor_ou_null""
  }}
}}

OU

{{
  ""plugin"": ""BoletoLookup"", 
  ""function"": ""SearchByCustomerName"",
  ""parameters"": {{}}
}}";

            var result = await _kernel.InvokePromptAsync(prompt);
            var response = result.ToString().Trim();

            Console.WriteLine($"🤖 Resposta da IA: {response}");

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
                        var cleanPlugin = routeInfo.Plugin.Replace("Plugin ", "").Replace("plugin ", "").Trim();

                        // Processa parâmetros da IA
                        if (routeInfo.Parameters != null)
                        {
                            foreach (var param in routeInfo.Parameters)
                            {
                                var paramValue = param.Value?.ToString();
                                if (!string.IsNullOrEmpty(paramValue) && !paramValue.Equals("null", StringComparison.OrdinalIgnoreCase))
                                {
                                    args[param.Key] = paramValue;
                                    Console.WriteLine($"🔧 Parâmetro extraído pela IA: {param.Key} = {paramValue}");
                                }
                            }
                        }

                        // Garante parâmetros essenciais
                        EnsureEssentialParameters(args, routeInfo.Function, input);

                        Console.WriteLine($"🎯 Roteamento final pela IA: {cleanPlugin}.{routeInfo.Function}");
                        return (cleanPlugin, routeInfo.Function, args);
                    }
                }
                catch (JsonException jex)
                {
                    Console.WriteLine($"❌ Erro ao analisar JSON da IA: {jex.Message}");
                    Console.WriteLine($"📄 JSON problemático: {jsonRaw}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro no roteador IA: {ex.Message}");
        }

        // Fallback para consulta
        Console.WriteLine($"🔄 Fallback - assumindo consulta de boleto");
        return ("BoletoLookup", "SearchByCustomerName", args);
    }

    private void EnsureEssentialParameters(KernelArguments args, string function, string input)
    {
        // Garante parâmetros obrigatórios baseados na função
        if (function.Equals("AddDispute", StringComparison.OrdinalIgnoreCase) && !args.ContainsKey("complaint"))
        {
            args["complaint"] = input;
        }

        if (function.Equals("EditDispute", StringComparison.OrdinalIgnoreCase))
        {
            if (!args.ContainsKey("id"))
            {
                var extractedId = ExtractIdFromInput(input);
                if (extractedId != null)
                {
                    args["id"] = extractedId;
                    Console.WriteLine($"🔧 ID extraído como fallback: {extractedId}");
                }
            }
            if (!args.ContainsKey("correctionText"))
            {
                args["correctionText"] = input;
            }
        }

        if ((function.Equals("DeleteDispute", StringComparison.OrdinalIgnoreCase) || 
             function.Equals("ShowDispute", StringComparison.OrdinalIgnoreCase)) &&
            !args.ContainsKey("id"))
        {
            var extractedId = ExtractIdFromInput(input);
            if (extractedId != null)
            {
                args["id"] = extractedId;
                Console.WriteLine($"🔧 ID extraído como fallback: {extractedId}");
            }
        }
    }

    private string? ExtractIdFromInput(string input)
    {
        try
        {
            // Padrão flexível para IDs - 6-8 caracteres alfanuméricos
            var idPattern = @"\b[a-zA-Z0-9]{6,8}\b";
            var matches = Regex.Matches(input, idPattern);
            
            // Filtra palavras comuns que não são IDs
            var commonWords = new[] { 
                "quero", "deletar", "remover", "excluir", "apagar", 
                "minhas", "reclamacoes", "reclamações", "disputa",
                "listar", "consultar", "detalhes", "ver", "mostrar"
            };
            
            foreach (Match match in matches)
            {
                var candidate = match.Value;
                // Verifica se não é uma palavra comum
                if (!commonWords.Contains(candidate.ToLower()) && 
                    !int.TryParse(candidate, out _)) // não é apenas números
                {
                    return candidate;
                }
            }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao extrair ID: {ex.Message}");
            return null;
        }
    }

    private class RouteInfo
    {
        public string? Plugin { get; set; }
        public string? Function { get; set; }
        public Dictionary<string, object?>? Parameters { get; set; }
    }
}