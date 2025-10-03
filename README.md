# 🤖 ZoopIA – Sistema Inteligente de Análise de Cobranças

Sistema avançado de análise automática de cobranças e disputas construído com **.NET 8 + Microsoft Semantic Kernel** e **Google Gemini**.

O sistema processa reclamações de clientes em linguagem natural, consulta boletos em tempo real, e toma decisões automatizadas baseadas em políticas de negócio da Zoop.

## 🌐 Interface Web Moderna

**Nova funcionalidade!** O ZoopIA agora possui uma interface web completa e intuitiva:

- **Terminal interativo** com design moderno inspirado no tema Zoop
- **Chat em tempo real** para interação natural com a IA
- **Consulta de boletos** com entrada de CPF integrada
- **Cores inteligentes** que se adaptam ao tipo de resposta (sucesso, erro, aviso)
- **Design responsivo** que funciona em desktop e mobile
- **Comandos de ajuda** integrados com modal explicativo

---

## 🚀 Funcionalidades Principais

### 🌐 Interface Web Completa
- **Terminal web interativo** com design moderno e responsivo
- **Chat em tempo real** para comunicação natural com a IA
- **Entrada de CPF integrada** para consultas de boletos
- **Sistema de cores inteligente** (sucesso, erro, aviso, informação)
- **Modal de ajuda** com todos os comandos disponíveis
- **Limpeza de terminal** e controles de interface intuitivos

### 🧠 Análise Inteligente com IA
- **Processamento de linguagem natural** para entender reclamações e consultas
- **Roteamento inteligente** via AIIntentRouter que distingue entre consultas e reclamações
- **Extração automática** de valores, datas e estabelecimentos das reclamações
- **Orquestração completa** do fluxo de análise com DisputeOrchestrator

### 🔍 Consulta de Boletos
- **Sistema completo de lookup** de boletos por CPF do cliente
- **Busca flexível** com remoção de acentos e correspondência parcial
- **Base de dados JSON** com empresas e boletos cadastrados
- **Identificação automática** quando Zoop é intermediária de pagamento

### ⚡ Decisões Automatizadas
- **Reembolso automático** para valores ≤ R$50,00
- **Escalação inteligente** para análise humana em valores maiores
- **Tickets de prioridade** baseados no valor e confiança da análise
- **Políticas configuráveis** de reembolso por faixa de valor

### 📊 Gerenciamento Completo de Disputas
- **CRUD completo** de disputas (criar, listar, atualizar, excluir, mostrar)
- **Persistência em JSON** (pasta `data/`)
- **Interface web e CLI** com comandos intuitivos
- **Histórico completo** de ações e status

---

## 📂 Arquitetura do Sistema

```
/SkTrailCourse
 ├── Controllers/
 │    └── HomeController.cs      # Controller MVC para interface web
 ├── Views/
 │    └── Home/
 │         └── Index.cshtml      # Interface web principal
 ├── wwwroot/
 │    ├── img/
 │    │    └── logo-zoop.webp    # Logo da Zoop
 │    └── js/
 │         └── chat.js           # JavaScript para chat (legado)
 ├── Infra/
 │    ├── AIIntentRouter.cs      # Roteamento inteligente de comandos
 │    └── JsonMemoryStore.cs     # Persistência local em JSON
 ├── Plugins/
 │    ├── DisputePlugin.cs       # CRUD completo de disputas
 │    ├── DisputeOrchestrator.cs # Orquestração e políticas de negócio
 │    ├── BoletoLookupPlugin.cs  # Consulta de boletos e empresas
 │    └── SupportPlugin.cs       # Políticas e relatórios de suporte
 ├── SkTrailCourse.Tests/        # Testes unitários
 ├── data/
 │    ├── disputes.json          # Armazenamento de disputas
 │    └── boletos.json          # Base de boletos e empresas
 ├── Program.cs                  # Aplicação web ASP.NET Core
 └── .env                        # Configurações da API Gemini
```

---

## ⚙️ Configuração e Execução

### 1. Pré-requisitos
- **.NET 8 SDK**
- **Google AI Studio API Key** (Gemini)

### 2. Configurar arquivo `.env`

Crie o arquivo `.env` na raiz do projeto:

```env
GOOGLE_API_KEY=sua_chave_do_google_ai_studio
AI_MODEL_ID=gemini-2.5-flash
```

### 3. Executar o sistema

```bash
cd SkTrailCourse
dotnet restore
dotnet run
```

**Acesse a interface web em:** `https://localhost:5001` ou `http://localhost:5000`

### 4. Executar testes

```bash
cd SkTrailCourse.Tests
dotnet test
```

---

## 💻 Exemplos de Uso

### 🌐 Interface Web

A interface web oferece uma experiência moderna e intuitiva:

1. **Acesse** `https://localhost:5001` após executar o projeto
2. **Digite comandos** no terminal interativo
3. **Use o botão de ajuda** (❔) para ver todos os comandos disponíveis
4. **Aproveite as cores inteligentes** que destacam diferentes tipos de resposta

### 🔍 Consulta de Boletos

**Na interface web:**
```
💬 > verifiquei uma compra de 150 reais no meu boleto
🔍 Analisando: 'verifiquei uma compra de 150 reais no meu boleto'
👤 Por favor, informe seu CPF (somente números ou formato padrão) para consulta:
[Campo de CPF aparece automaticamente]

✅ Encontramos 2 boleto(s) para o CPF informado:

📄 Boleto BLT_2024001 - R$ 150,00 (vencimento 2024-12-10)
   Emitido por: Zoop Tech Ltda
   Contato: financeiro@zoop.com.br
   Status: pendente
   📝 Descrição: Assinatura mensal Zoop Pro
```

