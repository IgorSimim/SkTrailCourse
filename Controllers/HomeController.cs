using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using SkTrailCourse.Infra;
using SkTrailCourse.Plugins;
using System.Text;

namespace ZoopSK.Controllers;

public class HomeController : Controller
{
    private readonly Kernel _kernel;
    private readonly AIIntentRouter _router;
    private readonly DisputeOrchestrator _orchestrator;
    private readonly BoletoLookupPlugin _boletoLookup;
    private readonly DisputePlugin _disputes;
    private readonly JsonMemoryStore _store;

    public HomeController(
        Kernel kernel, 
        AIIntentRouter router, 
        DisputeOrchestrator orchestrator,
        BoletoLookupPlugin boletoLookup,
        DisputePlugin disputes,
        JsonMemoryStore store) // ← ADICIONAR JsonMemoryStore
    {
        _kernel = kernel;
        _router = router;
        _orchestrator = orchestrator;
        _boletoLookup = boletoLookup;
        _disputes = disputes;
        _store = store; // ← INJETAR O STORE
    }

    public IActionResult Index()
    {
        // Mensagem de boas-vindas igual ao terminal
        var welcomeMessage = new StringBuilder();
    welcomeMessage.AppendLine("=== 🤖 ZoopIA (MVP) ===");
        welcomeMessage.AppendLine("Sistema de análise automática de cobranças indevidas");
        welcomeMessage.AppendLine();
        welcomeMessage.AppendLine("📝 COMO USAR:");
        welcomeMessage.AppendLine("• CONSULTAR origem de cobrança:");
        welcomeMessage.AppendLine("  Ex: 'verifiquei uma compra de 150 reais da zoop no meu boleto'");
        welcomeMessage.AppendLine("  Ex: 'não reconheço essa cobrança no meu extrato'");
        welcomeMessage.AppendLine("• RECLAMAR de cobrança indevida:");
        welcomeMessage.AppendLine("  Ex: 'quero reclamar de uma cobrança indevida da Netflix'");
        welcomeMessage.AppendLine("  Ex: 'fraude na minha fatura'");
        welcomeMessage.AppendLine();
        welcomeMessage.AppendLine("🔧 COMANDOS DISPONÍVEIS:");
        welcomeMessage.AppendLine("• 'listar reclamações' - Ver todas as disputas");
        welcomeMessage.AppendLine("• 'listar empresas' - Ver empresas cadastradas");
        welcomeMessage.AppendLine("• 'mostrar ABC123' - Detalhes de uma disputa");
        welcomeMessage.AppendLine("• 'atualizar ABC123 para resolvida' - Atualizar status");
        welcomeMessage.AppendLine("• 'excluir ABC123' - Remover uma disputa");
        welcomeMessage.AppendLine("• 'sair' - Encerrar o sistema");
        welcomeMessage.AppendLine();
        welcomeMessage.AppendLine("----------------------------------------");

        ViewBag.WelcomeMessage = welcomeMessage.ToString();
        return View();
    }

[HttpPost]
public async Task<JsonResult> ProcessCommand([FromBody] ChatInput input)
{
    try
    {
        var response = new ChatResponse();
        
        if (string.IsNullOrWhiteSpace(input.Command))
        {
            response.Message = "❌ Comando vazio.";
            return Json(response);
        }

        var command = input.Command.Trim();
        Console.WriteLine($"📥 Comando recebido: '{command}'");

        // Comando de saída
        if (command.Equals("sair", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            response.Message = "👋 Encerrando ZoopIA. Até logo!\n========================================";
            response.IsExit = true;
            return Json(response);
        }

        // Comando direto para listar empresas
        if (command.Equals("listar empresas", StringComparison.OrdinalIgnoreCase))
        {
            var result = await _kernel.InvokeAsync("BoletoLookup", "ListCompanies");
            response.Message = "🏢 " + result.ToString();
            return Json(response);
        }

        // Comandos simples diretos (sem IA)
        if (command.Equals("listar reclamações", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("listar", StringComparison.OrdinalIgnoreCase))
        {
            var listResult = await _kernel.InvokeAsync("Disputes", "ListDisputes");
            response.Message = "📋 " + listResult.ToString();
            return Json(response);
        }

        // Processamento com IA
        response.Message += $"🔍 Analisando: '{command}'\n";
        
        var routeResult = await _router.RouteAsync(command);
        var plugin = routeResult.plugin;
        var function = routeResult.function;
        var routeArgs = routeResult.args;

        Console.WriteLine($"🎯 Roteamento definido: {plugin}.{function}");
        response.Message += $"🎯 Roteado para: {plugin}.{function}\n";

        if (plugin is null || function is null)
        {
            response.Message += "🤔 Não entendi. Tente:\n";
            response.Message += "   • 'verifiquei uma compra no boleto' (para CONSULTAR origem)\n";
            response.Message += "   • 'quero reclamar de uma cobrança' (para RECLAMAR)\n";
            response.Message += "   • 'listar reclamações'\n";
            response.Message += "   • 'listar empresas'\n";
            return Json(response);
        }

        response.Message += $"⚡ Executando: {plugin}.{function}...\n";
        
        // Caso especial para consulta de boletos (requer interação)
        if (plugin == "BoletoLookup" && function == "SearchByCustomerName")
        {
            response.RequiresNameInput = true;
            response.Message += "👤 Por favor, informe seu nome completo para consulta:";
            return Json(response);
        }

        // PARA TODOS OS OUTROS CASOS, usar o kernel normalmente
        Console.WriteLine($"⚡ Invocando: {plugin}.{function}");
        var invokeResult = await _kernel.InvokeAsync(plugin, function, routeArgs);
        
        // Formatação da resposta
        var resultText = invokeResult?.ToString() ?? "Sem resposta";
        response.Message += $"\n✅ {resultText}\n";
        
        // Dica após adicionar disputa
        if (function == "AddDispute")
        {
            response.Message += "\n💡 Dica: Use 'listar reclamações' para ver todas as disputas.\n";
        }

        Console.WriteLine($"📤 Resposta enviada para o cliente");
        return Json(response);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erro no ProcessCommand: {ex}");
        var response = new ChatResponse
        {
            Message = $"\n❌ Ops! Algo deu errado:\n   {ex.Message}\n\n💡 Tente reformular sua mensagem.\n"
        };
        return Json(response);
    }
}
    [HttpPost]
    public async Task<JsonResult> SearchBoletos([FromBody] NameInput input)
    {
        try
        {
            var response = new ChatResponse();
            
            if (string.IsNullOrWhiteSpace(input.CustomerName))
            {
                response.Message = "❌ Nome não informado.";
                return Json(response);
            }

            response.Message += $"🔍 Consultando boletos para: {input.CustomerName}...\n";
            
            var result = await _boletoLookup.SearchByCustomerName(input.CustomerName);
            response.Message += $"\n{result}\n";

            return Json(response);
        }
        catch (Exception ex)
        {
            var response = new ChatResponse
            {
                Message = $"\n❌ Erro na consulta: {ex.Message}\n"
            };
            return Json(response);
        }
    }
}

// Model classes
public class ChatInput
{
    public string Command { get; set; } = string.Empty;
}

public class NameInput
{
    public string CustomerName { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public bool RequiresNameInput { get; set; }
    public bool IsExit { get; set; }
}