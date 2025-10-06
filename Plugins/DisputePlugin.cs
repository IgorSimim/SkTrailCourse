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

    [KernelFunction, Description("Registrar uma nova reclamação de cobrança indevida")]
    public async Task<string> AddDispute([Description("Texto da reclamação do cliente")] string complaint)
    {
        try
        {
            var establishment = ExtractEstablishmentOnce(complaint);

            if (ContainsOffensiveContent(establishment))
            {
                return "❌ Conteúdo inadequado detectado. Por favor, use um nome apropriado.";
            }

            if (string.IsNullOrWhiteSpace(establishment))
            {
                // Se não conseguiu extrair, pede estabelecimento
                return $"ESTABLISHMENT_REQUIRED|{complaint}";
            }

            var list = await _store.LoadListAsync<DisputeItem>(Key);

            var orchestratorResult = await _orchestrator.HandleAsync(complaint);

            var id = Guid.NewGuid().ToString("N")[..8];
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

            return GenerateConsistentResponse(record, establishment, orchestratorResult);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro em AddDispute: {ex.Message}");
            return $"❌ Erro ao registrar reclamação: {ex.Message}";
        }
    }

    [KernelFunction, Description("Registrar uma reclamação com estabelecimento informado pelo usuário")]
    public async Task<string> AddDisputeWithMerchant(
        [Description("Texto da reclamação do cliente")] string complaint, 
        [Description("Nome do estabelecimento")] string merchant)
    {
        try
        {
            if (ContainsOffensiveContent(merchant))
            {
                return "❌ Conteúdo inadequado detectado no nome do estabelecimento.";
            }

            var list = await _store.LoadListAsync<DisputeItem>(Key);

            var orchestratorResult = await _orchestrator.HandleAsync(complaint);

            var id = Guid.NewGuid().ToString("N")[..8];
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

            return $@"✅ Reclamação registrada com sucesso!

📋 ID: {id}
🏢 Estabelecimento: {merchant}
📝 Descrição: {complaint}
🤖 Ação: {orchestratorResult.Action}
📄 Resumo: {orchestratorResult.ActionSummary}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro em AddDisputeWithMerchant: {ex.Message}");
            return $"❌ Erro ao registrar reclamação: {ex.Message}";
        }
    }

    [KernelFunction, Description("Listar todas as reclamações registradas")]
    public async Task<string> ListDisputes()
    {
        try
        {
            var list = await _store.LoadListAsync<DisputeItem>(Key);
            if (list.Count == 0) 
                return "📭 Nenhuma reclamação registrada.";

            var disputes = list.Select(d => 
                $"📋 {d.Id} | [{d.Status}] {TruncateText(d.CustomerText, 50)} → {d.ActionTaken} | 🏢 {d.Merchant} | 📅 {d.CreatedAt:dd/MM/yyyy}");

            return $"📋 Lista de Reclamações ({list.Count}):\n\n" + string.Join("\n", disputes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro em ListDisputes: {ex.Message}");
            return $"❌ Erro ao listar reclamações: {ex.Message}";
        }
    }

[KernelFunction, Description("Editar uma reclamação existente")]
public async Task<string> EditDispute(
    [Description("ID da reclamação")] string id,
    [Description("Texto de correção ou atualização")] string correctionText)
{
    try
    {
        var list = await _store.LoadListAsync<DisputeItem>(Key);
        var dispute = list.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        
        if (dispute == null)
            return $"❌ Reclamação {id} não encontrada.";

        Console.WriteLine($"🔍 Editando reclamação {id}");
        Console.WriteLine($"📝 Original: {dispute.CustomerText}");
        Console.WriteLine($"🔧 Correção: {correctionText}");

        var updatedText = await PreserveContextUpdate(dispute.CustomerText, correctionText);
        var orchestratorResult = await _orchestrator.HandleAsync(updatedText, isEdit: true);
        
        // 🔧 CORREÇÃO: Extrair valor da correção também
        var correctionValue = ExtractValue(correctionText);
        var updatedAmountCents = correctionValue ?? orchestratorResult.AmountCents ?? dispute.AmountCents;
        
        var updatedMerchant = PreserveKnownMerchant(dispute.Merchant, ExtractEstablishmentOnce(updatedText));
        var updatedActionTaken = UpdateActionWithNewValues(dispute.ActionTaken, updatedMerchant, updatedAmountCents);

        var updatedDispute = dispute with { 
            CustomerText = updatedText,
            Merchant = updatedMerchant,
            AmountCents = updatedAmountCents,
            Status = "Atualizada",
            ActionTaken = updatedActionTaken
        };

        list[list.IndexOf(dispute)] = updatedDispute;
        await _store.SaveListAsync(Key, list);

        return $@"✏️ Reclamação {id} atualizada com sucesso!

📝 Nova descrição: {updatedText}
🏢 Estabelecimento: {updatedMerchant}
💰 Valor: {(updatedAmountCents.HasValue ? $"R$ {updatedAmountCents.Value / 100.0:F2}" : "Não identificado")}
🤖 Ação atualizada: {updatedActionTaken}";
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erro em EditDispute: {ex.Message}");
        return $"❌ Erro ao editar reclamação: {ex.Message}";
    }
}

    [KernelFunction, Description("Excluir uma reclamação")]
    public async Task<string> DeleteDispute([Description("ID da reclamação")] string id)
    {
        try
        {
            var list = await _store.LoadListAsync<DisputeItem>(Key);
            var dispute = list.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            
            if (dispute == null)
                return $"❌ Reclamação {id} não encontrada.";

            list.Remove(dispute);
            await _store.SaveListAsync(Key, list);

            return $@"🗑️ Reclamação excluída com sucesso!

📋 ID: {id}
📝 Descrição: {TruncateText(dispute.CustomerText, 100)}
🏢 Estabelecimento: {dispute.Merchant}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro em DeleteDispute: {ex.Message}");
            return $"❌ Erro ao excluir reclamação: {ex.Message}";
        }
    }

    [KernelFunction, Description("Mostrar detalhes de uma reclamação específica")]
    public async Task<string> ShowDispute([Description("ID da reclamação")] string id)
    {
        try
        {
            var list = await _store.LoadListAsync<DisputeItem>(Key);
            var dispute = list.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            
            if (dispute == null)
                return $"❌ Reclamação {id} não encontrada.";

            var amountText = dispute.AmountCents.HasValue 
                ? $"R$ {dispute.AmountCents.Value / 100.0:F2}" 
                : "Não identificado";

            return $@"🔍 Detalhes da Reclamação:

📋 ID: {dispute.Id}
📊 Status: {dispute.Status}
🏢 Estabelecimento: {dispute.Merchant}
💰 Valor: {amountText}
📅 Criada em: {dispute.CreatedAt:dd/MM/yyyy HH:mm}
🤖 Ação: {dispute.ActionTaken}
📝 Descrição: {dispute.CustomerText}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro em ShowDispute: {ex.Message}");
            return $"❌ Erro ao mostrar reclamação: {ex.Message}";
        }
    }

    #region Métodos Auxiliares Privados

    private string? ExtractEstablishmentOnce(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage)) 
            return null;

        // Se já contém marcação explícita
        if (userMessage.Contains("| Estabelecimento:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = userMessage.Split("| Estabelecimento:", StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1) 
                return parts[1].Trim();
        }

        var lower = userMessage.ToLowerInvariant();
        
        // Detecção manual de estabelecimentos conhecidos
        var knownMerchants = new Dictionary<string, string>
        {
            { "zoop", "Zoop" },
            { "netflix", "Netflix" },
            { "spotify", "Spotify" },
            { "amazon", "Amazon" },
            { "uber", "Uber" },
            { "ifood", "iFood" },
            { "google", "Google" },
            { "apple", "Apple" },
            { "microsoft", "Microsoft" }
        };

        foreach (var merchant in knownMerchants)
        {
            if (lower.Contains(merchant.Key))
                return merchant.Value;
        }

        return null;
    }

    private async Task<string> PreserveContextUpdate(string original, string correction)
    {
        Console.WriteLine($"🔍 Analisando contexto:");
        Console.WriteLine($"📝 Original: {original}");
        Console.WriteLine($"🔧 Correção: {correction}");

        var intent = await AnalyzeCorrectionIntent(original, correction);
        Console.WriteLine($"🤖 Intenção detectada: {intent}");

        switch (intent)
        {
            case "ADD_VALUE":
                var newValue = ExtractValue(correction);
                if (newValue.HasValue)
                {
                    var newAmount = $"R$ {newValue.Value / 100.0:F2}";
                    if (HasValueInText(original))
                    {
                        var updated = Regex.Replace(original, @"R\$\s*\d+[,.]?\d*", newAmount);
                        Console.WriteLine($"🔧 Substituindo valor: {updated}");
                        return updated;
                    }
                    else
                    {
                        var updated = $"{original} - Valor: {newAmount}";
                        Console.WriteLine($"🔧 Acrescentando valor: {updated}");
                        return updated;
                    }
                }
                break;

            case "UPDATE_VALUE":
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
                var newMerchant = ExtractEstablishmentOnce(correction);
                if (!string.IsNullOrEmpty(newMerchant))
                {
                    var updated = UpdateMerchantInText(original, newMerchant);
                    Console.WriteLine($"🔧 Atualizando merchant: {updated}");
                    return updated;
                }
                break;

            case "COMPLEMENT_INFO":
                var cleanCorrection = await ExtractComplementText(correction);
                if (!string.IsNullOrEmpty(cleanCorrection))
                {
                    var updated = $"{original} - {cleanCorrection}";
                    Console.WriteLine($"🔧 Complementando informação: {updated}");
                    return updated;
                }
                break;
        }

        return await ContextualTextUpdate(original, correction);
    }

    private async Task<string> AnalyzeCorrectionIntent(string original, string correction)
    {
        var prompt = $@"
ANALISE a intenção do usuário ao corrigir uma reclamação:

TEXTO ORIGINAL: ""{original}""
TEXTO DE CORREÇÃO: ""{correction}""

OPÇÕES DE INTENÇÃO:
- ADD_VALUE: Usuário quer ADICIONAR informação de valor que faltava
- UPDATE_VALUE: Usuário quer ATUALIZAR/CORRIGIR valor mencionado
- UPDATE_MERCHANT: Usuário quer corrigir o estabelecimento
- COMPLEMENT_INFO: Usuário quer COMPLEMENTAR com informações adicionais
- FULL_REPLACE: Usuário quer SUBSTITUIR completamente o texto

RESPONDA APENAS COM: ADD_VALUE, UPDATE_VALUE, UPDATE_MERCHANT, COMPLEMENT_INFO, FULL_REPLACE

Intenção:";

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            return result.ToString().Trim();
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
Dado o contexto original e a correção do usuário, gere um texto atualizado:

TEXTO ORIGINAL: ""{original}""
CORREÇÃO DO USUÁRIO: ""{correction}""

REGRAS:
- Preserve a intenção principal do texto original
- Incorpore as novas informações da correção
- Mantenha a clareza e contexto

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

    private async Task<string> ExtractComplementText(string correction)
    {
        var prompt = $@"
Extraia apenas a informação COMPLEMENTAR deste texto:

TEXTO: ""{correction}""

Remova referências a edições e mantenha apenas a informação nova.

INFORMAÇÃO COMPLEMENTAR:";

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt);
            return result.ToString().Trim().Trim('"');
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro na extração de complemento: {ex.Message}");
            return correction;
        }
    }

    private string GenerateConsistentResponse(DisputeItem dispute, string establishment, dynamic orchestratorResult)
    {
        return $@"✅ Reclamação registrada com sucesso!

📋 ID: {dispute.Id}
🏢 Estabelecimento: {establishment}
📝 Descrição: {dispute.CustomerText}
🤖 Ação: {orchestratorResult.Action}
📄 Resumo: {orchestratorResult.ActionSummary}

💡 Use 'listar reclamações' para ver todas as disputas.";
    }

    private bool ContainsOffensiveContent(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) 
            return false;
        
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
                return original.Replace(merchant, newMerchant, StringComparison.OrdinalIgnoreCase);
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
    try
    {
        // 🔧 MELHORIA: Mais padrões para capturar valores
        var patterns = new[]
        {
            @"r\$\s*(\d+)[,.]?(\d{2})?", // R$ 550,00 ou R$550.00
            @"(\d+)[,.]?(\d{2})?\s*reais", // 550 reais ou 550,00 reais
            @"valor.*?(\d+)[,.]?(\d{2})?", // valor de 550,00
            @"cobrança.*?(\d+)[,.]?(\d{2})?" // cobrança de 550
        };
        
        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text.ToLower(), pattern);
            if (match.Success)
            {
                var main = match.Groups[1].Value;
                var cents = match.Groups[2].Success ? match.Groups[2].Value : "00";
                
                if (int.TryParse(main + cents, out int value))
                {
                    Console.WriteLine($"🔍 Valor extraído: {value} do padrão: {pattern}");
                    return value;
                }
            }
        }
        
        return null;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erro ao extrair valor: {ex.Message}");
        return null;
    }
}

    private string PreserveKnownMerchant(string originalMerchant, string? newMerchant)
    {
        var knownMerchants = new[] { "netflix", "amazon", "spotify", "uber", "ifood", "google", "apple", "zoop" };
        
        if (!string.IsNullOrEmpty(originalMerchant) && 
            knownMerchants.Any(known => originalMerchant.ToLower().Contains(known)))
        {
            return originalMerchant;
        }
        
        return newMerchant ?? originalMerchant;
    }

   private string UpdateActionWithNewValues(string originalAction, string merchant, int? amountCents)
{
    if (!amountCents.HasValue) 
        return originalAction;
    
    var newAmount = $"R$ {amountCents.Value / 100.0:F2}";
    
    // 🔧 CORREÇÃO: Atualiza o valor de forma mais robusta
    var updated = originalAction;
    
    // Tenta encontrar e substituir qualquer padrão de valor
    if (Regex.IsMatch(updated, @"R\$\s*\d+[,.]?\d*"))
    {
        // Substitui o valor existente
        updated = Regex.Replace(updated, @"R\$\s*\d+[,.]?\d*", newAmount);
    }
    else if (Regex.IsMatch(updated, @"\d+[,.]?\d*\s*reais", RegexOptions.IgnoreCase))
    {
        // Substitui formato "550 reais"
        updated = Regex.Replace(updated, @"\d+[,.]?\d*\s*reais", $"{newAmount} reais", RegexOptions.IgnoreCase);
    }
    else
    {
        // Se não encontrou padrão, adiciona o valor no final
        updated = $"{originalAction} - {newAmount}";
    }
    
    // 🔧 CORREÇÃO: Atualiza o merchant se estiver específico
    if (!string.IsNullOrEmpty(merchant) && !merchant.Equals("desconhecido", StringComparison.OrdinalIgnoreCase))
    {
        // Remove o merchant antigo se existir
        var merchantPattern = @"([A-Za-z]+)\s*-\s*R\$\s*\d+[,.]?\d*";
        if (Regex.IsMatch(updated, merchantPattern))
        {
            updated = Regex.Replace(updated, merchantPattern, $"{merchant} - {newAmount}");
        }
        else
        {
            // Adiciona o merchant se não existir
            updated = $"{merchant} - {newAmount}";
        }
    }
    
    Console.WriteLine($"🔧 Ação atualizada: {updated}");
    return updated;
}
    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
            
        return text[..maxLength] + "...";
    }

    #endregion
}