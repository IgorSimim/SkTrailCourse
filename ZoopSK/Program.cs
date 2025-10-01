﻿using Microsoft.SemanticKernel;
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
    var modelId = Environment.GetEnvironmentVariable("AI_MODEL_ID") ?? "gemini-2.0-flash-exp";

    if (string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("GOOGLE_API_KEY não encontrada. Verifique seu arquivo .env");

    kernelBuilder.AddGoogleAIGeminiChatCompletion(
        modelId: modelId,
        apiKey: apiKey,
        apiVersion: GoogleAIVersion.V1);

    Console.WriteLine("✅ Modelo Gemini conectado com sucesso!");
    Console.WriteLine($"🤖 Modelo: {modelId}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Erro na configuração da IA: {ex.Message}");
    Console.WriteLine("💡 Verifique se a GOOGLE_API_KEY está configurada no arquivo .env");
    Environment.Exit(1);
}

var kernel = kernelBuilder.Build();
var store = new JsonMemoryStore("data");

// === Plugins ===
var orchestrator = new DisputeOrchestrator(kernel, store);
var disputes = new DisputePlugin(store, kernel, orchestrator);
var boletoLookup = new BoletoLookupPlugin();
var support = new SupportPlugin();

kernel.ImportPluginFromObject(disputes, "Disputes");
kernel.ImportPluginFromObject(boletoLookup, "BoletoLookup");
kernel.ImportPluginFromObject(support, "Support");

// Router
var router = new AIIntentRouter(kernel);

Console.WriteLine("=== 🤖 Zoop AI Analyst (MVP) ===");
Console.WriteLine("Sistema de análise automática de cobranças indevidas");
Console.WriteLine();

Console.WriteLine("📝 COMO USAR:");
Console.WriteLine("• CONSULTAR origem de cobrança:");
Console.WriteLine("  Ex: 'verifiquei uma compra de 150 reais da zoop no meu boleto'");
Console.WriteLine("  Ex: 'não reconheço essa cobrança no meu extrato'");
Console.WriteLine("• RECLAMAR de cobrança indevida:");
Console.WriteLine("  Ex: 'quero reclamar de uma cobrança indevida da Netflix'");
Console.WriteLine("  Ex: 'fraude na minha fatura'");
Console.WriteLine();

Console.WriteLine("🔧 COMANDOS DISPONÍVEIS:");
Console.WriteLine("• 'listar reclamações' - Ver todas as disputas");
Console.WriteLine("• 'listar minhas cobranças' - Ver todas as cobranças");

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

    // Comando direto para listar empresas
    if (input.Equals("listar empresas", StringComparison.OrdinalIgnoreCase))
    {
        var result = await kernel.InvokeAsync("BoletoLookup", "ListCompanies");
        Console.WriteLine("🏢 " + result?.ToString());
        continue;
    }

    // Comandos simples diretos (sem IA)
    if (input.Equals("listar reclamações", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("listar", StringComparison.OrdinalIgnoreCase))
    {
        var listResult = await kernel.InvokeAsync("Disputes", "ListDisputes");
        Console.WriteLine("📋 " + listResult?.ToString());
        continue;
    }

    try
    {
        Console.WriteLine($"🔍 Analisando: '{input}'");
        
        // Roteamento inteligente para outros comandos
        var routeResult = await router.RouteAsync(input);
        var plugin = routeResult.plugin;
        var function = routeResult.function;
        var routeArgs = routeResult.args;

        Console.WriteLine($"🎯 Roteado para: {plugin}.{function}");

        if (plugin is null || function is null)
        {
            // Fallback: se não entendeu, mostra ajuda
            Console.WriteLine("🤔 Não entendi. Tente:");
            Console.WriteLine("   • 'verifiquei uma compra no boleto' (para CONSULTAR origem)");
            Console.WriteLine("   • 'quero reclamar de uma cobrança' (para RECLAMAR)");
            Console.WriteLine("   • 'listar reclamações'");
            Console.WriteLine("   • 'listar empresas'");
            continue;
        }

        Console.WriteLine($"⚡ Executando: {plugin}.{function}...");
        
        // Caso especial para consulta de boletos (requer interação)
        if (plugin == "BoletoLookup" && function == "SearchByCustomerName")
        {
            var boletoResult = await orchestrator.HandleBoletoConsultaAsync(input);
            Console.WriteLine();
            Console.WriteLine(boletoResult);
            Console.WriteLine();
            continue;
        }

        var invokeResult = await kernel.InvokeAsync(plugin, function, routeArgs);
        
        // Formatação da resposta
        var response = invokeResult?.ToString() ?? "Sem resposta";
        Console.WriteLine();
        Console.WriteLine("✅ " + response);
        Console.WriteLine();
        
        // Dica após adicionar disputa
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