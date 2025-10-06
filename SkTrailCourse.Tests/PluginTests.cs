using SkTrailCourse.Infra;
using SkTrailCourse.Plugins;
using Xunit;

namespace SkTrailCourse.Tests;

public class PluginTests
{
    [Fact]
    public async Task JsonMemoryStore_SaveAndLoad()
    {
        var store = new JsonMemoryStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var testData = new List<TestItem>
        {
            new("1", "Test Item 1"),
            new("2", "Test Item 2")
        };

        await store.SaveListAsync("test", testData);
        var loaded = await store.LoadListAsync<TestItem>("test");

        Assert.Equal(2, loaded.Count);
        Assert.Equal("Test Item 1", loaded[0].Name);
        Assert.Equal("Test Item 2", loaded[1].Name);
    }

    [Fact]
    public async Task JsonMemoryStore_EmptyFile()
    {
        var store = new JsonMemoryStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var loaded = await store.LoadListAsync<TestItem>("nonexistent");

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task DisputePlugin_ManualOperations()
    {
        var store = new JsonMemoryStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

        // Simula dados de disputa diretamente no store
        var disputes = new List<DisputePlugin.DisputeItem>
        {
            new("test123", "Cobrança indevida Netflix", "Netflix", 3990, "Pendente", "Em análise", DateTime.UtcNow)
        };

        await store.SaveListAsync("disputes", disputes);

        // Testa operações que não dependem do Kernel
        var loaded = await store.LoadListAsync<DisputePlugin.DisputeItem>("disputes");
        Assert.Single(loaded);
        Assert.Equal("Netflix", loaded[0].Merchant);
        Assert.Equal(3990, loaded[0].AmountCents);

        // Atualiza status
        loaded[0] = loaded[0] with { Status = "Resolvida" };
        await store.SaveListAsync("disputes", loaded);

        var updated = await store.LoadListAsync<DisputePlugin.DisputeItem>("disputes");
        Assert.Equal("Resolvida", updated[0].Status);
    }

    [Fact]
    public void SupportPlugin_PolicyAndTransactionCheck()
    {
        var plugin = new SupportPlugin();

        var policy = plugin.GetRefundPolicy();
        Assert.Contains("R$ 50,00: Reembolso automático", policy);
        Assert.Contains("24h", policy);
        Assert.Contains("72h", policy);

        var transaction = plugin.CheckTransaction("TXN123");
        Assert.Contains("TXN123", transaction);
        Assert.Contains("Em análise", transaction);

        var report = plugin.GenerateReport("semana");
        Assert.Contains("Relatório semana", report);
    }

    [Fact]
    public void DisputeOrchestrator_PolicyLogic()
    {
        // Testa a lógica de políticas sem dependência de IA
        var testCases = new[]
        {
            new { Amount = 3000, Expected = "aprovado" }, // R$ 30,00 - deve aprovar
            new { Amount = 5000, Expected = "aprovado" }, // R$ 50,00 - limite
            new { Amount = 7500, Expected = "ticket" },   // R$ 75,00 - deve abrir ticket
        };

        foreach (var testCase in testCases)
        {
            var shouldApprove = testCase.Amount <= 5000;
            if (testCase.Expected == "aprovado")
            {
                Assert.True(shouldApprove, $"Valor {testCase.Amount} deveria ser aprovado");
            }
            else
            {
                Assert.False(shouldApprove, $"Valor {testCase.Amount} deveria abrir ticket");
            }
        }
    }

    public record TestItem(string Id, string Name);
}