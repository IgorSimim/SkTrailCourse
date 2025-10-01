# 🔌 Zoop API Mock

API Mock simples para simular consultas de transações da Zoop, desenvolvida em **ASP.NET Core 8**.

Esta API é usada pelo **ZoopSK** (sistema de análise de disputas) para rastrear transações e obter dados dos merchants durante o processo de análise de cobranças indevidas.

---

## 🚀 Funcionalidades

- **Endpoint de consulta de transações** por valor e data aproximada
- **Cenários pré-configurados** para testes (FitEasy, Loja XPTO)
- **Respostas realistas** com dados de merchants
- **Tratamento de casos não encontrados** (404)
- **Swagger UI** para documentação automática

---

## 📋 Endpoint Disponível

### `GET /api/v1/transacao/detalhes`

**Parâmetros:**
- `valor` (double): Valor exato da transação (ex: 39.90)
- `data_aprox` (string): Data aproximada no formato YYYY-MM-DD (ex: 2025-09-20)

**Resposta de Sucesso (200):**
```json
{
  "sucesso": true,
  "transacao_id": "ZOOPABC001",
  "valor_cobrado": 39.90,
  "data_transacao": "2025-09-20T14:00:00Z",
  "merchant": {
    "nome_fantasia": "Academia FitEasy",
    "contato_email": "suporte@fiteasy.com.br",
    "descricao_servico": "Mensalidade - Plano Trimestral"
  }
}
```

**Resposta de Erro (404):**
```json
{
  "sucesso": false,
  "mensagem": "Não foi possível rastrear uma transação com o valor R$ 99,99 na data aproximada 2025-01-01. Isso pode ser uma falha de conciliação ou fraude."
}
```

---

## 🎯 Cenários Mock Configurados

### ✅ Cenários de Sucesso

1. **Academia FitEasy**
   - Valor: `39.90`
   - Data: `2025-09-20`
   - Merchant: Academia FitEasy
   - Serviço: Mensalidade - Plano Trimestral

2. **Loja XPTO**
   - Valor: `150.00`
   - Data: `2025-09-25`
   - Merchant: Loja XPTO E-commerce
   - Serviço: Compra de Eletrônico (Pix)

### ❌ Cenários de Falha

- Qualquer combinação de valor/data não listada acima retorna 404

---

## ⚙️ Como Executar

### Pré-requisitos
- .NET 8 SDK

### Executar a API
```bash
cd ZoopApiMock
dotnet run
```

A API estará disponível em:
- **HTTP:** http://localhost:5000
- **Swagger UI:** http://localhost:5000/swagger

---

## 🔗 Integração com ZoopSK

O **ZoopSK** utiliza esta API através do `SupportPlugin.RastrearTransacao()` para:

1. Consultar transações baseadas em reclamações de clientes
2. Obter dados dos merchants para resolução de disputas
3. Validar se cobranças são legítimas ou potencialmente fraudulentas

**URL configurada no ZoopSK:** `http://localhost:5000/api/v1/transacao/detalhes`

---

## 🛠️ Tecnologias

- **ASP.NET Core 8**
- **Swagger/OpenAPI** para documentação
- **Records** para DTOs tipados
- **Controller-based API** com roteamento por atributos

---

## 📝 Próximos Passos

- 🔒 Adicionar autenticação/autorização
- 📊 Implementar logging estruturado
- 🗄️ Conectar com banco de dados real
- 🎯 Adicionar mais cenários de teste
- ⚡ Implementar cache de respostas