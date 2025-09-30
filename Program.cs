using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using SkTrailCourse.Infra;
using SkTrailCourse.Plugins;
using System.Text;
using DotNetEnv;

Env.Load();

Console.OutputEncoding = Encoding.UTF8;

var kernelBuilder = Kernel.CreateBuilder();

try
{
    var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
    var modelId = Environment.GetEnvironmentVariable("AI_MODEL_ID") ?? "gemini-2.5-flash";

    if (string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("GOOGLE_API_KEY não encontrada.");

    kernelBuilder.AddGoogleAIGeminiChatCompletion(
        modelId: modelId,
        apiKey: apiKey,
        apiVersion: GoogleAIVersion.V1);

    Console.WriteLine("✅ Modelo Gemini conectado com sucesso!");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Erro: {ex.Message}");
    Environment.Exit(1);
}

var kernel = kernelBuilder.Build();
var store = new JsonMemoryStore("data");

// === Plugins ===
var orchestrator = new DisputeOrchestrator(kernel, store);
var disputes = new DisputePlugin(store, kernel, orchestrator);
kernel.ImportPluginFromObject(disputes, "Disputes");

// Router
var router = new AIIntentRouter(kernel);

Console.WriteLine("=== Zoop AI Analyst (MVP) ===");
Console.WriteLine("Digite uma reclamação, exemplo:");
Console.WriteLine("  'Não reconheço a cobrança de 39,90 da FitEasy'");
Console.WriteLine("Comandos:");
Console.WriteLine("  - listar reclamações");
Console.WriteLine("  - mostrar reclamações");
Console.WriteLine("Digite 'sair' para encerrar.");
Console.WriteLine("----------------------------------------");

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) continue;
    if (input.Equals("sair", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

    try
    {
        var routeResult = await router.RouteAsync(input);
        var plugin = routeResult.plugin;
        var functionName = routeResult.function;
        var skArgs = routeResult.args;

        if (plugin is null || functionName is null)
        {
            Console.WriteLine("❓ Não entendi. Tente: 'Não reconheço cobrança de 39,90 da FitEasy'");
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
