using Microsoft.SemanticKernel;
using SkOfflineCourse.Infra;
using SkOfflineCourse.Plugins;

var kernel = Kernel.CreateBuilder()
    // Sem AddOpenAI/AzureOpenAI: não há LLM aqui.
    .Build();

// “Memória” persistida em JSON
var store = new JsonMemoryStore("data"); // pasta data/ com .json
var tasks = new TaskPlugin(store);
var notes = new NotesPlugin(store);

// Registrando plugins no Kernel
kernel.ImportPluginFromObject(tasks, "Tasks");
kernel.ImportPluginFromObject(notes, "Notes");

// Router heurístico (simula planner)
var router = new IntentRouter();

Console.WriteLine("=== SK Offline Assistant (sem LLM) ===");
Console.WriteLine("Dicas:");
Console.WriteLine("- tarefas: 'add tarefa ...', 'listar tarefas', 'concluir 2', 'sugerir proxima'");
Console.WriteLine("- notas: 'add nota ...', 'listar notas', 'buscar nota termo', 'resumo 3'");
Console.WriteLine("- sair: 'exit' ou 'quit'");
Console.WriteLine("----------------------------------------");

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) continue;
    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("quit", StringComparison.OrdinalIgnoreCase)) break;

    var (plugin, functionName, args) = router.Route(input);

    if (plugin is null || functionName is null)
    {
        Console.WriteLine("❓ Não entendi. Tente: 'add tarefa Comprar café', 'listar tarefas', 'add nota ideia...', 'buscar nota café'.");
        continue;
    }

    try
    {
        var result = await kernel.InvokeAsync(plugin, functionName, args);
        Console.WriteLine(result?.ToString());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Erro: {ex.Message}");
    }
}
