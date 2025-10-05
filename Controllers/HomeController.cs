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
    // Removed manual complaint heuristics: intents are inferred via AI-only now.

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

            // Detectar se o usuário quer CONSULTA ou RECLAMAÇÃO com base em texto natural
            // Recupera contexto da sessão (ConversationState) para manter estado entre requisições
            var state = GetConversationState();
            // Armazena a resposta do usuário no histórico para preservar contexto
            state.AddToHistory(input.UserResponse, "user");
            UpdateConversationState(state);
            var detected = await DetectConfirmationViaAI(input.UserResponse);

            if (detected == ConfirmationDecision.Consult)
            {
                response.Message = "👤 Para consulta, preciso do seu CPF:";
                response.RequiresCpfInput = true;
                response.RequiresConfirmation = false;
                response.ConfirmationType = input.Type;
                // Atualiza estado na sessão
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

            // Se não detectou claramente, pede para o usuário responder com texto natural
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
        // Salva a mensagem do usuário no histórico de conversa
        state.AddToHistory(command, "user");
        UpdateConversationState(state);

        // Tratamento rápido de finalização/agradecimento: encerra a conversa
        var lowerCmdQuick = command.ToLowerInvariant();
        if (lowerCmdQuick.Contains("obrigad") || lowerCmdQuick.Contains("valeu") || lowerCmdQuick.Contains("obg") || lowerCmdQuick.Contains("obrigada"))
        {
            var farewell = new ChatResponse { Message = "👋 Obrigado! Volte sempre que precisar.", IsExit = true };
            ResetConversationState();
            return Json(farewell);
        }

        // Se o estado está 'normal' mas já temos um contexto Zoop preservado,
        // reentramos no fluxo de escolha (consultar/reclamar) para não perder o contexto.
        if (state.CurrentStep == "normal" &&
            (!string.IsNullOrWhiteSpace(state.PreviousMessage) && state.PreviousMessage.IndexOf("zoop", StringComparison.OrdinalIgnoreCase) >= 0
             || state.ExpectedResponseType == "zoop_contexto"))
        {
            // reabre a escolha Zoop
            state.CurrentStep = "aguardando_opcao_zoop";
            state.ExpectedResponseType = "zoop_detalhado";
            UpdateConversationState(state);

            var amountSummaryRe = ExtractAmountSummary(state.PreviousMessage ?? command);
            var amountTextRe = string.IsNullOrEmpty(amountSummaryRe) ? "" : $" no valor de {amountSummaryRe}";

            return Json(new ChatResponse {
                RequiresConfirmation = true,
                ConfirmationType = "zoop_detalhado",
                Message = $"🤔 Você mencionou a Zoop{amountTextRe}. O que você gostaria de fazer?\n\n🔍 CONSULTAR detalhes dos boletos (precisa do CPF)\n\n🚨 ABRIR RECLAMAÇÃO formal\n\nDigite 'consultar' ou 'reclamar':"
            });
        }

        // Análise de intenção via IA (apenas IA, sem heurísticas manuais)
        if (state.CurrentStep == "normal")
        {
            try
            {
                // Early check: finalização / agradecimento comum — tratar imediatamente
                if (Regex.IsMatch(command, "^\\s*(obrigad[oa]|valeu|tchau|até|adeus)\\b", RegexOptions.IgnoreCase))
                {
                    var farewell = new ChatResponse { Message = "👋 Obrigado! Volte sempre que precisar.", IsExit = true };
                    ResetConversationState();
                    return Json(farewell);
                }

                var intent = await AnalyzeIntentAsync(command);
                Console.WriteLine($"🤖 Intent (IA): {intent}");

                switch (intent)
                {
                    case "consultar_zoop":
                        state.CurrentStep = "aguardando_cpf";
                        state.PreviousMessage = command;
                        state.LastUpdate = DateTime.UtcNow;
                        UpdateConversationState(state);
                        return Json(new ChatResponse { RequiresCpfInput = true, Message = "👤 Para consultar boletos da Zoop, preciso do seu CPF:" });

                    case "reclamar_zoop":
                        // If the message is detailed (values, dates, boleto info), prefer asking CONSULTAR vs RECLAMAR
                        if (IsDetailedZoopContext(command) || IsDetailedZoopContext(state.PreviousMessage ?? string.Empty))
                        {
                            state.CurrentStep = "aguardando_opcao_zoop";
                            state.PreviousMessage = command;
                            state.ExpectedResponseType = "zoop_detalhado";
                            UpdateConversationState(state);
                            return Json(new ChatResponse {
                                RequiresConfirmation = true,
                                ConfirmationType = "zoop_detalhado",
                                Message = $"🤔 Você mencionou a Zoop{(string.IsNullOrEmpty(ExtractAmountSummary(command)) ? "" : " no valor de " + ExtractAmountSummary(command))}. O que você gostaria de fazer?\n\n🔍 CONSULTAR detalhes dos boletos (precisa do CPF)\n\n🚨 ABRIR RECLAMAÇÃO formal\n\nDigite 'consultar' ou 'reclamar':"
                            });
                        }

                        // Abrir reclamação automaticamente para Zoop (IA decidiu and not detailed)
                        var disputeResultZ = await _disputes.AddDispute(command + " | Estabelecimento: Zoop");
                        // resetar estado após ação final
                        ResetConversationState();
                        return Json(new ChatResponse { Message = disputeResultZ });

                    case "reclamar_outra":
                        state.CurrentStep = "aguardando_merchant";
                        state.PreviousMessage = command;
                        state.ExpectedResponseType = "merchant_required";
                        UpdateConversationState(state);
                        return Json(new ChatResponse { RequiresConfirmation = true, Message = "📝 Sobre qual estabelecimento você quer reclamar? Por favor, informe o nome da empresa:" });

                    case "finalizar":
                    case "finalizacao":
                        var farewell2 = new ChatResponse { Message = "👋 Obrigado! Volte sempre que precisar.", IsExit = true };
                        ResetConversationState();
                        return Json(farewell2);

                    case "ambiguidade_zoop":
                        // IA sinalizou ambiguidade: ofereça opções claras
                        state.CurrentStep = "aguardando_opcao_zoop";
                        state.PreviousMessage = command;
                        state.ExpectedResponseType = "zoop_detalhado";
                        UpdateConversationState(state);
                        return Json(new ChatResponse {
                            RequiresConfirmation = true,
                            ConfirmationType = "zoop_detalhado",
                            Message = "🤔 Estou em dúvida se você quer CONSULTAR boletos da Zoop ou ABRIR UMA RECLAMAÇÃO.\n\nDigite 'consultar' ou 'reclamar' (ou explique em poucas palavras):"
                        });

                    case "outro":
                    default:
                        // IA não entendeu a intenção: peça esclarecimento ao usuário
                        return Json(new ChatResponse {
                            Message = "🤔 Não consegui entender. Pode repetir de forma mais específica?\n\n" +
                                     "Exemplos:\n" +
                                     "• 'consultar boletos da Zoop'\n" + 
                                     "• 'reclamar da Netflix'\n" +
                                     "• 'listar minhas reclamações'"
                        });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao analisar intenção via IA: {ex.Message}");
                // proceed with existing logic as fallback
            }
        }

        // Caso: o usuário acabou de fazer uma consulta e agora diz que quer reclamar
        if (state.CurrentStep == "consulta_realizada")
        {
            var postConsultDecision = await DetectConfirmationViaAI(command);
            if (postConsultDecision == ConfirmationDecision.Complaint)
            {
                // Reaproveita a consulta anterior como base para a reclamação
                var complaintText = string.IsNullOrWhiteSpace(state.PreviousMessage) ? command : state.PreviousMessage + " | " + command;
                state.AddToHistory(command, "user");
                UpdateConversationState(state);

                var createResult = await _disputes.AddDispute(complaintText);
                // Após criar a disputa, não removemos completamente o estado: deixamos em 'normal'
                // mas preservamos PreviousMessage para contexto futuro.
                state.CurrentStep = "normal";
                state.ExpectedResponseType = string.Empty;
                UpdateConversationState(state);
                return Json(new ChatResponse { Message = createResult });
            }
        }

        // (Zoop-detailed handling is now performed earlier via the AI intent analyzer)

        // Caso: estamos aguardando que o usuário informe o estabelecimento
        if (state.CurrentStep == "aguardando_merchant")
        {
            var userResponse = command;

            // Permitimos QUALQUER nome informado pelo usuário (desde que não ofenda)
            var establishmentName = ExtractEstablishmentName(userResponse);

            // Se usuário digitou texto extenso, tentamos limpar e pegar o nome (primeira palavra/frase)
            if (string.IsNullOrWhiteSpace(establishmentName))
            {
                // tenta limpar pontuação e pegar as primeiras palavras
                var cleaned = Regex.Replace(userResponse, @"[^\w\s\-\.\,]", "").Trim();
                var parts = cleaned.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                establishmentName = parts.FirstOrDefault()?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            }

            if (ContainsOffensiveContent(establishmentName))
            {
                return Json(new ChatResponse {
                    RequiresConfirmation = true,
                    Message = "❌ Conteúdo inadequado detectado no nome do estabelecimento. Por favor, informe um nome apropriado."
                });
            }
            // registra o merchant no histórico
            state.AddToHistory(establishmentName, "user");
            UpdateConversationState(state);

            var previousComplaint = string.IsNullOrWhiteSpace(state.PreviousMessage) ? "" : state.PreviousMessage;
            // Usa a reclamação original + estabelecimento informado para criar disputa
            var createResult = await _disputes.AddDisputeWithMerchant(previousComplaint, establishmentName);
            // Após criar a disputa, retornamos ao estado normal mas preservamos histórico/contexto
            state.CurrentStep = "normal";
            state.ExpectedResponseType = string.Empty;
            UpdateConversationState(state);

            return Json(new ChatResponse { Message = createResult });
        }

        // Comando de saída
        if (command.Equals("sair", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            response.Message = "👋 Encerrando ZoopIA. Até logo!\n========================================";
            response.IsExit = true;
            return Json(response);
        }

        // 'listar empresas' removido - funcionalidade não necessária

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
      response.Message = "🤔 Não entendi. Por favor, seja mais específico ou escolha uma opção:\n\n" +
                   "• 'consultar' - Ver detalhes de boletos (precisa do CPF)\n" +
                   "• 'reclamar' - Abrir nova reclamação\n" + 
                   "Digite sua escolha:";
            return Json(response);
        }

        // Se estamos aguardando confirmação (por exemplo, zoop), tratar respostas curtas aqui
    if (state.CurrentStep == "aguardando_opcao_zoop")
        {
            var detected = await DetectConfirmationViaAI(command);
            if (detected == ConfirmationDecision.Consult)
            {
                // transita para aguardando_cpf
                state.CurrentStep = "aguardando_cpf";
                state.LastUpdate = DateTime.UtcNow;
                UpdateConversationState(state);
                var resp = new ChatResponse { Message = "👤 Para consulta, preciso do seu CPF:", RequiresCpfInput = true };
                return Json(resp);
            }

            if (detected == ConfirmationDecision.Complaint)
            {
                // Se já temos uma mensagem anterior detalhada (ex: o usuário acabou de descrever a cobrança),
                // use essa mensagem para criar a reclamação automaticamente.
                var previousComplaint = string.IsNullOrWhiteSpace(state.PreviousMessage) ? command : state.PreviousMessage;
                // registra no histórico que o usuário confirmou abrir reclamação
                state.AddToHistory(command, "user");
                UpdateConversationState(state);

                // Se não havia detalhes suficientes, pede descrição; caso contrário, cria a disputa
                if (string.IsNullOrWhiteSpace(previousComplaint))
                {
                    state.CurrentStep = "aguardando_detalhes_reclamacao";
                    state.LastUpdate = DateTime.UtcNow;
                    UpdateConversationState(state);
                    var resp = new ChatResponse { Message = "📝 Entendi que você quer abrir uma reclamação. Por favor, descreva o problema com mais detalhes:" };
                    return Json(resp);
                }

                var createResult = await _disputes.AddDispute(previousComplaint);
                // Após criar a disputa, retornar ao estado normal e preservar histórico
                state.CurrentStep = "normal";
                state.ExpectedResponseType = string.Empty;
                state.LastUpdate = DateTime.UtcNow;
                UpdateConversationState(state);
                return Json(new ChatResponse { Message = createResult });
            }
            // se não detectou, continua para análise normal
        }

        // Evita criação automática de disputa quando a entrada não parece ser uma reclamação clara
        // Para AddDispute, confiamos na IA/orquestrador para decidir exigir mais detalhes.
        if (string.Equals(plugin, "Disputes", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(function, "AddDispute", StringComparison.OrdinalIgnoreCase))
        {
            // Se a IA/orquestrador precisar de mais dados, o plugin retornará um marcador que tratamos abaixo.
        }

        // Caso especial para consulta de boletos (requer interação)
        if (plugin == "BoletoLookup" && function == "SearchByCustomerName")
        {
            // Para consultas de boleto, pedimos CPF em vez do nome
            response.RequiresCpfInput = true;
            response.Message += "👤 Por favor, informe seu CPF (somente números ou formato padrão) para consulta:";
            // atualiza estado da sessão para aguardando_cpf
            state.CurrentStep = "aguardando_cpf";
            state.PreviousMessage = command;
            // salva o contexto da consulta no histórico para uso posterior
            state.AddToHistory(command, "user");
            state.LastUpdate = DateTime.UtcNow;
            UpdateConversationState(state);
            return Json(response);
        }

        // PARA TODOS OS OUTROS CASOS, usar o kernel normalmente
        Console.WriteLine($"⚡ Invocando: {plugin}.{function}");
        var invokeResult = await _kernel.InvokeAsync(plugin, function, routeArgs);

        // Formatação da resposta (apenas conteúdo útil ao usuário)
        var resultText = invokeResult?.ToString() ?? "Sem resposta";

        // Se o plugin AddDispute indicar que precisa do estabelecimento, fazemos a pergunta
        if (function == "AddDispute" && resultText.StartsWith("ESTABLISHMENT_REQUIRED|"))
        {
            // Guarda a mensagem original para usar depois
            state.CurrentStep = "aguardando_merchant";
            // A parte após o marcador contém a reclamação original
            var originalComplaint = resultText.Substring("ESTABLISHMENT_REQUIRED|".Length);
            if (string.IsNullOrWhiteSpace(originalComplaint)) originalComplaint = command;
            state.PreviousMessage = originalComplaint;
            // Guarda a reclamação original no histórico para contexto futuro
            state.AddToHistory(originalComplaint, "user");
            state.ExpectedResponseType = "merchant_required";
            UpdateConversationState(state);

            response.Message = "📝 Qual estabelecimento/empresa você quer reclamar? Por favor, especifique (ex: Netflix, Spotify, loja X):";
            return Json(response);
        }

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

            // Se houver um estado de conversa na sessão, limpar estado ou anotar a busca
            var state = GetConversationState();
            if (state != null)
            {
                // Não limpamos o contexto aqui. Marcamos que uma consulta foi realizada
                // para que mensagens subsequentes possam referenciar essa consulta.
                state.CurrentStep = "consulta_realizada";
                state.LastUpdate = DateTime.UtcNow;
                // Preserve Zoop context marker so later messages still know this was a Zoop-related flow
                if (!string.IsNullOrWhiteSpace(state.PreviousMessage) && state.PreviousMessage.IndexOf("zoop", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    state.ExpectedResponseType = "zoop_contexto";
                }
                // preserva state.PreviousMessage e ConversationHistory
                UpdateConversationState(state);
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

    // IA-only: Analisa intenção do usuário (sem heurísticas locais)
    private async Task<string> AnalyzeIntentAsync(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage)) return "outro";

        var prompt = $"""
Você é um assistente que recebe mensagens de usuários sobre cobranças.
Responda APENAS com UMA das labels abaixo (apenas a palavra):

- consultar_zoop       (usuário quer apenas consultar boletos da Zoop)
- reclamar_zoop        (usuário quer abrir reclamação contra a Zoop)
- reclamar_outra       (usuário quer reclamar de outra empresa)
- finalizar            (usuário está se despedindo/ agradecendo)
- ambiguidade_zoop     (mensagem menciona Zoop mas não fica claro: consultar ou reclamar)
- outro                (qualquer outra intenção)

Mensagem: "{userMessage}"
""";

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            return result.ToString().Trim().ToLower();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro em AnalyzeIntentAsync: {ex.Message}");
            return "outro";
        }
    }

    // Usa IA para detectar se uma resposta curta indica CONSULTA ou RECLAMAÇÃO
    private async Task<ConfirmationDecision> DetectConfirmationViaAI(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText)) return ConfirmationDecision.Unknown;
        var prompt = $"""
O usuário respondeu: "{userText}"\n\nClassifique como apenas uma das opções: CONSULTA ou RECLAMAÇÃO ou AMBÍGUO. Responda só com CONSULTA, RECLAMAÇÃO ou AMBIGUO.
""";
        try
        {
            var res = await _kernel.InvokePromptAsync(prompt);
            var s = res.ToString().Trim().ToLower();
            if (s.Contains("consulta") || s.Contains("consultar")) return ConfirmationDecision.Consult;
            if (s.Contains("reclama") || s.Contains("reclamar")) return ConfirmationDecision.Complaint;
            return ConfirmationDecision.Unknown;
        }
        catch
        {
            return ConfirmationDecision.Unknown;
        }
    }

    // Extrai um resumo de valor simples (ex: R$ 715,00 ou 715,00) para exibição
    private string ExtractAmountSummary(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return string.Empty;
        var lower = message.ToLowerInvariant();
        var m = System.Text.RegularExpressions.Regex.Match(lower, @"r\$\s*\d+[.,]\d{2}");
        if (m.Success) return m.Value.ToUpper();
        var m2 = System.Text.RegularExpressions.Regex.Match(lower, @"\d+[.,]\d{2}");
        if (m2.Success) return m2.Value;
        return string.Empty;
    }

    // Detecta se a mensagem contém detalhes típicos de boletos (valores, datas, banco, linha digitável)
    private bool IsDetailedZoopContext(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var lower = message.ToLowerInvariant();

        // Palavras-chave que indicam detalhe de boleto/compra
        var keywords = new[] { "boleto", "linha digitável", "vencimento", "venc", "banco", "agência", "conta", "r$", "rs", "reais", "data", "nf", "nota", "fatura", "parcelas" };
        if (keywords.Any(k => lower.Contains(k))) return true;

        // Valores em formato numérico (ex: 150,00 ou 150.00)
        if (Regex.IsMatch(lower, @"\d+[.,]\d{2}")) return true;

        // datas simples
        if (Regex.IsMatch(lower, @"\d{1,2}/\d{1,2}/\d{2,4}")) return true;

        return false;
    }

    // Obtém ou cria um session id baseado em cookie ou header
    private string GetSessionIdFromRequest()
    {
        try
        {
            // 1) tenta header X-Session-Id
            if (Request.Headers.TryGetValue("X-Session-Id", out var headerVal) && !string.IsNullOrWhiteSpace(headerVal))
            {
                return headerVal.ToString();
            }

            // 2) tenta cookie
            if (Request.Cookies.TryGetValue("sessionId", out var cookieVal) && !string.IsNullOrWhiteSpace(cookieVal))
            {
                return cookieVal;
            }

            // 3) cria novo session id e envia cookie
            var newId = Guid.NewGuid().ToString();
            Response.Cookies.Append("sessionId", newId, new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = true, Expires = DateTimeOffset.UtcNow.AddHours(2) });
            return newId;
        }
        catch
        {
            return Guid.NewGuid().ToString();
        }
    }

    // Heurística para extrair apenas o nome do estabelecimento da resposta do usuário
    private string ExtractEstablishmentName(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var lower = text.ToLowerInvariant();

        // Checa lista simples de merchants conhecidos
        var known = new[] { "netflix", "amazon", "spotify", "uber", "ifood", "google", "apple", "zoop" };
        foreach (var k in known)
        {
            if (lower.Contains(k)) return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(k);
        }

        // Se não encontrou, tenta limpar pontuação e retornar a primeira palavra (heurística)
    var cleaned = Regex.Replace(text, @"[^a-zA-Z0-9\s]", "").Trim();
        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0];

        // Se não for claro, tenta perguntar novamente (vazio significa não extraído)
        // Em vez de retornar vazio, também pode retornar uma versão curta do texto para permitir aceitação
        var shortCandidate = parts.Length > 0 ? parts.Take(3) : parts;
        return string.Join(' ', shortCandidate);
    }

    private bool ContainsOffensiveContent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var offensiveWords = new[] { "porra", "caralho", "foda", "merda", "buceta", "cu", "puta", "viado", "corno" };
        var lower = text.ToLowerInvariant();
        return offensiveWords.Any(w => lower.Contains(w));
    }

    // ConversationState stored in ASP.NET Session
    public class ConversationState
    {
        public string CurrentStep { get; set; } = "normal";
        public string PreviousMessage { get; set; } = string.Empty;
        public string ExpectedResponseType { get; set; } = string.Empty;
        // Histórico completo da conversa (mantemos apenas últimas N entradas)
        public List<string> ConversationHistory { get; set; } = new();
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

        // Adiciona uma mensagem ao histórico e mantém o tamanho limitado
        public void AddToHistory(string message, string sender = "user")
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            ConversationHistory.Add($"{sender}: {message}");
            if (ConversationHistory.Count > 10)
                ConversationHistory.RemoveAt(0);
            LastUpdate = DateTime.UtcNow;
        }

        public string GetContextSummary()
        {
            if (ConversationHistory == null || ConversationHistory.Count == 0) return string.Empty;
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
    