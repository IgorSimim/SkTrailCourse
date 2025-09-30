# SK Assistant com IA

Assistente de linha de comando usando **Microsoft Semantic Kernel** com modelos de linguagem de IA locais. O sistema utiliza modelos como Llama 3.1 para entender comandos em linguagem natural e executar tarefas através de plugins.

## Funcionalidades

- **Gerenciar tarefas com linguagem natural**:
  - Criar novas tarefas - ex: "Preciso comprar café amanhã"
  - Listar tarefas pendentes - ex: "Mostre minhas tarefas"
  - Marcar tarefas como concluídas - ex: "Marquei como concluída a tarefa 2" 
  - Receber recomendações inteligentes - ex: "O que devo fazer agora?"

- **Gerenciar notas com linguagem natural**:
  - Salvar anotações - ex: "Anote que a reunião foi adiada para sexta"
  - Listar suas anotações - ex: "Mostrar todas as minhas notas"
  - Buscar por conteúdo - ex: "Encontre minhas notas sobre reunião"
  - Gerar resumos automáticos - ex: "Faça um resumo da nota 2"

- **Recursos técnicos**:
  - Integração com modelos de IA locais (como Llama 3.1)
  - Compreensão de linguagem natural para comandos
  - Armazenamento persistido em `data/*.json` (criado automaticamente)
  - Plugins registrados como funções do SK (`TaskPlugin`, `NotesPlugin`)

## Estrutura do projeto

```
Infra/
  AIIntentRouter.cs        -> Direciona comandos de linguagem natural para funções
  AISummarizer.cs          -> Implementação de resumo usando IA
  ISummarizer.cs           -> Interface para resumir textos
  JsonMemoryStore.cs       -> Persistência de dados em JSON
Plugins/
  TaskPlugin.cs            -> Operações de gerenciamento de tarefas
  NotesPlugin.cs           -> Operações de gerenciamento de notas
Program.cs                 -> Loop de interação da interface CLI
```

## Configuração do modelo de IA

O projeto está configurado para usar o modelo Llama 3.1 (8B) local através de uma API compatível com OpenAI. Por padrão, ele espera encontrar o modelo em `http://localhost:11434/v1/`.

Para usar um modelo ou configuração diferente, ajuste as configurações no arquivo `Program.cs`:

```csharp
kernelBuilder.AddOpenAIChatCompletion(
    modelId: "llama3.1:8b",
    apiKey: "apiKey",  // Pode não ser necessário para modelos locais
    httpClient: new HttpClient { 
        BaseAddress = new Uri("http://localhost:11434/v1/")
    });
```

## Como executar

```bash
# Compilar o projeto
dotnet build

# Executar o assistente
dotnet run
```

## Requisitos

- .NET 8.0 ou superior
- Um modelo de linguagem compatível com a API OpenAI
  - Pode ser Llama 3.1 ou outro modelo executado localmente
  - Recomendado: Ollama, llama.cpp ou outra solução que disponibilize uma API REST

## Interação por linguagem natural

O assistente é projetado para entender comandos em linguagem natural. Não é necessário usar comandos específicos - experimente falar naturalmente, como você falaria com um assistente real.

## Licença

Uso educacional/demonstração.
