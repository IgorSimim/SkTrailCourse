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
4. Disputes.DeleteDispute - PARA: Excluir, remover, apagar uma reclamação específica
5. Disputes.ShowDispute - PARA: Detalhes, informações específicas de uma reclamação
6. BoletoLookup.SearchByCustomerName - PARA: Consultar, verificar, identificar origem de boletos/cobranças

ANÁLISE DE INTENÇÃO - PERGUNTAS CRÍTICAS:

- O usuário está se referindo a uma reclamação EXISTENTE (menciona ID como 9b344c60 ou fala ""aquela reclamação"")? → EditDispute/ShowDispute/DeleteDispute
- O usuário quer CRIAR uma NOVA reclamação? → AddDispute  
- O usuário quer apenas CONSULTAR/VER informações? → ListDisputes ou SearchByCustomerName
- O usuário menciona ""boleto"", ""cobrança"", ""verifiquei"" sem reclamar? → SearchByCustomerName

EXEMPLOS DE INTENÇÃO:

- ""quero reclamar de uma cobrança da Netflix"" → NOVA reclamação → AddDispute
- ""na vdd a reclamação 9b344c60 é de 500 reais"" → CORREÇÃO de existente → EditDispute (id: ""9b344c60"", correctionText: ""é de 500 reais"")
- ""aquela reclamação que fiz, o valor é 300"" → CORREÇÃO de existente → EditDispute  
- ""lista minhas reclamações"" → LISTAR → ListDisputes
- ""verifiquei uma compra no boleto"" → CONSULTAR → SearchByCustomerName
- ""excluir a 9b344c60"" → EXCLUIR → DeleteDispute (id: ""9b344c60"")
- ""detalhes da abc123"" → DETALHES → ShowDispute (id: ""abc123"")

EXTRAÇÃO DE PARÂMETROS:
- Para EditDispute: extraia 'id' (padrão: 8 caracteres alfanuméricos) e 'correctionText' (o texto da correção)
- Para DeleteDispute/ShowDispute: extraia apenas 'id'
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

                        // Processa parâmetros de forma inteligente
                        if (routeInfo.Parameters != null)
                        {
                            foreach (var param in routeInfo.Parameters)
                            {
                                var paramValue = param.Value?.ToString();
                                if (!string.IsNullOrEmpty(paramValue) && !paramValue.Equals("null", StringComparison.OrdinalIgnoreCase))
                                {
                                    args[param.Key] = paramValue;
                                    Console.WriteLine($"🔧 Parâmetro extraído: {param.Key} = {paramValue}");
                                }
                            }
                        }

                        // GARANTE parâmetros obrigatórios baseados na função
                        if (routeInfo.Function.Equals("AddDispute", StringComparison.OrdinalIgnoreCase) && !args.ContainsKey("complaint"))
                        {
                            args["complaint"] = input;
                        }

                        if (routeInfo.Function.Equals("EditDispute", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!args.ContainsKey("id"))
                            {
                                // Tenta extrair ID como fallback
                                var extractedId = ExtractIdFromInput(input);
                                if (extractedId != null)
                                {
                                    args["id"] = extractedId;
                                    args["correctionText"] = input;
                                    Console.WriteLine($"🔧 ID extraído como fallback: {extractedId}");
                                }
                            }
                            if (!args.ContainsKey("correctionText"))
                            {
                                args["correctionText"] = input;
                            }
                        }

                        if ((routeInfo.Function.Equals("DeleteDispute", StringComparison.OrdinalIgnoreCase) || 
                             routeInfo.Function.Equals("ShowDispute", StringComparison.OrdinalIgnoreCase)) &&
                            !args.ContainsKey("id"))
                        {
                            var extractedId = ExtractIdFromInput(input);
                            if (extractedId != null)
                            {
                                args["id"] = extractedId;
                            }
                        }

                        Console.WriteLine($"🎯 Roteamento final: {cleanPlugin}.{routeInfo.Function}");
                        return (cleanPlugin, routeInfo.Function, args);
                    }
                }
                catch (JsonException jex)
                {
                    Console.WriteLine($"❌ Erro ao analisar JSON: {jex.Message}");
                    Console.WriteLine($"📄 JSON problemático: {jsonRaw}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro no roteador: {ex.Message}");
        }

        // FALLBACK MÍNIMO: Se tudo falhar, pergunta se é consulta ou reclamação
        Console.WriteLine($"🔄 Fallback mínimo - assumindo consulta de boleto");
        return ("BoletoLookup", "SearchByCustomerName", args);
    }

    private string? ExtractIdFromInput(string input)
    {
        try
        {
            var idPattern = @"\b[a-f0-9]{8}\b|\b[A-Za-z0-9]{6,8}\b";
            var match = Regex.Match(input, idPattern);
            return match.Success ? match.Value : null;
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