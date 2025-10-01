using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace SkTrailCourse.Plugins;

public class SupportPlugin
{
    [KernelFunction, Description("Consultar política de reembolso")]
    public string GetRefundPolicy()
    {
        return @"📋 Política de Reembolso Zoop:

• Valores até R$ 50,00: Reembolso automático
• Valores de R$ 50,01 a R$ 200,00: Análise em 24h
• Valores acima de R$ 200,00: Análise em 72h
• Assinaturas: Cancelamento + reembolso proporcional
• Primeira reclamação: Prioridade na análise";
    }

    [KernelFunction, Description("Verificar status de uma transação")]
    public string CheckTransaction(
        [Description("ID ou descrição da transação")] string transactionId)
    {
        return $"🔍 Transação {transactionId}: Status 'Em análise' - Time especializado verificando os detalhes.";
    }

    [KernelFunction, Description("Gerar relatório simples de disputas")]
    public string GenerateReport(
        [Description("Período (hoje, semana, mes)")] string period = "hoje")
    {
        return $"📊 Relatório {period}: Use o comando 'listar reclamações' para ver as disputas atuais.";
    }
}