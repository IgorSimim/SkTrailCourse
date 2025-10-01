using Microsoft.AspNetCore.Mvc;

namespace ZoopApiMock.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TransacaoController : ControllerBase
{
    public record MerchantDetails(string nome_fantasia, string contato_email, string descricao_servico);
    public record TransacaoResponse(bool sucesso, string transacao_id, double valor_cobrado, string data_transacao, MerchantDetails merchant);
    public record ErroResponse(bool sucesso, string mensagem);


    // Endpoint: GET /api/v1/transacao/detalhes?valor={valor}&data_aprox={data}
    [HttpGet("detalhes")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TransacaoResponse))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ErroResponse))]
    public IActionResult GetDetalhesTransacao(
        [FromQuery] double valor,
        [FromQuery] string data_aprox)
    {
        // === Cenários MOCK de SUCESSO ===

        // 1. Cobrança da FitEasy (Reclamação mais comum que atacamos)
        if (valor == 39.90 && data_aprox == "2025-09-20")
        {
            var merchant = new MerchantDetails(
                nome_fantasia: "Academia FitEasy",
                contato_email: "suporte@fiteasy.com.br",
                descricao_servico: "Mensalidade - Plano Trimestral"
            );

            var response = new TransacaoResponse(
                sucesso: true,
                transacao_id: "ZOOPABC001",
                valor_cobrado: valor,
                data_transacao: "2025-09-20T14:00:00Z",
                merchant: merchant
            );

            return Ok(response); 
        }
        
        // 2. Cobrança da Loja XPTO (Outro exemplo de sucesso)
        else if (valor == 150.00 && data_aprox == "2025-09-25")
        {
            var merchant = new MerchantDetails(
                nome_fantasia: "Loja XPTO E-commerce",
                contato_email: "sac@lojaxpto.com",
                descricao_servico: "Compra de Eletrônico (Pix)"
            );

            var response = new TransacaoResponse(
                sucesso: true,
                transacao_id: "ZOOPXYZ789",
                valor_cobrado: valor,
                data_transacao: "2025-09-25T11:00:00Z",
                merchant: merchant
            );

            return Ok(response);
        }
        
        // === Cenário MOCK de FALHA (Transação Não Encontrada) ===
        else
        {
            var erro = new ErroResponse(
                sucesso: false,
                mensagem: $"Não foi possível rastrear uma transação com o valor R$ {valor:F2} na data aproximada {data_aprox}. Isso pode ser uma falha de conciliação ou fraude."
            );
            return NotFound(erro);
        }
    }
}