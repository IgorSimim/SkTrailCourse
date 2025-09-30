# 📌 Zoop AI Analyst – MVP (Semantic Kernel)

Este é um **MVP de assistente inteligente de disputas** construído em **.NET + Microsoft Semantic Kernel**, usando o modelo **Google Gemini** como LLM.

O objetivo é permitir que clientes registrem **reclamações de cobrança indevida** diretamente no terminal/app. O agente entende a reclamação, aplica regras de negócio (ex.: valor baixo, confiança alta) e decide automaticamente se deve:

- Abrir um **ticket de disputa**;
- Aprovar um **reembolso provisório (simulado)**;
- Escalar para **análise humana**.

---

## 🚀 Funcionalidades

- **Registrar disputas** (texto livre do cliente).
- **Classificação automática via IA** (NLU com Semantic Kernel).
- **Aplicação de regras simples**:
    - Se valor ≤ R$50,00 e confiança alta → aprova reembolso provisório.
    - Caso contrário → abre ticket para análise.
- **Gerenciamento de disputas**: listar, mostrar detalhes, atualizar status, remover.
- **Persistência local em JSON** (pasta `data/`).
- **Roteador de intents** que entende a entrada do usuário e chama o plugin correto.

---

## 📂 Estrutura dos Arquivos

```
/SkTrailCourse
 ├── Infra/
 │    └── AIIntentRouter.cs      # Roteador de intents usando IA
 ├── Plugins/
 │    ├── DisputePlugin.cs       # Plugin principal de disputas
 │    └── DisputeOrchestrator.cs # Orquestrador de fluxo (NLU + regras + tickets/refunds simulados)
 ├── Program.cs                  # Entry point (CLI interativo)
 ├── data/                       # Armazena disputas em JSON
 └── README.md                   # Este arquivo

```

---

## 🔑 Pré-requisitos

- .NET 8 SDK
- Conta no Google AI Studio para obter a **API Key do Gemini**.

---

## ⚙️ Configuração

### 1. Criar arquivo `.env`

Na raiz do projeto, crie um arquivo chamado `.env` com o seguinte conteúdo:

```
GOOGLE_API_KEY=coloque_sua_chave_aqui
AI_MODEL_ID=gemini-2.5-flash

```

- **GOOGLE_API_KEY**: sua chave obtida no Google AI Studio.
- **AI_MODEL_ID**: modelo do Gemini a ser usado (ex.: `gemini-2.5-flash`, `gemini-1.5-pro`).

### 2. Restaurar pacotes

```bash
dotnet restore

```

### 3. Rodar o app

```bash
dotnet run

```

---

## 💻 Exemplo de Uso no Terminal

```
=== Zoop AI Analyst (MVP) ===
Digite uma reclamação, exemplo:
  'Não reconheço a cobrança de 39,90 da FitEasy'
Comandos:
  - listar reclamações
  - mostrar reclamações
Digite 'sair' para encerrar.
----------------------------------------

> Não reconheço a cobrança de 39,90 da FitEasy
📩 Reclamação registrada (id: 1a2b3c4d).
🤖 Decisão da IA: aprovar_reembolso_provisorio
Resumo: Ticket T-12345 criado e reembolso provisório R-67890 aprovado (simulado).

> listar reclamações
6d3381a2 | [Pendente] Não reconheço a cobrança de 39,90 da FitEasy → Ticket criado para análise manual. (em 2025-09-30 18:38:21Z)

> sair

```

---

## 📊 Regras de Negócio (MVP)

- **Disputa válida** → Abre ticket.
- **Valor ≤ R$50,00 e confiança ≥ 75%** → Aprova reembolso provisório.
- **Valor alto ou confiança baixa** → Escala para humano.

*(Os valores podem ser ajustados no código do `DisputeOrchestrator.cs`.)*

---

## 📦 Próximos Passos

- 🔒 Integração real com sistema de tickets da Zoop.
- 💳 Integração com API de reembolsos reais.
- 📈 Dashboards para monitorar volume de disputas e decisões.
- 🌐 Interface web (em vez de terminal).