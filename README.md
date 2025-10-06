# 🤖 ZoopIA – Sistema Inteligente de Análise de Cobranças

Sistema avançado de análise automática de cobranças e disputas construído com **.NET 8 + Microsoft Semantic Kernel** e **Google Gemini**.

O sistema processa reclamações de clientes em linguagem natural, consulta boletos em tempo real, e toma decisões automatizadas baseadas em políticas de negócio da Zoop.

---

## 🌐 Interface Web Moderna

**Funcionalidade principal!** O ZoopIA possui uma interface web completa e intuitiva:

- **Terminal interativo** com design moderno inspirado no tema Zoop
- **Paleta de cores padronizada** (laranja #FF5C00 e rosa #FF2D92)
- **Tipografia consistente** com escala de tamanhos definida
- **CSS organizado** com variáveis CSS para fácil manutenção
- **Chat em tempo real** para interação natural com a IA
- **Consulta de boletos** com entrada de CPF integrada e automática
- **Sistema de cores inteligente** que se adapta ao tipo de resposta (sucesso, erro, aviso, info)
- **Design responsivo** otimizado para desktop e mobile
- **Modal de ajuda** com comandos disponíveis e exemplos práticos
- **Controles de terminal** (limpar, ajuda) integrados no header

---

## 🚀 Funcionalidades Principais

### 🌐 Interface Web Completa
- **Terminal web interativo** com design moderno e responsivo
- **Paleta de cores unificada** usando variáveis CSS (--color-primary, --color-secondary)
- **Tipografia padronizada** (--font-size-xs, --font-size-sm, --font-size-base, --font-size-lg)
- **Chat em tempo real** para comunicação natural com a IA
- **Entrada de CPF automática** para consultas de boletos (detecta quando necessário)
- **Sistema de cores inteligente** (sucesso ✅, erro ❌, aviso ⚠️, informação 🔍)
- **Modal de ajuda** com comandos e exemplos práticos
- **Controles de terminal** (limpar ⟳, ajuda i) no header
- **Loading states** e feedback visual durante processamento
- **Scrollbar customizada** com gradiente Zoop

### 🧠 Análise Inteligente com IA
- **Processamento de linguagem natural** para entender reclamações e consultas
- **Roteamento inteligente** via AIIntentRouter que distingue entre consultas e reclamações
- **Extração automática** de valores, datas e estabelecimentos das reclamações
- **Orquestração completa** do fluxo de análise com DisputeOrchestrator
- **Detecção de ambiguidade** para contextos Zoop (consultar vs reclamar)
- **Análise de intenção** com fallback determinístico para comandos do sistema

### 🔍 Consulta de Boletos
- **Sistema completo de lookup** de boletos por CPF do cliente
- **Busca flexível** com remoção de acentos e correspondência parcial
- **Base de dados JSON** com empresas e boletos cadastrados
- **Identificação automática** quando Zoop é intermediária de pagamento
- **Interface CPF integrada** que aparece automaticamente quando necessário
- **Validação e formatação** de CPF em tempo real

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
- **Estado de conversa** mantido via ASP.NET Session

---

## 📂 Arquitetura do Sistema

```
/SkTrailCourse
 ├── Controllers/
 │    └── HomeController.cs      # Controller MVC com APIs ProcessCommand e SearchBoletos
 ├── Views/
 │    └── Home/
 │         └── Index.cshtml      # Interface web com terminal interativo (CSS refatorado)
 ├── wwwroot/
 │    ├── css/
 │    │    └── style.css         # Estilos legados
 │    ├── img/
 │    │    ├── logo-zoop.webp    # Logo da Zoop
 │    │    └── logo.png          # Logo alternativo
 │    └── js/
 │         ├── chat.js           # JavaScript legado
 │         └── terminal.js       # JavaScript legado
 ├── Infra/
 │    ├── AIIntentRouter.cs      # Roteamento inteligente de comandos
 │    └── JsonMemoryStore.cs     # Persistência local em JSON
 ├── Plugins/
 │    ├── DisputePlugin.cs       # CRUD completo de disputas
 │    ├── DisputeOrchestrator.cs # Orquestração e políticas de negócio
 │    ├── BoletoLookupPlugin.cs  # Consulta de boletos por nome e CPF
 │    └── SupportPlugin.cs       # Políticas e relatórios de suporte
 ├── SkTrailCourse.Tests/        # Testes unitários
 ├── data/
 │    ├── disputes.json          # Armazenamento de disputas (criado automaticamente)
 │    └── boletos.json          # Base de boletos e empresas
 ├── Program.cs                  # Aplicação web ASP.NET Core
 ├── SkTrailCourse.csproj        # Configuração do projeto
 └── .env                        # Configurações da API Gemini (criar manualmente)
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
AI_MODEL_ID=gemini-2.0-flash-exp
```

### 3. Executar o sistema

```bash
cd SkTrailCourse
dotnet restore
dotnet run
```

**Acesse a interface web em:** `https://localhost:5000` (HTTPS) ou `http://localhost:5000` (HTTP)

### 4. Executar testes

```bash
cd SkTrailCourse.Tests
dotnet test
```

---

## 💻 Exemplos de Uso

### 🌐 Interface Web

A interface web oferece uma experiência moderna e intuitiva:

1. **Acesse** `https://localhost:5000` após executar o projeto
2. **Digite comandos** no terminal interativo
3. **Use o botão de ajuda** (i) para ver todos os comandos disponíveis
4. **Use o botão limpar** (⟳) para limpar o terminal
5. **Aproveite as cores inteligentes** que destacam diferentes tipos de resposta
6. **Interface CPF automática** aparece quando necessário para consultas

### 🔍 Consulta de Boletos

**Na interface web:**
```
💬 > verifiquei uma compra de 150 reais no meu boleto
👤 Por favor, informe seu CPF (somente números ou formato padrão) para consulta:
[Campo de CPF aparece automaticamente na interface]

👤 CPF informado: 123.456.789-00
🔍 Consultando boletos para o CPF: 123.456.789-00...

✅ Encontramos 2 boleto(s) para o CPF '123.456.789-00':

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
- Detecta ambiguidade em contextos Zoop
- Fallback para reclamação em caso de dúvida

### 2. **Processamento de Consultas (BoletoLookupPlugin)**
- Busca flexível por CPF do cliente
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

## 🎨 Design System

### Paleta de Cores
```css
--color-primary: #FF5C00      /* Laranja Zoop */
--color-secondary: #FF2D92    /* Rosa Zoop */
--color-success: #00e676      /* Verde */
--color-error: #FF2D92        /* Rosa (erro) */
--color-warning: #FF5C00      /* Laranja (aviso) */
```

### Tipografia
```css
--font-size-xs: 0.9rem        /* Textos pequenos */
--font-size-sm: 1rem          /* Textos padrão */
--font-size-base: 1rem        /* Base */
--font-size-lg: 1.6rem        /* Títulos */
--font-size-terminal: 15px    /* Terminal */
```

### Espaçamentos
```css
--spacing-xs: 6px
--spacing-sm: 10px
--spacing-md: 15px
--spacing-lg: 20px
--spacing-xl: 25px
```

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
- **Google Gemini 2.0 Flash Experimental** - Modelo de linguagem
- **System.Text.Json** - Serialização e persistência
- **DotNetEnv 3.1.1** - Gerenciamento de variáveis de ambiente
- **Microsoft.Extensions.Http 9.0.9** - Cliente HTTP
- **xUnit** - Framework de testes

### Frontend
- **Bootstrap 5.3.0** - Framework CSS responsivo
- **JavaScript Vanilla** - Interatividade do terminal web
- **CSS3 com Variáveis CSS** - Design moderno com tema Zoop padronizado
- **Responsive Design** - Compatível com desktop e mobile
- **Modal Components** - Interface de ajuda integrada

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
- ✅ **APIs REST** para ProcessCommand e SearchBoletos (implementadas!)
- ✅ **Design System** padronizado com variáveis CSS (implementado!)
- **Webhooks** para notificações em tempo real
- **PWA (Progressive Web App)** para uso offline
- **API de consulta direta** por CPF

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
