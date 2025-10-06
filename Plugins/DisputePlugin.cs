using System.ComponentModel;
using Microsoft.SemanticKernel;
using SkTrailCourse.Infra;
using System.Text.RegularExpressions;
using System.Globalization;

namespace SkTrailCourse.Plugins;

public class DisputePlugin
{
    private readonly JsonMemoryStore _store;
    private readonly Kernel _kernel;
    private readonly DisputeOrchestrator _orchestrator;
    private const string Key = "disputes";

    public DisputePlugin(JsonMemoryStore store, Kernel kernel, DisputeOrchestrator orchestrator)
    {
        _store = store;
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    public record DisputeItem(
        string Id,
        string CustomerText,
        string Merchant,
        int? AmountCents,
        string Status,
        string ActionTaken,
        DateTime CreatedAt);

    [KernelFunction, Description("Registrar uma reclamação de cobrança indevida")]
    public async Task<string> AddDispute([Description("Texto da reclamação do cliente")] string complaint)
    {
        // Unifica extração do estabelecimento e usa o mesmo valor em todo o fluxo
        try
        {
            // Extrai UMA vez usando heurísticas manuais (rápido e previsível)
            var establishment = ExtractEstablishmentOnce(complaint);

            // Validação de conteúdo ofensivo: rejeita nomes impróprios
            if (ContainsOffensiveContent(establishment))
            {
                return "❌ Conteúdo inadequado detectado. Por favor, use um nome apropriado.";
            }

            // Aceita qualquer estabelecimento (mesmo quando não foi extraído)
            if (string.IsNullOrWhiteSpace(establishment))
            {
                establishment = "Estabelecimento a confirmar";
            }

            var list = await _store.LoadListAsync<DisputeItem>(Key);

            // Processa com o orquestrador para obter dados auxiliares (valores, ação, resumo),
            // mas NÃO confiar no merchant retornado por ele: usaremos 'establishment' determinado acima
            var orchestratorResult = await _orchestrator.HandleAsync(complaint);

            var id = Guid.NewGuid().ToString("N").Substring(0, 8);
            var record = new DisputeItem(
                Id: id,
                CustomerText: complaint,
                Merchant: establishment,
                AmountCents: orchestratorResult.AmountCents,
                Status: orchestratorResult.Status,
                ActionTaken: orchestratorResult.ActionSummary,
                CreatedAt: DateTime.UtcNow);

            list.Add(record);
            await _store.SaveListAsync(Key, list);

            // Gera resposta final CONSISTENTE usando o mesmo estabelecimento
            var finalMessage = GenerateConsistentResponse(record, establishment, orchestratorResult);
            return finalMessage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro em AddDispute: {ex.Message}");
            return $"❌ Erro ao registrar reclamação: {ex.Message}";
        }
    }

    // Extrai estabelecimento usando heurísticas manuais primeiro e fallback para IA
    private async Task<string?> ExtractAndValidateEstablishment(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage)) return null;

        var lower = userMessage.ToLowerInvariant();

