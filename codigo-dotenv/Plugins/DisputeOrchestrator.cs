using Microsoft.SemanticKernel;
using SkOfflineCourse.Infra;
using System.Text.Json;

namespace SkOfflineCourse.Plugins;

public class DisputeOrchestrator
{
    private readonly Kernel _kernel;
    private readonly JsonMemoryStore _store;

    public DisputeOrchestrator(Kernel kernel, JsonMemoryStore store)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public record OrchestratorResult(
        string Action,
        string ActionSummary,
        string? Merchant,
        int? AmountCents,
        string Status);

    public async Task<OrchestratorResult> HandleAsync(string customerText)
    {
        var prompt = $@"
Classifique a reclamação do cliente:
'{customerText}'

Responda em JSON:
{{
  ""isDispute"": true|false,
  ""merchant"": ""nome ou null"",
  ""amount_cents"": int|null,
  ""confidence"": 0.0-1.0
}}";

        var nluResponse = await _kernel.InvokePromptAsync(prompt);
        var nluText = nluResponse.ToString().Trim();

        try
        {
            var root = JsonDocument.Parse(nluText).RootElement;
            var isDispute = root.GetProperty("isDispute").GetBoolean();
            var merchant = root.TryGetProperty("merchant", out var pM) ? pM.GetString() : null;
            var amount = root.TryGetProperty("amount_cents", out var pA) && pA.ValueKind == JsonValueKind.Number ? pA.GetInt32() : (int?)null;
            var conf = root.TryGetProperty("confidence", out var pC) && pC.ValueKind == JsonValueKind.Number ? pC.GetDouble() : 0.0;

            return ApplyPolicy(customerText, merchant, amount, isDispute, conf);
        }
        catch
        {
            return new OrchestratorResult("abrir_ticket", "Ticket criado para análise manual.", null, null, "Pendente");
        }
    }

    private OrchestratorResult ApplyPolicy(string originalText, string? merchant, int? amountCents, bool isDispute, double confidence)
    {
        if (!isDispute)
        {
            return new OrchestratorResult("ignorar", "Não é cobrança indevida.", merchant, amountCents, "Ignorada");
        }

        if (amountCents.HasValue && amountCents.Value <= 5000 && confidence >= 0.75)
        {
            return new OrchestratorResult("aprovar_reembolso_provisorio", "Reembolso provisório aprovado (simulado).", merchant, amountCents, "Reembolso");
        }

        return new OrchestratorResult("abrir_ticket", "Ticket aberto para análise humana.", merchant, amountCents, "Pendente");
    }
}
