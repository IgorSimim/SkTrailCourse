using System.ComponentModel;
using Microsoft.SemanticKernel;
using SkOfflineCourse.Infra;

namespace SkOfflineCourse.Plugins;

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
        var list = await _store.LoadListAsync<DisputeItem>(Key);

        var orchestratorResult = await _orchestrator.HandleAsync(complaint);

        var id = Guid.NewGuid().ToString("N").Substring(0, 8);
        var record = new DisputeItem(
            Id: id,
            CustomerText: complaint,
            Merchant: orchestratorResult.Merchant ?? "desconhecido",
            AmountCents: orchestratorResult.AmountCents,
            Status: orchestratorResult.Status,
            ActionTaken: orchestratorResult.ActionSummary,
            CreatedAt: DateTime.UtcNow);

        list.Add(record);
        await _store.SaveListAsync(Key, list);

        return $@"📩 Reclamação registrada (id: {id}).
🤖 Decisão da IA: {orchestratorResult.Action}
Resumo: {orchestratorResult.ActionSummary}";
    }

    [KernelFunction, Description("Lista as reclamações registradas")]
    public async Task<string> ListDisputes()
    {
        var list = await _store.LoadListAsync<DisputeItem>(Key);
        if (list.Count == 0) return "📭 Nenhuma reclamação registrada.";

        return string.Join(Environment.NewLine,
            list.Select(d => $"{d.Id} | [{d.Status}] {d.CustomerText} → {d.ActionTaken} (em {d.CreatedAt:u})"));
    }

    [KernelFunction, Description("Atualizar status de uma reclamação")]
    public async Task<string> UpdateDisputeStatus(
        [Description("Id da reclamação")] string id,
        [Description("Novo status (Resolvida, Escalada, Reembolsada)")] string newStatus)
    {
        var list = await _store.LoadListAsync<DisputeItem>(Key);
        var idx = list.FindIndex(x => x.Id == id);
        if (idx < 0) return "❌ Não encontrei essa reclamação.";

        var old = list[idx];
        list[idx] = old with { Status = newStatus };
        await _store.SaveListAsync(Key, list);

        return $"✏️ Reclamação {id} atualizada para '{newStatus}'.";
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
Valor (cents): {item.AmountCents}
Criada em: {item.CreatedAt:u}
Ação: {item.ActionTaken}
Texto: {item.CustomerText}";
    }
}
