using Microsoft.SemanticKernel;
using System.Text.Json;

namespace SkOfflineCourse.Infra;

public class AIIntentRouter
{
    private readonly Kernel _kernel;
    private readonly Dictionary<string, List<string>> _pluginFunctions;

    public AIIntentRouter(Kernel kernel)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _pluginFunctions = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "Tasks", new List<string> { "AddTask", "ListTasks", "CompleteTask", "RecommendNext" } },
            { "Notes", new List<string> { "AddNote", "ListNotes", "SearchNotes", "SummarizeNote" } }
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
Você é um assistente especializado em identificar intenções de usuários e rotear comandos para funções adequadas.

Analise a entrada do usuário e determine qual função deve ser chamada de acordo com as seguintes opções disponíveis:

Plugin Tasks:
- AddTask: Adiciona uma tarefa (parâmetro: title)
- ListTasks: Lista todas as tarefas (sem parâmetros)
- CompleteTask: Marca uma tarefa como concluída (parâmetro: index - número inteiro)
- RecommendNext: Sugere a próxima tarefa a ser feita (sem parâmetros)

Plugin Notes:
- AddNote: Adiciona uma nota (parâmetro: content)
- ListNotes: Lista todas as notas (sem parâmetros)
- SearchNotes: Busca notas por um termo (parâmetro: term)
- SummarizeNote: Gera um resumo de uma nota específica (parâmetro: index - número inteiro)

Entrada do usuário: {input}

Responda em formato JSON:
{{
  ""plugin"": ""[nome do plugin: Tasks ou Notes]"",
  ""function"": ""[nome da função]"",
  ""parameters"": {{
    // parâmetros necessários para a função (se houver)
  }}
}}

Se a entrada não corresponder a nenhuma função, retorne plugin e function como null.
";

            var result = await _kernel.InvokePromptAsync(prompt);
            var response = result.ToString().Trim();

            // Extrair o JSON da resposta (pode estar envolvido em ```json ... ```)
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonRaw = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                
                // Tenta fazer o parsing do JSON
                var options = new JsonSerializerOptions { 
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                
                try 
                {
                    var routeInfo = JsonSerializer.Deserialize<RouteInfo>(jsonRaw, options);
                
                    if (routeInfo != null && !string.IsNullOrEmpty(routeInfo.Plugin) && !string.IsNullOrEmpty(routeInfo.Function))
                    {
                        // Verificar se o plugin e função existem
                        if (_pluginFunctions.TryGetValue(routeInfo.Plugin, out var functions) && 
                            functions.Contains(routeInfo.Function, StringComparer.OrdinalIgnoreCase))
                        {
                            // Adicionar parâmetros ao KernelArguments
                            if (routeInfo.Parameters != null)
                            {
                                foreach (var param in routeInfo.Parameters)
                                {
                                    args[param.Key] = param.Value?.ToString();
                                }
                            }
                            
                            // Tratamentos específicos para parâmetros comuns
                            if (routeInfo.Function.Equals("AddTask", StringComparison.OrdinalIgnoreCase) && !args.ContainsKey("title"))
                            {
                                // Extrair título da tarefa da entrada do usuário
                                var title = ExtractContentAfterKeyword(input, "tarefa");
                                args["title"] = string.IsNullOrWhiteSpace(title) ? "Sem título" : title;
                            }
                            else if (routeInfo.Function.Equals("AddNote", StringComparison.OrdinalIgnoreCase) && !args.ContainsKey("content"))
                            {
                                // Extrair conteúdo da nota da entrada do usuário
                                var content = ExtractContentAfterKeyword(input, "nota");
                                args["content"] = string.IsNullOrWhiteSpace(content) ? "Vazio" : content;
                            }
                            
                            return (routeInfo.Plugin, routeInfo.Function, args);
                        }
                    }
                }
                catch (JsonException jex)
                {
                    Console.WriteLine($"Erro ao analisar JSON da resposta do modelo: {jex.Message}");
                    // Tenta limpar o JSON de caracteres problemáticos e tentar novamente
                    try
                    {
                        string cleanedJson = jsonRaw
                            .Replace("\\", "\\\\")  // Escape backslashes
                            .Replace("\r", "")      // Remove carriage returns
                            .Replace("\n", " ")     // Replace newlines with spaces
                            .Replace("/", "\\/");   // Escape forward slashes
                            
                        var routeInfo = JsonSerializer.Deserialize<RouteInfo>(cleanedJson, options);
                        
                        if (routeInfo != null && !string.IsNullOrEmpty(routeInfo.Plugin) && !string.IsNullOrEmpty(routeInfo.Function))
                        {
                            if (_pluginFunctions.TryGetValue(routeInfo.Plugin, out var functions) && 
                                functions.Contains(routeInfo.Function, StringComparer.OrdinalIgnoreCase))
                            {
                                // Processar parâmetros
                                if (routeInfo.Parameters != null)
                                {
                                    foreach (var param in routeInfo.Parameters)
                                    {
                                        args[param.Key] = param.Value?.ToString();
                                    }
                                }
                                
                                return (routeInfo.Plugin, routeInfo.Function, args);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Falha na segunda tentativa de parsing do JSON: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao rotear com modelo de IA: {ex.Message}");
            // Sem fallback - se o modelo falhar, retorna null
        }

        return (null, null, args);
    }

    private string ExtractContentAfterKeyword(string input, string keyword)
    {
        var keywordIndex = input.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (keywordIndex < 0) return string.Empty;
        
        return input[(keywordIndex + keyword.Length)..].Trim();
    }

    private class RouteInfo
    {
        public string? Plugin { get; set; }
        public string? Function { get; set; }
        public Dictionary<string, object?>? Parameters { get; set; }
    }
}