### 🎯 Análise Automática de Reclamação

```
💬 > Não reconheço a cobrança de 39,90 da Netflix
🔍 Analisando: 'Não reconheço a cobrança de 39,90 da Netflix'
🎯 Roteado para: Disputes.AddDispute
⚡ Executando: Disputes.AddDispute...

✅ 📩 Reclamação registrada (id: abc12345).
🤖 Decisão da IA: aprovar_reembolso_provisorio
Resumo: ✅ Reembolso automático para Netflix - R$ 39,90

💡 Dica: Use 'listar reclamações' para ver todas as disputas.
```

### 📋 Comandos de Gerenciamento

```
💬 > listar reclamações
📋 abc12345 | [Reembolso Aprovado] Não reconheço a cobrança de 39,90 da Netflix → ✅ Reembolso automático para Netflix - R$ 39,90 (em 2024-12-15 14:30:00Z)

💬 > mostrar abc12345
✅ ID: abc12345
Status: Reembolso Aprovado
Merchant: Netflix
Valor (cents): 3990
Criada em: 2024-12-15 14:30:00Z
Ação: ✅ Reembolso automático para Netflix - R$ 39,90
Texto: Não reconheço a cobrança de 39,90 da Netflix

💬 > atualizar abc12345 para Resolvida
✅ ✏️ Reclamação abc12345 atualizada para 'Resolvida'.
```

---

## 🎯 Fluxo de Análise Inteligente

### 1. **Roteamento Inteligente (AIIntentRouter)**
- Analisa a entrada do usuário com IA
- Distingue entre **consultas** (BoletoLookup) e **reclamações** (Disputes)
- Extrai parâmetros automaticamente
- Fallback para reclamação em caso de dúvida

### 2. **Processamento de Consultas (BoletoLookupPlugin)**
- Busca flexível por nome do cliente
- Remoção de acentos e correspondência parcial
- Identificação de Zoop como intermediária
- Dados completos da empresa emissora

### 3. **Processamento de Disputas (DisputeOrchestrator)**
- Extração de informações via IA (merchant, valor, confiança)
- Aplicação de políticas de negócio
- Decisões automatizadas baseadas em valor e confiança

### 4. **Políticas de Reembolso**
- **Até R$ 50,00 + alta confiança**: Reembolso automático
- **R$ 50,01 - R$ 200,00**: Análise manual em 24h
- **Acima de R$ 200,00**: Análise manual em 72h
- **Baixa confiança**: Sempre para análise manual

---

## 🗂️ Base de Dados

### Empresas Cadastradas
- **Zoop Tech Ltda** - Plataforma de pagamentos
- **FitEasy Academy** - Academia e esportes
- **Netflix Brasil** - Streaming
- **Colégio Viver** - Educação

### Estrutura de Boletos
```json
{
  "boleto_id": "BLT_2024001",
  "emissor_id": "emp_001",
  "valor": 150.00,
  "vencimento": "2024-12-10",
  "pagavel_para": "João Silva Santos",
  "status": "pendente",
  "descricao": "Assinatura mensal Zoop Pro"
}
```

---

## 🛠️ Tecnologias

### Backend
- **.NET 8** - Framework principal
- **ASP.NET Core MVC** - Framework web
- **Microsoft Semantic Kernel 1.65.0** - Orquestração de IA
- **Google Gemini 2.0 Flash** - Modelo de linguagem
- **System.Text.Json** - Serialização e persistência
- **DotNetEnv** - Gerenciamento de variáveis de ambiente
- **xUnit** - Framework de testes

### Frontend
- **Bootstrap 5** - Framework CSS responsivo
- **JavaScript Vanilla** - Interatividade do terminal web
- **CSS3 com Gradientes** - Design moderno com cores da Zoop
- **Responsive Design** - Compatível com desktop e mobile

---

## 🧪 Testes

O projeto inclui testes unitários abrangentes:

- **JsonMemoryStore**: Persistência e carregamento de dados
- **SupportPlugin**: Políticas e funcionalidades de suporte
- **DisputePlugin**: Operações manuais de CRUD
- **Políticas de Negócio**: Lógica de aprovação de reembolsos

```bash
# Executar todos os testes
dotnet test

# Executar com detalhes
dotnet test --verbosity normal
```

---

## 📈 Próximos Passos

### 🌐 Interface e Integração
- ✅ **Interface Web** completa e responsiva (implementada!)
- **API REST** para integração externa
- **Webhooks** para notificações em tempo real
- **PWA (Progressive Web App)** para uso offline

### 🔒 Segurança e Autenticação
- **Autenticação JWT** para usuários
- **Autorização baseada em roles**
- **Auditoria completa** de ações

### 📊 Analytics e Monitoramento
- **Dashboard** de métricas e KPIs
- **Relatórios avançados** de disputas
- **Alertas automáticos** para padrões suspeitos

### 🗄️ Infraestrutura
- **Banco de dados** real (PostgreSQL/SQL Server)
- **Cache distribuído** (Redis)
- **Containerização** com Docker
- **CI/CD** com GitHub Actions

### 🤖 IA Avançada
- **Análise de sentimento** nas reclamações
- **Detecção de fraude** com ML
- **Classificação automática** de tipos de disputa
- **Sugestões proativas** de resolução

---

*Desenvolvido com .NET 8, ASP.NET Core MVC e Microsoft Semantic Kernel*