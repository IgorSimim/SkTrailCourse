using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.Extensions.DependencyInjection;
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
    var modelId = Environment.GetEnvironmentVariable("AI_MODEL_ID") ?? "gemini-2.0-flash-exp";

    if (string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("GOOGLE_API_KEY não encontrada. Verifique seu arquivo .env");

    kernelBuilder.AddGoogleAIGeminiChatCompletion(
        modelId: modelId,
        apiKey: apiKey,
        apiVersion: GoogleAIVersion.V1);

    // adicionar um espaço antes do prompt para melhor formatação
    Console.WriteLine("✅ Modelo Gemini conectado com sucesso!");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Erro na configuração da IA: {ex.Message}");
    Console.WriteLine("💡 Verifique se a GOOGLE_API_KEY está configurada no arquivo .env");
    Environment.Exit(1);
}

var kernel = kernelBuilder.Build();
var store = new JsonMemoryStore("data");

// Configurar o HttpClient para a Injeção de Dependência
var serviceProvider = new ServiceCollection()
    .AddHttpClient() 
    .BuildServiceProvider();

var httpClient = serviceProvider.GetRequiredService<HttpClient>();


// === Plugins (usando suas classes originais) ===
var orchestrator = new DisputeOrchestrator(kernel, store);
var disputes = new DisputePlugin(store, kernel, orchestrator);
var support = new SupportPlugin(httpClient);

kernel.ImportPluginFromObject(disputes, "Disputes");
kernel.ImportPluginFromObject(support, "Support");

// Router (usando sua classe AIIntentRouter original)
var router = new AIIntentRouter(kernel);

Console.WriteLine("=== 🤖 Zoop AI Analyst (MVP) ===");
Console.WriteLine("Sistema de análise automática de cobranças indevidas");
Console.WriteLine();

Console.WriteLine("📝 COMO USAR:");
Console.WriteLine("• Digite uma reclamação sobre cobrança:");
Console.WriteLine("  Ex: 'Não reconheço a cobrança de 39,90 da FitEasy'");
Console.WriteLine("  Ex: 'Cobrança indevida da Loja XPTO no valor de R$ 150,00'");
Console.WriteLine();

Console.WriteLine("🔧 COMANDOS DISPONÍVEIS:");
Console.WriteLine("• 'listar reclamações' - Ver todas as disputas");
Console.WriteLine("• 'mostrar ABC123' - Detalhes de uma disputa");
Console.WriteLine("• 'atualizar ABC123 para resolvida' - Atualizar status");
Console.WriteLine("• 'excluir ABC123' - Remover uma disputa");
Console.WriteLine("• 'sair' - Encerrar o sistema");
Console.WriteLine();

Console.WriteLine("----------------------------------------");

while (true)
{
    Console.Write("💬 > ");
    var input = Console.ReadLine()?.Trim();
    
    if (string.IsNullOrWhiteSpace(input)) 
        continue;
        
    if (input.Equals("sair", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("👋 Encerrando Zoop AI Analyst. Até logo!");
        break;
    }

    try
    {
        // Comandos simples diretos (sem IA)
        if (input.Equals("listar reclamações", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("listar", StringComparison.OrdinalIgnoreCase))
        {
            var listResult = await kernel.InvokeAsync("Disputes", "ListDisputes"); // Mudei para listResult
            Console.WriteLine("📋 " + listResult?.ToString());
            continue;
        }

        // Roteamento inteligente para outros comandos
        var routeResult = await router.RouteAsync(input);
        var plugin = routeResult.plugin;
        var function = routeResult.function;
        var routeArgs = routeResult.args;

        // Se o roteamento não encontrou um comando direto (como 'listar'), assumimos que é uma reclamação
        if (plugin is null || function is null || function == "AddDispute") 
        {
            Console.WriteLine("⚡ Iniciando análise de cobrança com IA...");

            // Chama o método do Orquestrador
            var finalResponse = await orchestrator.AnalyzeAndResolveDispute(input);

            Console.WriteLine();
            Console.WriteLine("🤖 Resposta do Zoop AI Analyst:");
            Console.WriteLine("----------------------------------------");
            Console.WriteLine(finalResponse);
            Console.WriteLine("----------------------------------------");
            
            continue;
        }

        Console.WriteLine($"⚡ Executando: {plugin}.{function}...");
        
        var invokeResult = await kernel.InvokeAsync(plugin, function, routeArgs); 
        
        var response = invokeResult?.ToString() ?? "Sem resposta";
        Console.WriteLine();
        Console.WriteLine("✅ " + response);
        Console.WriteLine();
        
        if (function == "AddDispute")
        {
            Console.WriteLine("💡 Dica: Use 'listar reclamações' para ver todas as disputas.");
            Console.WriteLine();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Console.WriteLine($"❌ Ops! Algo deu errado:");
        Console.WriteLine($"   {ex.Message}");
        Console.WriteLine();
        Console.WriteLine("💡 Tente reformular sua mensagem.");
        Console.WriteLine();
    }
}

Console.WriteLine("========================================");
Console.WriteLine("Obrigado por usar o Zoop AI Analyst! 🚀");