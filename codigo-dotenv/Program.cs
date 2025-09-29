using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkOfflineCourse.Infra;
using SkOfflineCourse.Plugins;
using System.Text;

// Configurar codificação para exibir corretamente caracteres especiais
Console.OutputEncoding = Encoding.UTF8;

// Configuração do modelo de linguagem de IA
var kernelBuilder = Kernel.CreateBuilder();

try
{
    // Configuração para o modelo de linguagem local via HTTP
    // Ajuste o endpoint e as configurações para corresponder à sua instalação local
    kernelBuilder.AddOpenAIChatCompletion(
        modelId: "llama3.1:8b",
        apiKey: "apiKey",
        httpClient: new HttpClient { 
            BaseAddress = new Uri("http://localhost:11434/v1/") // Ajuste conforme sua configuração local
        });
        
    Console.WriteLine("✅ Modelo de IA conectado com sucesso!");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Erro ao conectar ao modelo de IA: {ex.Message}");
    Console.WriteLine("O aplicativo precisa de um modelo de IA para funcionar.");
    Environment.Exit(1); // Sair do programa se não conseguir conectar
}

var kernel = kernelBuilder.Build();

// "Memória" persistida em JSON
var store = new JsonMemoryStore("data");

// Cria plugins com acesso ao LLM
var tasks = new TaskPlugin(store, kernel);
var notes = new NotesPlugin(store, new AISummarizer(kernel));

// Registrando plugins no Kernel
kernel.ImportPluginFromObject(tasks, "Tasks");
kernel.ImportPluginFromObject(notes, "Notes");

// Router usando LLM
var router = new AIIntentRouter(kernel);

Console.WriteLine("=== Assistente Pessoal com IA ===");
Console.WriteLine("O que posso fazer por você:");
Console.WriteLine();
Console.WriteLine("📋 Gerenciar suas tarefas:");
Console.WriteLine("  • Criar tarefas - ex: \"Preciso comprar café amanhã\"");
Console.WriteLine("  • Mostrar suas tarefas - ex: \"Mostre minhas tarefas pendentes\"");
Console.WriteLine("  • Concluir tarefas - ex: \"Marquei como concluída a tarefa 2\"");
Console.WriteLine("  • Recomendar o que fazer - ex: \"O que devo fazer agora?\"");
Console.WriteLine();
Console.WriteLine("📝 Organizar suas notas:");
Console.WriteLine("  • Salvar anotações - ex: \"Anote que a reunião foi adiada para sexta\"");
Console.WriteLine("  • Ver suas anotações - ex: \"Mostrar todas as minhas notas\"");
Console.WriteLine("  • Buscar informações - ex: \"Encontre minhas notas sobre reunião\"");
Console.WriteLine("  • Resumir conteúdo - ex: \"Faça um resumo da nota 2\"");
Console.WriteLine();
Console.WriteLine("Digite 'sair' ou 'exit' para encerrar");
Console.WriteLine("----------------------------------------");

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) continue;
    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("quit", StringComparison.OrdinalIgnoreCase)) break;

    try
    {
        // Usando o router assíncrono baseado em LLM
        var routeResult = await router.RouteAsync(input);
        var plugin = routeResult.plugin;
        var functionName = routeResult.function;
        var skArgs = routeResult.args;

        if (plugin is null || functionName is null)
        {
            Console.WriteLine("❓ Desculpe, não entendi o que você precisa. Tente dizer de outra forma ou consulte as sugestões acima.");
            Console.WriteLine("   Por exemplo: \"Preciso comprar café\" ou \"Mostre minhas tarefas\".");
            continue;
        }

        var result = await kernel.InvokeAsync(plugin, functionName, skArgs);
        Console.WriteLine(result?.ToString());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Erro: {ex.Message}");
    }
}