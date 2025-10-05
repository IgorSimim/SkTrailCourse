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

    // Heurística simples para decidir se a entrada do usuário é provavelmente uma reclamação
    private bool IsLikelyComplaint(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;

        var lowered = input.ToLowerInvariant();

        // Palavras-chave que indicam reclamação/fraude/cobrança
        var complaintKeywords = new[]
        {
            "reclam", "cobran", "fraud", "fraude", "não reconhec", "nao reconhec", "estorno", "charg", "cobrado", "cobrança", "cobranca",
            "não paguei", "nao paguei", "cobrança indevida", "cobranca indevida", "erro na cobrança", "contest",
            "não reconheço", "nao reconheco", "cobrança não", "cobranca nao"
        };

        foreach (var kw in complaintKeywords)
        {
            if (lowered.Contains(kw)) return true;
        }

        // Padrão monetário: R$ 123,45 ou apenas números seguidos de 'reais' ou 'rs'
        if (System.Text.RegularExpressions.Regex.IsMatch(lowered, @"r\$\s*\d+|\d+\s*reais|\d+,\d{2}\s*reais|rs\s*\d+"))
            return true;

        // Se a entrada for muito curta ou apenas um agradecimento/resposta curta, não é reclamação
        var shortResponses = new[] { "ok", "obrigado", "brigado", "valeu", "thanks", "thanks!", "ok!", "entendi" };
        if (shortResponses.Any(s => lowered.Equals(s))) return false;

        // Caso contrário, conservador: não assume reclamação
        return false;
    }

    public IActionResult Index()
    {
        // Mensagem de boas-vindas igual ao terminal
        var welcomeMessage = new StringBuilder();
    welcomeMessage.AppendLine("=== 🤖 ZoopIA (MVP) ===");
        welcomeMessage.AppendLine("Sistema de análise automática de cobranças indevidas");
        // welcomeMessage.AppendLine();
        // welcomeMessage.AppendLine("📝 COMO USAR:");
        // welcomeMessage.AppendLine("• CONSULTAR origem de cobrança:");
        // welcomeMessage.AppendLine("  Ex: 'verifiquei uma compra de 150 reais da zoop no meu boleto'");
        // welcomeMessage.AppendLine("  Ex: 'não reconheço essa cobrança no meu extrato'");
        // welcomeMessage.AppendLine("• RECLAMAR de cobrança indevida:");
        // welcomeMessage.AppendLine("  Ex: 'quero reclamar de uma cobrança indevida da Netflix'");
        // welcomeMessage.AppendLine("  Ex: 'fraude na minha fatura'");
        // welcomeMessage.AppendLine();
        // welcomeMessage.AppendLine("🔧 COMANDOS DISPONÍVEIS:");
        // welcomeMessage.AppendLine("• 'listar reclamações' - Ver todas as disputas");
        // welcomeMessage.AppendLine("• 'listar empresas' - Ver empresas cadastradas");
        // welcomeMessage.AppendLine("• 'mostrar ABC123' - Detalhes de uma disputa");
        // welcomeMessage.AppendLine("• 'atualizar ABC123 para resolvida' - Atualizar status");
        // welcomeMessage.AppendLine("• 'excluir ABC123' - Remover uma disputa");
        // welcomeMessage.AppendLine("• 'sair' - Encerrar o sistema");
        // welcomeMessage.AppendLine();
        welcomeMessage.AppendLine("----------------------------------------");

        ViewBag.WelcomeMessage = welcomeMessage.ToString();
        return View();
    }

    [HttpPost]
    public Task<JsonResult> ProcessConfirmation([FromBody] ConfirmationInput input)
    {
        try
        {
            var response = new ChatResponse();

            if (input == null || string.IsNullOrWhiteSpace(input.UserResponse) || string.IsNullOrWhiteSpace(input.Type))
            {
                response.Message = "❌ Confirmação inválida.";
                return Task.FromResult(Json(response));
            }

            Console.WriteLine($"📥 Processando confirmação: Type={input.Type}, UserResponse={input.UserResponse}");

            // Detectar se o usuário quer CONSULTA ou RECLAMAÇÃO com base em texto natural
            var detected = DetectConfirmationIntent(input.UserResponse);

            if (detected == ConfirmationDecision.Consult)
            {
                response.Message = "👤 Para consulta, preciso do seu CPF:";
                response.RequiresCpfInput = true;
                response.RequiresConfirmation = false;
                response.ConfirmationType = input.Type;
                return Task.FromResult(Json(response));
            }

            if (detected == ConfirmationDecision.Complaint)
            {
                response.Message = "📝 Entendi que você quer abrir uma reclamação. Por favor, descreva o problema com mais detalhes:";
                response.RequiresConfirmation = false;
                response.ConfirmationType = input.Type;
                return Task.FromResult(Json(response));
            }

            // Se não detectou claramente, pede para o usuário responder com texto natural
            response.Message = "🤔 Não consegui identificar claramente. Você prefere CONSULTAR seus boletos da Zoop ou ABRIR UMA RECLAMAÇÃO?";
            response.RequiresConfirmation = true;
            response.ConfirmationType = input.Type;
            return Task.FromResult(Json(response));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro em ProcessConfirmation: {ex}");
            var response = new ChatResponse
            {
                Message = $"\n❌ Ops! Algo deu errado ao processar a confirmação:\n   {ex.Message}\n"
            };
            return Task.FromResult(Json(response));
        }
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

        // Processamento com IA (apenas logs server-side; respostas ao usuário são limpas)
        var routeResult = await _router.RouteAsync(command);
        var plugin = routeResult.plugin;
        var function = routeResult.function;
        var routeArgs = routeResult.args;

        Console.WriteLine($"🎯 Roteamento definido: {plugin}.{function}");

        if (plugin is null || function is null)
        {
            response.Message = "🤔 Não entendi. Tente:\n" +
                               "   • 'verifiquei uma compra no boleto' (para CONSULTAR origem)\n" +
                               "   • 'quero reclamar de uma cobrança' (para RECLAMAR)\n" +
                               "   • 'listar reclamações'\n" +
                               "   • 'listar empresas'";
            return Json(response);
        }

        // Evita criação automática de disputa quando a entrada não parece ser uma reclamação clara
        if (string.Equals(plugin, "Disputes", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(function, "AddDispute", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsLikelyComplaint(command))
            {
                response.Message += "🤔 Não consegui identificar claramente uma reclamação de cobrança. Pode descrever o problema com mais detalhes (ex: 'quero reclamar de uma cobrança não reconhecida de R$ 150,00')?";
                return Json(response);
            }
        }

        // Caso especial para consulta de boletos (requer interação)
        if (plugin == "BoletoLookup" && function == "SearchByCustomerName")
        {
            // Para consultas de boleto, pedimos CPF em vez do nome
            response.RequiresCpfInput = true;
            response.Message += "👤 Por favor, informe seu CPF (somente números ou formato padrão) para consulta:";
            return Json(response);
        }

        // PARA TODOS OS OUTROS CASOS, usar o kernel normalmente
        Console.WriteLine($"⚡ Invocando: {plugin}.{function}");
        var invokeResult = await _kernel.InvokeAsync(plugin, function, routeArgs);
        
        // Formatação da resposta (apenas conteúdo útil ao usuário)
        var resultText = invokeResult?.ToString() ?? "Sem resposta";
        response.Message = resultText;

        // Dica após adicionar disputa
        if (function == "AddDispute")
        {
            response.Message += "\n\n💡 Dica: Use 'listar reclamações' para ver todas as disputas.";
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
    public async Task<JsonResult> SearchBoletos([FromBody] CpfInput input)
    {
        try
        {
            var response = new ChatResponse();
            
            if (string.IsNullOrWhiteSpace(input.CustomerCpf))
            {
                response.Message = "❌ CPF não informado.";
                return Json(response);
            }

            response.Message += $"🔍 Consultando boletos para o CPF: {input.CustomerCpf}...\n";
            
            var result = await _boletoLookup.SearchByCpf(input.CustomerCpf);
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
    
    // Coloca o enum e a função de detecção dentro da classe para visibilidade correta
    private enum ConfirmationDecision
    {
        Unknown = 0,
        Consult = 1,
        Complaint = 2
    }

    // Detecta se a resposta do usuário indica CONSULTA ou RECLAMAÇÃO
    private ConfirmationDecision DetectConfirmationIntent(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText)) return ConfirmationDecision.Unknown;

        var lowered = userText.ToLowerInvariant();

        var consultKeywords = new[] { "consult", "consultar", "ver", "verificar", "saber", "boletos", "cobrancas", "detalhes", "ver boletos", "ver cobranças" };
        var complaintKeywords = new[] { "reclam", "reclamar", "problema", "indevida", "indevido", "errada", "abrir", "reclamação", "reclamacao", "cobrança indevida", "cobranca indevida" };

        foreach (var kw in consultKeywords)
        {
            if (lowered.Contains(kw)) return ConfirmationDecision.Consult;
        }

        foreach (var kw in complaintKeywords)
        {
            if (lowered.Contains(kw)) return ConfirmationDecision.Complaint;
        }

        // Also check short natural forms
        var consultShort = new[] { "consultar", "quero consultar", "ver", "ver boletos", "ver cobranças", "ver cobrancas", "gostaria de ver" };
        var complaintShort = new[] { "reclamar", "abrir reclamação", "abrir reclamacao", "tenho um problema", "cobrança indevida", "cobranca indevida" };

        foreach (var kw in consultShort)
        {
            if (lowered.Contains(kw)) return ConfirmationDecision.Consult;
        }

        foreach (var kw in complaintShort)
        {
            if (lowered.Contains(kw)) return ConfirmationDecision.Complaint;
        }

        return ConfirmationDecision.Unknown;
    }

}

// Model classes
public class ChatInput
{
    public string Command { get; set; } = string.Empty;
}

    public class ConfirmationInput
    {
        public string Type { get; set; } = string.Empty; // ex: "zoop_intent"
        public string UserResponse { get; set; } = string.Empty; // texto natural do usuário
    }

public class NameInput
{
    public string CustomerName { get; set; } = string.Empty;
}

public class CpfInput
{
    public string CustomerCpf { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public bool RequiresNameInput { get; set; }
    public bool RequiresCpfInput { get; set; }
    public bool IsExit { get; set; }
    // Novas propriedades para confirmação
    public bool RequiresConfirmation { get; set; }
    public string? ConfirmationType { get; set; }
}
    