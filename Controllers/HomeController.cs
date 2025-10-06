using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using SkTrailCourse.Infra;
using SkTrailCourse.Plugins;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Linq;

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
        JsonMemoryStore store) 
    {
        _kernel = kernel;
        _router = router;
        _orchestrator = orchestrator;
        _boletoLookup = boletoLookup;
        _disputes = disputes;
        _store = store; 
    }

    public IActionResult Index()
    {
        var welcomeMessage = new StringBuilder();
        welcomeMessage.AppendLine("=== 🤖 ZoopIA (MVP) ===");
        welcomeMessage.AppendLine("Sistema de análise automática de cobranças indevidas");
        welcomeMessage.AppendLine("----------------------------------------");

        ViewBag.WelcomeMessage = welcomeMessage.ToString();
        return View();
    }

    [HttpPost]
    public async Task<JsonResult> ProcessConfirmation([FromBody] ConfirmationInput input)
    {
        try
        {
            var response = new ChatResponse();

            if (input == null || string.IsNullOrWhiteSpace(input.UserResponse) || string.IsNullOrWhiteSpace(input.Type))
            {
                response.Message = "❌ Confirmação inválida.";
                return Json(response);
            }

            Console.WriteLine($"📥 Processando confirmação: Type={input.Type}, UserResponse={input.UserResponse}");

            var state = GetConversationState();
            state.AddToHistory(input.UserResponse, "user");
            UpdateConversationState(state);
            
            var detected = await DetectConfirmationViaAI(input.UserResponse);

            if (detected == ConfirmationDecision.Consult)
            {
                response.Message = "👤 Para consulta, preciso do seu CPF:";
                response.RequiresCpfInput = true;
                response.RequiresConfirmation = false;
                response.ConfirmationType = input.Type;
                
                state.CurrentStep = "aguardando_cpf";
                state.LastUpdate = DateTime.UtcNow;
                state.ExpectedResponseType = input.Type;
                UpdateConversationState(state);
                return Json(response);
            }

            if (detected == ConfirmationDecision.Complaint)
            {
                response.Message = "📝 Entendi que você quer abrir uma reclamação. Por favor, descreva o problema com mais detalhes:";
                response.RequiresConfirmation = false;
                response.ConfirmationType = input.Type;
                
                state.CurrentStep = "aguardando_detalhes_reclamacao";
                state.LastUpdate = DateTime.UtcNow;
                state.ExpectedResponseType = input.Type;
                UpdateConversationState(state);
                return Json(response);
            }

            response.Message = "🤔 Não consegui identificar claramente. Você prefere CONSULTAR seus boletos da Zoop ou ABRIR UMA RECLAMAÇÃO?";
            response.RequiresConfirmation = true;
            response.ConfirmationType = input.Type;
            
            state.CurrentStep = "aguardando_opcao_zoop";
            state.LastUpdate = DateTime.UtcNow;
            state.ExpectedResponseType = input.Type;
            UpdateConversationState(state);
            return Json(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro em ProcessConfirmation: {ex}");
            var response = new ChatResponse
            {
                Message = $"\n❌ Ops! Algo deu errado ao processar a confirmação:\n   {ex.Message}\n"
            };
            return Json(response);
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
            var state = GetConversationState();
            Console.WriteLine($"📥 Comando recebido: '{command}'");
            
            state.AddToHistory(command, "user");
            UpdateConversationState(state);

            // 🔧 CORREÇÃO MELHORADA: Detecção Zoop sempre que mencionado
            var isZoopContext = ContainsZoopContext(command);
            var isDuringActiveFlow = state.CurrentStep == "aguardando_cpf" || 
                                    state.CurrentStep == "aguardando_merchant" ||
                                    state.CurrentStep == "aguardando_detalhes_reclamacao" ||
                                    state.CurrentStep == "aguardando_opcao_zoop";

            if (isZoopContext && !isDuringActiveFlow && !IsSimpleCommand(command))
            {
                state.CurrentStep = "aguardando_opcao_zoop";
                state.PreviousMessage = command;
                state.ExpectedResponseType = "zoop_intent";
                UpdateConversationState(state);

                response.RequiresConfirmation = true;
                response.ConfirmationType = "zoop_intent";
                response.Message = $"🤔 Entendi que você mencionou a Zoop. O que você gostaria de fazer?\n\n" +
                                 $"🔍 **CONSULTAR** detalhes dos boletos (precisa do CPF)\n\n" +
                                 $"🚨 **ABRIR RECLAMAÇÃO** formal contra a Zoop\n\n" +
                                 $"Digite 'consultar' ou 'reclamar':";
                return Json(response);
            }

            // 🔧 CORREÇÃO COMPLETA: Fluxo Zoop com fallback robusto
            if (state.CurrentStep == "aguardando_opcao_zoop")
            {
                var lowerCommand = command.ToLowerInvariant().Trim();
                
                // 🔧 DETECÇÃO MANUAL PRIORITÁRIA (evita dependência da IA)
                if (lowerCommand == "consultar" || lowerCommand.Contains("consultar") || lowerCommand == "consulta")
                {
                    state.CurrentStep = "aguardando_cpf";
                    state.LastUpdate = DateTime.UtcNow;
                    UpdateConversationState(state);
                    
                    response.RequiresCpfInput = true;
                    response.Message = "👤 Para consultar os boletos da Zoop, preciso do seu CPF:";
                    return Json(response);
                }
                
                if (lowerCommand == "reclamar" || lowerCommand.Contains("reclamar") || lowerCommand == "reclamação")
                {
                    // Usa a mensagem original para criar a reclamação
                    var complaintText = state.PreviousMessage;
                    var createResult = await _disputes.AddDispute(complaintText);
                    
                    state.CurrentStep = "normal";
                    state.ExpectedResponseType = string.Empty;
                    UpdateConversationState(state);
                    
                    response.Message = createResult;
                    return Json(response);
                }
                
                // 🔧 FALLBACK PARA IA (apenas se necessário)
                try
                {
                    var decision = await DetectConfirmationViaAI(command);
                    
                    if (decision == ConfirmationDecision.Consult)
                    {
                        state.CurrentStep = "aguardando_cpf";
                        state.LastUpdate = DateTime.UtcNow;
                        UpdateConversationState(state);
                        
                        response.RequiresCpfInput = true;
                        response.Message = "👤 Para consultar os boletos da Zoop, preciso do seu CPF:";
                        return Json(response);
                    }
                    
                    if (decision == ConfirmationDecision.Complaint)
                    {
                        var complaintText = state.PreviousMessage;
                        var createResult = await _disputes.AddDispute(complaintText);
                        
                        state.CurrentStep = "normal";
                        state.ExpectedResponseType = string.Empty;
                        UpdateConversationState(state);
                        
                        response.Message = createResult;
                        return Json(response);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Erro na detecção por IA, usando fallback manual: {ex.Message}");
                }
                
                // 🔧 FALLBACK FINAL: Se tudo falhar, pede entrada clara
                response.RequiresConfirmation = true;
                response.ConfirmationType = "zoop_intent";
                response.Message = "🤔 Não consegui identificar. Por favor, digite exatamente:\n\n" +
                                 "• **CONSULTAR** - para ver detalhes dos boletos\n" +
                                 "• **RECLAMAR** - para abrir uma reclamação formal";
                return Json(response);
            }

            // Comandos simples diretos (sem IA)
            var normalized = command.ToLowerInvariant().Trim();
            if (normalized == "listar reclamações" || normalized == "minhas reclamações" || normalized == "ver disputas" || normalized == "listar")
            {
                var listResult = await _disputes.ListDisputes();
                response.Message = listResult;
                return Json(response);
            }

            if (normalized == "sair" || normalized == "exit")
            {
                ResetConversationState();
                return Json(new ChatResponse { Message = "👋 Encerrando ZoopIA. Até logo!", IsExit = true });
            }

            if (normalized == "ajuda" || normalized == "help")
            {
                return Json(new ChatResponse { Message = "💡 Exemplos: 'consultar boletos da Zoop', 'reclamar da Netflix', 'listar reclamações'" });
            }

            // Tratamento rápido de finalização/agradecimento
            var lowerCmdQuick = command.ToLowerInvariant();
            if (lowerCmdQuick.Contains("obrigad") || lowerCmdQuick.Contains("valeu") || lowerCmdQuick.Contains("obg") || lowerCmdQuick.Contains("obrigada"))
            {
                var farewell = new ChatResponse { Message = "👋 Obrigado! Volte sempre que precisar.", IsExit = true };
                ResetConversationState();
                return Json(farewell);
            }

            // Processamento principal via IA
            var routeResult = await _router.RouteAsync(command);
            var plugin = routeResult.plugin;
            var function = routeResult.function;
            var routeArgs = routeResult.args;

            Console.WriteLine($"🎯 Roteamento definido pela IA: {plugin}.{function}");

            if (plugin is null || function is null)
            {
                response.Message = "🤔 Não entendi. Por favor, seja mais específico.\n\n" +
                           "Exemplos:\n" +
                           "• 'consultar boletos da Zoop'\n" +
                           "• 'reclamar da Netflix'\n" + 
                           "• 'listar minhas reclamações'\n" +
                           "• 'deletar d8794da0'\n" +
                           "• 'atualizar reclamação abc123'";
                return Json(response);
            }

            // CORREÇÃO: Evita chamar BoletoLookup sem parâmetros necessários
            if (plugin == "BoletoLookup" && function == "SearchByCustomerName")
            {
                // Em vez de chamar diretamente, pede CPF primeiro
                response.RequiresCpfInput = true;
                response.Message = "👤 Para consultar boletos, preciso do seu CPF:";
                
                state.CurrentStep = "aguardando_cpf";
                state.PreviousMessage = command;
                state.AddToHistory(command, "user");
                state.LastUpdate = DateTime.UtcNow;
                UpdateConversationState(state);
                return Json(response);
            }

            // Caso especial: AddDispute pode precisar de estabelecimento
            if (plugin == "Disputes" && function == "AddDispute")
            {
                var complaint = routeArgs.ContainsKey("complaint") ? routeArgs["complaint"]?.ToString() : command;
                var testResult = await _disputes.AddDispute(complaint ?? command);
                
                if (testResult.StartsWith("ESTABLISHMENT_REQUIRED|"))
                {
                    state.CurrentStep = "aguardando_merchant";
                    var originalComplaint = testResult.Substring("ESTABLISHMENT_REQUIRED|".Length);
                    state.PreviousMessage = originalComplaint;
                    state.ExpectedResponseType = "merchant_required";
                    UpdateConversationState(state);

                    response.Message = "📝 Qual estabelecimento/empresa você quer reclamar?";
                    return Json(response);
                }
                
                response.Message = testResult;
                return Json(response);
            }

            // Executa outras funções normalmente
            Console.WriteLine($"⚡ Invocando: {plugin}.{function}");
            // KernelResult is not defined in this project; use a general object to hold different result types
            object? invokeResultObj = null;

            if (plugin == "Disputes" && function == "AddDisputeWithMerchant")
            {
                var complaint = routeArgs.ContainsKey("complaint") ? routeArgs["complaint"]?.ToString() : command;
                var merchant = routeArgs.ContainsKey("merchant") ? routeArgs["merchant"]?.ToString() : "";

                if (string.IsNullOrEmpty(merchant))
                {
                    response.Message = "❌ Nome do estabelecimento não especificado.";
                    return Json(response);
                }

                // AddDisputeWithMerchant returns a string message; store it directly
                invokeResultObj = await _disputes.AddDisputeWithMerchant(complaint ?? command, merchant);
            }
            else
            {
                // InvokeAsync returns a kernel result/context object; keep it as object and call ToString() below
                invokeResultObj = await _kernel.InvokeWithRetryAsync(plugin, function, routeArgs);
            }

            var resultText = invokeResultObj?.ToString() ?? "Sem resposta";
            response.Message = resultText;

            // Dicas contextuais
            if (function == "AddDispute" || function == "AddDisputeWithMerchant")
            {
                response.Message += "\n\n💡 Use 'listar reclamações' para ver todas as disputas.";
            }
            else if (function == "DeleteDispute")
            {
                response.Message += "\n\n✅ Reclamação removida com sucesso.";
            }

            Console.WriteLine($"📤 Resposta enviada: {response.Message}");
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

            // Validação básica de CPF
            var cleanCpf = new string(input.CustomerCpf.Where(char.IsDigit).ToArray());
            if (cleanCpf.Length != 11)
            {
                response.Message = "❌ CPF inválido. Deve conter 11 dígitos.";
                return Json(response);
            }

            var state = GetConversationState();
            state.CurrentStep = "consulta_realizada";
            state.LastUpdate = DateTime.UtcNow;
            
            if (!string.IsNullOrWhiteSpace(state.PreviousMessage) && state.PreviousMessage.IndexOf("zoop", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                state.ExpectedResponseType = "zoop_contexto";
            }
            
            UpdateConversationState(state);

            response.Message = $"🔍 Consultando boletos para o CPF: {FormatCpf(cleanCpf)}...\n";
            
            var result = await _boletoLookup.SearchByCpf(cleanCpf);
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

    [HttpPost]
    public async Task<JsonResult> ProcessMerchant([FromBody] MerchantInput input)
    {
        try
        {
            var response = new ChatResponse();
            
            if (string.IsNullOrWhiteSpace(input.MerchantName))
            {
                response.Message = "❌ Nome do estabelecimento não informado.";
                return Json(response);
            }

            var state = GetConversationState();
            
            if (state.CurrentStep != "aguardando_merchant" || string.IsNullOrEmpty(state.PreviousMessage))
            {
                response.Message = "❌ Contexto de reclamação não encontrado.";
                return Json(response);
            }

            var merchantName = input.MerchantName.Trim();
            
            // Validação de conteúdo ofensivo
            if (ContainsOffensiveContent(merchantName))
            {
                response.Message = "❌ Conteúdo inadequado detectado no nome do estabelecimento. Por favor, informe um nome apropriado.";
                return Json(response);
            }

            state.AddToHistory(merchantName, "user");
            UpdateConversationState(state);

            // Cria a reclamação com o estabelecimento informado
            var createResult = await _disputes.AddDisputeWithMerchant(state.PreviousMessage, merchantName);
            
            // Retorna ao estado normal
            state.CurrentStep = "normal";
            state.ExpectedResponseType = string.Empty;
            UpdateConversationState(state);

            response.Message = createResult;
            return Json(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro em ProcessMerchant: {ex}");
            var response = new ChatResponse
            {
                Message = $"\n❌ Erro ao processar estabelecimento:\n   {ex.Message}\n"
            };
            return Json(response);
        }
    }

    #region Métodos Auxiliares

    private enum ConfirmationDecision
    {
        Unknown = 0,
        Consult = 1,
        Complaint = 2
    }

    private async Task<ConfirmationDecision> DetectConfirmationViaAI(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText)) 
            return ConfirmationDecision.Unknown;
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        
        try
        {
            var prompt = $"""
Analise se o usuário quer CONSULTAR boletos ou ABRIR RECLAMAÇÃO:

Texto: "{userText}"

Responda APENAS com UMA palavra: CONSULTA ou RECLAMAÇÃO
""";
            
            var res = await _kernel.InvokePromptWithRetryAsync(prompt, cancellationToken: cts.Token);
            var s = res?.ToString().Trim().ToLower() ?? string.Empty;
            
            Console.WriteLine($"🤖 Resposta da IA para confirmação: {s}");
            
            if (s.Contains("consulta")) 
                return ConfirmationDecision.Consult;
            if (s.Contains("reclama")) 
                return ConfirmationDecision.Complaint;
                
            return ConfirmationDecision.Unknown;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("⏰ Timeout na detecção por IA");
            return ConfirmationDecision.Unknown;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro na detecção por IA: {ex.Message}");
            return ConfirmationDecision.Unknown;
        }
    }

    private bool ContainsZoopContext(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
            
        var lower = text.ToLowerInvariant();
        var zoopKeywords = new[] 
        { 
            "zoop", "zoo p", "zoo.", "zoop brasil", "zoop brasil", 
            "boletos zoop", "cobrança zoop", "cobranca zoop", "empresa zoop",
            "zoop brasil que nunca", "zoop brasil que nunca ouvi", "zoop brasil que nunca ouvi falar",
            "zoop no meu boleto", "zoop no extrato", "cobrança da zoop", "cobranca da zoop"
        };
        
        return zoopKeywords.Any(keyword => lower.Contains(keyword));
    }

    private bool IsSimpleCommand(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
            
        var lower = text.ToLowerInvariant().Trim();
        var simpleCommands = new[]
        {
            "consultar", "reclamar", "listar", "sair", "ajuda", "help",
            "sim", "não", "não", "ok", "certo"
        };
        
        return simpleCommands.Contains(lower) || lower.Length < 10;
    }

    private string FormatCpf(string cpf)
    {
        if (string.IsNullOrEmpty(cpf) || cpf.Length != 11)
            return cpf;
            
        return $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";
    }

    private bool ContainsOffensiveContent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) 
            return false;
            
        var offensiveWords = new string[] { };
        var lower = text.ToLowerInvariant();
        return offensiveWords.Any(w => lower.Contains(w));
    }

    #endregion

    #region Gerenciamento de Estado da Conversação

    public class ConversationState
    {
        public string CurrentStep { get; set; } = "normal";
        public string PreviousMessage { get; set; } = string.Empty;
        public string ExpectedResponseType { get; set; } = string.Empty;
        public List<string> ConversationHistory { get; set; } = new();
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

        public void AddToHistory(string message, string sender = "user")
        {
            if (string.IsNullOrWhiteSpace(message)) 
                return;
                
            ConversationHistory.Add($"{sender}: {message}");
            if (ConversationHistory.Count > 10)
                ConversationHistory.RemoveAt(0);
                
            LastUpdate = DateTime.UtcNow;
        }

        public string GetContextSummary()
        {
            if (ConversationHistory == null || ConversationHistory.Count == 0) 
                return string.Empty;
                
            var recent = ConversationHistory.Skip(Math.Max(0, ConversationHistory.Count - 3)).ToList();
            return string.Join(" | ", recent);
        }
    }

    private ConversationState GetConversationState()
    {
        var sessionKey = "ConversationState";
        try
        {
            var json = HttpContext.Session.GetString(sessionKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                return JsonSerializer.Deserialize<ConversationState>(json) ?? new ConversationState();
            }
            
            var newState = new ConversationState();
            HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(newState));
            return newState;
        }
        catch
        {
            var fallback = new ConversationState();
            HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(fallback));
            return fallback;
        }
    }

    private void UpdateConversationState(ConversationState state)
    {
        var sessionKey = "ConversationState";
        state.LastUpdate = DateTime.UtcNow;
        HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(state));
    }

    private void ResetConversationState()
    {
        HttpContext.Session.Remove("ConversationState");
    }

    #endregion
}

// Model classes
public class ChatInput
{
    public string Command { get; set; } = string.Empty;
}

public class ConfirmationInput
{
    public string Type { get; set; } = string.Empty;
    public string UserResponse { get; set; } = string.Empty;
}

public class NameInput
{
    public string CustomerName { get; set; } = string.Empty;
}

public class CpfInput
{
    public string CustomerCpf { get; set; } = string.Empty;
}

public class MerchantInput
{
    public string MerchantName { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public bool RequiresNameInput { get; set; }
    public bool RequiresCpfInput { get; set; }
    public bool IsExit { get; set; }
    public bool RequiresConfirmation { get; set; }
    public string? ConfirmationType { get; set; }
}