        // Detecção manual prioritária
        if (lower.Contains("zoop") || lower.Contains("zoo ") || lower == "zoo") return "Zoop";
        if (lower.Contains("netflix")) return "Netflix";
        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("amazon")) return "Amazon";

        // Tentar heurística simples com merchant conhecido
        var simple = ExtractMerchant(userMessage);
        if (!string.IsNullOrWhiteSpace(simple)) return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(simple);

        // Fallback: usar IA apenas quando as heurísticas falharem
        try
        {
            var prompt = $"""
Extraia APENAS o nome do estabelecimento desta mensagem.

REGRAS:
- "zoop" ou "zoo" -> "Zoop"
- Netflix, Spotify, Amazon -> retorne o nome
- Se não encontrar -> null

Mensagem: "{userMessage}"
Resposta (apenas nome ou null):
""";

            var result = await _kernel.InvokePromptAsync(prompt);
            var establishment = result.ToString().Trim();
            if (string.Equals(establishment, "null", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(establishment))
                return null;
            return establishment;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao extrair estabelecimento via IA: {ex.Message}");
            return null;
        }
    }

    private string GenerateConsistentResponse(DisputeItem dispute, string establishment, dynamic orchestratorResult)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📩 Reclamação registrada (id: {dispute.Id}).");
        sb.AppendLine($"📋 Estabelecimento: {establishment}");
        if (orchestratorResult != null)
        {
            try
            {
                sb.AppendLine($"🤖 Decisão da IA: {orchestratorResult.Action}");
                sb.AppendLine($"Resumo: {orchestratorResult.ActionSummary}");
            }
            catch
            {
                // ignore if dynamic properties missing
            }
        }
        return sb.ToString();
    }

    [KernelFunction, Description("Registrar uma reclamação com estabelecimento informado pelo usuário")]
    public async Task<string> AddDisputeWithMerchant([Description("Texto da reclamação do cliente")] string complaint, [Description("Nome do estabelecimento")] string merchant)
    {
        try
        {
            var list = await _store.LoadListAsync<DisputeItem>(Key);

            var orchestratorResult = await _orchestrator.HandleAsync(complaint);

            var id = Guid.NewGuid().ToString("N").Substring(0, 8);
            var record = new DisputeItem(
                Id: id,
                CustomerText: complaint,
                Merchant: merchant,
                AmountCents: orchestratorResult.AmountCents,
                Status: orchestratorResult.Status,
                ActionTaken: orchestratorResult.ActionSummary,
                CreatedAt: DateTime.UtcNow);

            list.Add(record);
            await _store.SaveListAsync(Key, list);

            return $@"📩 Reclamação registrada (id: {id}).\n📋 Estabelecimento: {merchant}\n🤖 Decisão da IA: {orchestratorResult.Action}\nResumo: {orchestratorResult.ActionSummary}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro em AddDisputeWithMerchant: {ex.Message}");
            return $"❌ Erro ao registrar reclamação: {ex.Message}";
        }
    }

    [KernelFunction, Description("Lista as reclamações registradas")]
    public async Task<string> ListDisputes()
    {
        var list = await _store.LoadListAsync<DisputeItem>(Key);
        if (list.Count == 0) return "📭 Nenhuma reclamação registrada.";

        return string.Join(Environment.NewLine,
            list.Select(d => $"{d.Id} | [{d.Status}] {d.CustomerText} → {d.ActionTaken} (em {d.CreatedAt:u})"));
    }

    [KernelFunction, Description("Editar o texto de uma reclamação existente")]
    public async Task<string> EditDispute(
        [Description("Id da reclamação")] string id,
        [Description("Texto de correção")] string correctionText)
    {
        var list = await _store.LoadListAsync<DisputeItem>(Key);
        var idx = list.FindIndex(x => x.Id == id);
        if (idx < 0) return "❌ Não encontrei essa reclamação.";

        var old = list[idx];

        Console.WriteLine($"🔍 Editando reclamação {id}");
        Console.WriteLine($"📝 Original: {old.CustomerText}");
        Console.WriteLine($"🔧 Correção: {correctionText}");

        // Usa IA para preservar contexto de forma inteligente
        string newCustomerText = await PreserveContextUpdate(old.CustomerText, correctionText);

        // Re-processa mantendo a intenção de disputa
        var orchestratorResult = await _orchestrator.HandleAsync(newCustomerText, isEdit: true);

        // PRESERVAÇÃO INTELIGENTE DOS DADOS ORIGINAIS
        var finalMerchant = PreserveKnownMerchant(old.Merchant, orchestratorResult.Merchant);
        var finalAmountCents = orchestratorResult.AmountCents ?? old.AmountCents;
        var finalStatus = "Pendente";
        var finalActionTaken = UpdateActionWithNewValues(old.ActionTaken, finalMerchant, finalAmountCents);

        list[idx] = old with
        {
            CustomerText = newCustomerText,
            Merchant = finalMerchant,
            AmountCents = finalAmountCents,
            Status = finalStatus,
            ActionTaken = finalActionTaken
        };

        await _store.SaveListAsync(Key, list);

        return $@"✏️ Reclamação {id} atualizada.

{newCustomerText}";
    }

    [KernelFunction, Description("Remover uma reclamação")]
    public async Task<string> DeleteDispute([Description("Id da reclamação")] string id)
    {
        var list = await _store.LoadListAsync<DisputeItem>(Key);
        var idx = list.FindIndex(x => x.Id == id);
        if (idx < 0) return "❌ Não encontrei essa reclamação.";

        var removed = list[idx];
        list.RemoveAt(idx);
        await _store.SaveListAsync(Key, list);

        return $"🗑️ Reclamação removida: {removed.CustomerText}";
    }

    [KernelFunction, Description("Mostrar detalhes de uma reclamação")]
    public async Task<string> ShowDispute([Description("Id da reclamação")] string id)
    {
        var list = await _store.LoadListAsync<DisputeItem>(Key);
        var item = list.FirstOrDefault(x => x.Id == id);
        if (item is null) return "❌ Não encontrei essa reclamação.";

        return $@"ID: {item.Id}
Status: {item.Status}
Merchant: {item.Merchant}
Valor: {(item.AmountCents.HasValue ? $"R$ {item.AmountCents.Value / 100.0:F2}" : "Não identificado")}
Criada em: {item.CreatedAt:u}
Ação: {item.ActionTaken}
Texto: {item.CustomerText}";
    }

    private async Task<string> PreserveContextUpdate(string original, string correction)
    {
        Console.WriteLine($"🔍 Analisando contexto:");
        Console.WriteLine($"📝 Original: {original}");
        Console.WriteLine($"🔧 Correção: {correction}");

        // CONSULTA IA para entender a intenção da correção
        var intent = await AnalyzeCorrectionIntent(original, correction);
        Console.WriteLine($"🤖 Intenção detectada: {intent}");

        switch (intent)
        {
            case "ADD_VALUE":
                // Usuário quer ADICIONAR valor ao contexto existente
                var newValue = ExtractValue(correction);
                if (newValue.HasValue)
                {
                    var newAmount = $"R$ {newValue.Value / 100.0:F2}";
                    if (HasValueInText(original))
                    {
                        // Se já tem valor, substitui
                        var updated = Regex.Replace(original, @"R\$\s*\d+[,.]?\d*", newAmount);
                        Console.WriteLine($"🔧 Substituindo valor: {updated}");
                        return updated;
                    }
                    else
                    {
                        // Se não tem valor, acrescenta
                        var updated = $"{original} - Valor: {newAmount}";
                        Console.WriteLine($"🔧 Acrescentando valor: {updated}");
                        return updated;
                    }
                }
                break;

            case "UPDATE_VALUE":
                // Usuário quer ATUALIZAR valor existente
                var updatedValue = ExtractValue(correction);
                if (updatedValue.HasValue)
                {
                    var newAmount = $"R$ {updatedValue.Value / 100.0:F2}";
                    var updated = Regex.Replace(original, @"R\$\s*\d+[,.]?\d*", newAmount);
                    if (updated != original)
                    {
                        Console.WriteLine($"🔧 Atualizando valor: {updated}");
                        return updated;
                    }
                }
                break;

            case "UPDATE_MERCHANT":
                // Usuário quer atualizar merchant
                var newMerchant = ExtractMerchant(correction);
                if (!string.IsNullOrEmpty(newMerchant))
                {
                    var updated = UpdateMerchantInText(original, newMerchant);
                    Console.WriteLine($"🔧 Atualizando merchant: {updated}");
                    return updated;
                }
                break;

            case "COMPLEMENT_INFO":
                // Usuário quer COMPLEMENTAR informação
                var cleanCorrection = await ExtractComplementText(correction);
                if (!string.IsNullOrEmpty(cleanCorrection))
                {
                    var updated = $"{original} - {cleanCorrection}";
                    Console.WriteLine($"🔧 Complementando informação: {updated}");
                    return updated;
                }
                break;
        }

        // Se não detectou intenção específica, usa análise contextual
        return await ContextualTextUpdate(original, correction);
    }

    private async Task<string> AnalyzeCorrectionIntent(string original, string correction)
    {
        var prompt = $@"
ANALISE a intenção do usuário ao corrigir uma reclamação:

TEXTO ORIGINAL: ""{original}""
TEXTO DE CORREÇÃO: ""{correction}""

OPÇÕES DE INTENÇÃO:
- ADD_VALUE: Usuário quer ADICIONAR informação de valor que faltava (quando o original não menciona valor)
- UPDATE_VALUE: Usuário quer ATUALIZAR/CORRIGIR valor mencionado (quando o original já tem valor)
- UPDATE_MERCHANT: Usuário quer corrigir o estabelecimento/merchant
- COMPLEMENT_INFO: Usuário quer COMPLEMENTAR com informações adicionais
- FULL_REPLACE: Usuário quer SUBSTITUIR completamente o texto

ANÁLISE CONTEXTUAL:
- Se a correção fala sobre valores mas o original não tem valor → ADD_VALUE
- Se a correção corrige um valor que já existe no original → UPDATE_VALUE  
- Se a correção menciona novo estabelecimento → UPDATE_MERCHANT
- Se a correção adiciona informações sem alterar o contexto principal → COMPLEMENT_INFO
- Se a correção é completamente diferente do contexto original → FULL_REPLACE

RESPONDA APENAS COM UMA DESTAS PALAVRAS: ADD_VALUE, UPDATE_VALUE, UPDATE_MERCHANT, COMPLEMENT_INFO, FULL_REPLACE

Intenção:";

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            var intent = result.ToString().Trim();
            return intent;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro na análise de intenção: {ex.Message}");
            return "FULL_REPLACE";
        }
    }

    private async Task<string> ContextualTextUpdate(string original, string correction)
    {
        var prompt = $@"
Dado o contexto original e a correção do usuário, gere um texto atualizado que preserve o contexto mas incorpore a correção:

TEXTO ORIGINAL: ""{original}""
CORREÇÃO DO USUÁRIO: ""{correction}""

REGRAS:
- Preserve a intenção principal do texto original
- Incorpore as novas informações da correção
- Mantenha a clareza e contexto
- Se a correção for sobre valores, atualize ou acrescente
- Se for informação complementar, acrescente com ""-""

TEXTO ATUALIZADO:";

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            var updatedText = result.ToString().Trim().Trim('"');
            Console.WriteLine($"🤖 Texto contextual gerado: {updatedText}");
            return updatedText;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro na atualização contextual: {ex.Message}");
            return correction;
        }
    }

    private async Task<string> ExtractEstablishmentFromMessage(string userMessage)
    {
        try
        {
            var prompt = $@"Extraia o nome do estabelecimento/empresa desta reclamação.
Se não encontrar, retorne ""não identificado"".

MENSAGEM: ""{userMessage}""

Responda APENAS com o nome do estabelecimento ou ""não identificado"".";

            var result = await _kernel.InvokePromptAsync(prompt);
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao extrair estabelecimento: {ex.Message}");
            return "não identificado";
        }
    }

    private async Task<string> ExtractComplementText(string correction)
    {
        var prompt = $@"
Extraia apenas a informação COMPLEMENTAR deste texto, removendo referências a edição:

TEXTO: ""{correction}""

Remova:
- Referências a IDs ou edições
- Palavras sobre o processo de correção
- Expressões como ""esqueci de mencionar"", ""preciso acrescentar"", ""complementando""

Mantenha apenas a informação nova que deve ser adicionada ao contexto original.

INFORMAÇÃO COMPLEMENTAR:";

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            var complement = result.ToString().Trim().Trim('"');
            return complement;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro na extração de complemento: {ex.Message}");
            return correction;
        }
    }

    // MÉTODOS AUXILIARES
    private string? ExtractMerchant(string text)
    {
        var knownMerchants = new[] { "netflix", "amazon", "spotify", "uber", "ifood", "google", "apple", "microsoft", "zoom", "zoop" };
        var lowerText = text.ToLower();

        foreach (var merchant in knownMerchants)
        {
            if (lowerText.Contains(merchant))
                return merchant;
        }
        return null;
    }

    // Extrai o estabelecimento apenas uma vez usando padrões e heurísticas simples
    private string? ExtractEstablishmentOnce(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage)) return null;

        // Se o texto já contém uma marcação explícita, usa ela
        if (userMessage.Contains("| Estabelecimento:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = userMessage.Split("| Estabelecimento:", StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1) return parts[1].Trim();
        }

        var lower = userMessage.ToLowerInvariant();
        if (lower.Contains("zoop") || lower == "zoo") return "Zoop";
        if (lower.Contains("netflix")) return "Netflix";
        if (lower.Contains("spotify")) return "Spotify";
        if (lower.Contains("amazon")) return "Amazon";

        // tenta extrair merchant conhecido
        var simple = ExtractMerchant(userMessage);
        if (!string.IsNullOrWhiteSpace(simple))
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(simple);

        // se nada encontrado, retorna null para ser tratado pelo chamador
        return null;
    }

    private bool ContainsOffensiveContent(string? text)
    {
        // Palavras potencialmente ofensivas removidas a pedido do usuário.
        // Mantemos a função para fácil reativação futura, retornando false enquanto a lista estiver vazia.
        if (string.IsNullOrWhiteSpace(text)) return false;
        var offensiveWords = new string[] { };
        var lower = text.ToLowerInvariant();
        return offensiveWords.Any(w => lower.Contains(w));
    }

    private string UpdateMerchantInText(string original, string newMerchant)
    {
        var knownMerchants = new[] { "netflix", "amazon", "spotify", "uber", "ifood", "google", "apple", "zoop" };
        var lowerOriginal = original.ToLower();

        foreach (var merchant in knownMerchants)
        {
            if (lowerOriginal.Contains(merchant))
            {
                return original.Replace(merchant, newMerchant, StringComparison.OrdinalIgnoreCase)
                              .Replace(merchant.ToLower(), newMerchant.ToLower())
                              .Replace(merchant.ToUpper(), newMerchant.ToUpper());
            }
        }
        return original;
    }

    private bool HasValueInText(string text)
    {
        return Regex.IsMatch(text, @"R\$\s*\d+[,.]?\d*") ||
               Regex.IsMatch(text.ToLower(), @"\d+\s*(reais|r\$|pila)");
    }

    private int? ExtractValue(string text)
    {
        var match = Regex.Match(text.ToLower(), @"(\d+)\s*(reais|r\$?)");
        return match.Success && int.TryParse(match.Groups[1].Value, out int val) ? val * 100 : null;
    }

    private string PreserveKnownMerchant(string originalMerchant, string? newMerchant)
    {
        var knownMerchants = new[] { "netflix", "amazon", "spotify", "uber", "ifood", "google", "apple" };

        // Se o original era um merchant conhecido, preserva
        if (!string.IsNullOrEmpty(originalMerchant) &&
            knownMerchants.Any(known => originalMerchant.ToLower().Contains(known)))
        {
            return originalMerchant;
        }

        return newMerchant ?? originalMerchant;
    }

    private string UpdateActionWithNewValues(string originalAction, string merchant, int? amountCents)
    {
        if (!amountCents.HasValue) return originalAction;

        var newAmount = $"R$ {amountCents.Value / 100.0:F2}";

        // Atualiza valor
        var updated = System.Text.RegularExpressions.Regex.Replace(
            originalAction,
            @"R\$\s*\d+[,.]?\d*",
            newAmount);

        // Atualiza o merchant se estiver específico
        if (!string.IsNullOrEmpty(merchant) && !merchant.Equals("desconhecido", StringComparison.OrdinalIgnoreCase))
        {
            updated = System.Text.RegularExpressions.Regex.Replace(
                updated,
                @"(\w+)\s*-\s*R\$\s*\d+[,.]?\d*",
                $"{merchant} - {newAmount}");
        }

        return updated;
    }
}