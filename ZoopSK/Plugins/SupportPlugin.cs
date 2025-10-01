using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Net.Http;
using System.Threading.Tasks;

namespace SkTrailCourse.Plugins;

[Description("Serviços de suporte e rastreamento de transações da Zoop.")]
public class SupportPlugin
{
    // URL da sua API Mock
    private const string MockApiBaseUrl = "http://localhost:5000/api/v1/transacao/detalhes"; 
    private readonly HttpClient _httpClient;

    // Construtor para injeção de dependência do HttpClient
    public SupportPlugin(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    [KernelFunction]
    [Description("Busca a transação no sistema interno, usando valor e data, e retorna os dados do merchant (cliente Zoop).")]
    public async Task<string> RastrearTransacao(
        [Description("Valor exato da cobrança, como '42.00'.")]
        double valor,
        [Description("Data aproximada da cobrança no formato 'AAAA-MM-DD'.")]
        string dataAprox)
    {
        try
        {
            // 1. Monta a URL de consulta
            string url = $"{MockApiBaseUrl}?valor={valor:F2}&data_aprox={dataAprox}";

            // 2. Chama a API
            HttpResponseMessage resposta = await _httpClient.GetAsync(url);

            // 3. Verifica o Status Code
            if (resposta.IsSuccessStatusCode)
            {
                return await resposta.Content.ReadAsStringAsync();
            }
            else if (resposta.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return "{\"sucesso\": false, \"mensagem\": \"Transação não rastreada no sistema. Possível cobrança indevida, fraude ou erro de conciliação.\" }";
            }
            else
            {
                return $"{{\"sucesso\": false, \"mensagem\": \"Erro interno ao consultar a transação. Status: {(int)resposta.StatusCode}\" }}";
            }
        }
        catch (HttpRequestException) 
        {
            return "{\"sucesso\": false, \"mensagem\": \"Serviço de rastreamento indisponível. Verifique se a API Mock está rodando (localhost:5000).\" }";
        }
    }

    [KernelFunction]
    [Description("Consultar política de reembolso da Zoop.")]
    public string GetRefundPolicy()
    {
        return """
📋 Política de Reembolso Zoop:

• Valores até R$ 50,00: Reembolso automático
• Valores de R$ 50,01 a R$ 200,00: Análise em 24h
• Valores acima de R$ 200,00: Análise em 72h
• Assinaturas: Cancelamento + reembolso proporcional
• Primeira reclamação: Prioridade na análise
""";
    }
}