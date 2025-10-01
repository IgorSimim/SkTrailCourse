# 🤖 Zoop AI Analyst – Sistema Inteligente de Disputas

Sistema avançado de análise automática de cobranças indevidas construído com **.NET 8 + Microsoft Semantic Kernel** e **Google Gemini**.

O sistema processa reclamações de clientes em linguagem natural, rastreia transações em tempo real via API, e toma decisões automatizadas baseadas em políticas de negócio da Zoop.

---

## 🚀 Funcionalidades Principais

### 🧠 Análise Inteligente com IA
- **Processamento de linguagem natural** para entender reclamações
- **Extração automática** de valores e datas das reclamações
- **Roteamento inteligente** de comandos via AIIntentRouter
- **Orquestração completa** do fluxo de análise

### 🔍 Rastreamento de Transações
- **Integração com ZoopApiMock** para consulta de transações
- **Validação automática** de cobranças legítimas vs. fraudulentas
- **Dados completos do merchant** (nome, contato, serviço)

### ⚡ Decisões Automatizadas
- **Reembolso automático** para valores ≤ R$50,00
- **Escalação inteligente** para análise humana
- **Tickets de prioridade máxima** para casos não rastreados

### 📊 Gerenciamento Completo
- **CRUD completo** de disputas
- **Persistência em JSON** (pasta `data/`)
- **Interface CLI interativa** com comandos intuitivos

---

## 📂 Arquitetura do Sistema

```
/ZoopSK
 ├── Infra/
 │    ├── AIIntentRouter.cs      # Roteamento inteligente de comandos
 │    └── JsonMemoryStore.cs     # Persistência local em JSON
 ├── Plugins/
 │    ├── DisputePlugin.cs       # CRUD de disputas
 │    ├── DisputeOrchestrator.cs # Orquestração do fluxo de análise
 │    └── SupportPlugin.cs       # Integração com API de transações
 ├── Prompts/
 │    └── Analysis/
 │         └── skprompt.txt      # Prompt semântico para análise
 ├── data/                       # Armazenamento local de disputas
 ├── Program.cs                  # Interface CLI interativa
 └── .env                        # Configurações da API Gemini
```



---

## ⚙️ Configuração e Execução

### 1. Pré-requisitos
- **.NET 8 SDK**
- **Google AI Studio API Key** (Gemini)
- **ZoopApiMock** rodando em localhost:5000

### 2. Configurar arquivo `.env`

Crie o arquivo `.env` na raiz do projeto:

```env
GOOGLE_API_KEY=sua_chave_do_google_ai_studio
AI_MODEL_ID=gemini-2.0-flash-exp
```

### 3. Executar o ZoopApiMock

```bash
cd ../ZoopApiMock
dotnet run
```

### 4. Executar o ZoopSK

```bash
cd ZoopSK
dotnet restore
dotnet run
```

---

## 💻 Exemplos de Uso

### 🎯 Análise Automática de Reclamação

```
💬 > Não reconheço a cobrança de 39,90 da FitEasy
⚡ Iniciando análise de cobrança com IA...

🤖 Resposta do Zoop AI Analyst:
----------------------------------------
Olá! Analisei sua reclamação sobre a cobrança de R$ 39,90.

✅ TRANSAÇÃO RASTREADA:
• Merchant: Academia FitEasy
• Serviço: Mensalidade - Plano Trimestral
• Data: 20/09/2025
• Contato: suporte@fiteasy.com.br

💰 REEMBOLSO APROVADO:
Como o valor é inferior a R$ 50,00, seu reembolso provisório foi aprovado automaticamente.

Recomendo entrar em contato com a Academia FitEasy para cancelar a assinatura se necessário.
----------------------------------------
```

### 📋 Comandos Disponíveis

```
💬 > listar reclamações
📋 abc123 | [Pendente] Não reconheço a cobrança de 39,90 da FitEasy → Reembolso aprovado (em 2025-01-15 10:30:00Z)

💬 > mostrar abc123
✅ ID: abc123
Status: Pendente
Merchant: Academia FitEasy
Valor (cents): 3990
Criada em: 2025-01-15 10:30:00Z
Ação: Reembolso aprovado
Texto: Não reconheço a cobrança de 39,90 da FitEasy

💬 > atualizar abc123 para resolvida
✏️ Reclamação abc123 atualizada para 'resolvida'.
```

---

## 🎯 Fluxo de Análise Inteligente

### 1. **Processamento da Reclamação**
- Extração automática de valor e data via IA
- Chamada para `Support.RastrearTransacao`
- Validação contra base de transações

### 2. **Decisões Automatizadas**

#### ✅ **Transação Rastreada**
- **Valor ≤ R$50,00**: Reembolso automático aprovado
- **Valor > R$50,00**: Ticket para análise em 24-72h
- Fornece dados do merchant para contato direto

#### ❌ **Transação NÃO Rastreada**
- Possível fraude ou falha de conciliação
- Ticket de **prioridade máxima** criado
- Escalação para suporte humano (suporte@zoop.com.br)

### 3. **Políticas de Reembolso**
- **Até R$ 50,00**: Automático
- **R$ 50,01 - R$ 200,00**: Análise em 24h
- **Acima de R$ 200,00**: Análise em 72h

---

## 🔗 Integração com ZoopApiMock

O sistema se integra com a **ZoopApiMock** para:
- Consultar transações por valor e data
- Obter dados completos dos merchants
- Validar legitimidade das cobranças

**Endpoint utilizado:** `http://localhost:5000/api/v1/transacao/detalhes`

---

## 🛠️ Tecnologias

- **.NET 8** - Framework principal
- **Microsoft Semantic Kernel** - Orquestração de IA
- **Google Gemini** - Modelo de linguagem
- **HttpClient** - Integração com APIs
- **System.Text.Json** - Serialização
- **DotNetEnv** - Gerenciamento de variáveis

---

## 📈 Próximos Passos

- 🌐 **Interface Web** com Blazor/React
- 🔒 **Autenticação** e autorização
- 📊 **Dashboard** de métricas e KPIs
- 🗄️ **Banco de dados** real (PostgreSQL/SQL Server)
- 🔔 **Notificações** em tempo real
- 📱 **API REST** para integração externa
- 🧪 **Testes automatizados** unitários e de integração