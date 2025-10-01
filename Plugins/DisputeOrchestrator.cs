using Microsoft.SemanticKernel;
using SkTrailCourse.Infra;
using System.Text.Json;

namespace SkTrailCourse.Plugins;

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
        // Extrai informações usando IA de forma robusta
        var analysis = await ExtractInformationWithAI(customerText);
        
        return ApplyPolicy(
            customerText, 
            analysis.Merchant, 
            analysis.AmountCents, 
            analysis.IsDispute, 
            analysis.Confidence
        );
    }

    // NOVO MÉTODO: Consulta de boletos com interação
    public async Task<string> HandleBoletoConsultaAsync(string initialInput)
    {
        Console.Write("👤 Por favor, informe seu nome completo para consulta: ");
        var nomeCliente = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(nomeCliente))
        {
            return "❌ Nome não informado. Operação cancelada.";
        }

        Console.WriteLine($"🔍 Consultando boletos para: {nomeCliente}...");

        // Chama o plugin de consulta de boletos
        var result = await _kernel.InvokeAsync("BoletoLookup", "SearchByCustomerName", 
            new KernelArguments { ["nomeCliente"] = nomeCliente });

        return result.ToString();
    }

    private async Task<(string? Merchant, int? AmountCents, bool IsDispute, double Confidence)> 
        ExtractInformationWithAI(string customerText)
    {
        var prompt = $@"Você é um especialista em extrair informações de reclamações financeiras.

ANALISE esta reclamação e extraia:
- Nome do estabelecimento/comerciante
- Valor da transação (converta para centavos)
- Se é uma disputa legítima

RECLAMAÇÃO: ""{customerText}""

REGRAS:
1. Para merchant: extraia o nome do negócio, loja ou serviço
2. Para amount_cents: converta valores como ""R$ 35,90"" → 3590
3. Para isDispute: true para reclamações de cobrança indevida
4. Para confidence: estime a confiança da extração (0.0-1.0)

RESPOSTA EM JSON (use double quotes):
{{
    ""merchant"": ""string ou null"",
    ""amount_cents"": ""number ou null"", 
    ""isDispute"": ""boolean"",
    ""confidence"": ""number""
}}

Exemplos:
- ""Não reconheço R$ 35,90 da Netflix"" → {{""merchant"": ""Netflix"", ""amount_cents"": 3590, ""isDispute"": true, ""confidence"": 0.95}}
- ""Cobrança de 150 reais na loja"" → {{""merchant"": ""loja"", ""amount_cents"": 15000, ""isDispute"": true, ""confidence"": 0.8}}
- ""Problema com assinatura"" → {{""merchant"": null, ""amount_cents"": null, ""isDispute"": true, ""confidence"": 0.6}}";

        try
        {
            var response = await _kernel.InvokePromptAsync(prompt);
            var jsonText = CleanJsonResponse(response.ToString());

            var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            var merchant = root.GetProperty("merchant").GetString();
            
            int? amountCents = null;
            if (root.GetProperty("amount_cents").ValueKind != JsonValueKind.Null)
            {
                amountCents = root.GetProperty("amount_cents").GetInt32();
            }
            
            var isDispute = root.GetProperty("isDispute").GetBoolean();
            var confidence = root.GetProperty("confidence").GetDouble();

            return (merchant, amountCents, isDispute, confidence);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Erro na extração com IA: {ex.Message}");
            
            // Fallback conservativo
            return (null, null, true, 0.3);
        }
    }

    private string CleanJsonResponse(string response)
    {
        // Remove markdown code blocks e espaços extras
        return response
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();
    }

    private OrchestratorResult ApplyPolicy(string originalText, string? merchant, int? amountCents, bool isDispute, double confidence)
    {
        if (!isDispute)
        {
            return new OrchestratorResult(
                "ignorar", 
                "Não é cobrança indevida.", 
                merchant, 
                amountCents, 
                "Ignorada"
            );
        }

        // 🎯 POLÍTICA INTELIGENTE
        if (amountCents.HasValue)
        {
            var amountReais = amountCents.Value / 100.0;
            
            if (amountCents.Value <= 5000 && confidence >= 0.7) // Até R$ 50,00
            {
                return new OrchestratorResult(
                    "aprovar_reembolso_provisorio", 
                    $"✅ Reembolso automático para {merchant ?? "estabelecimento"} - R$ {amountReais:F2}",
                    merchant, 
                    amountCents, 
                    "Reembolso Aprovado"
                );
            }
            
            return new OrchestratorResult(
                "abrir_ticket", 
                $"📋 Análise manual necessária - {merchant ?? "Estabelecimento"} - R$ {amountReais:F2}",
                merchant, 
                amountCents, 
                "Pendente"
            );
        }

        // Sem valor identificado
        return new OrchestratorResult(
            "abrir_ticket", 
            $"📋 Análise manual - {merchant ?? "Estabelecimento não identificado"}",
            merchant, 
            null, 
            "Pendente"
        );
    }
}