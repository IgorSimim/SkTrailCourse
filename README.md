# SK Offline Course

Assistente de linha de comando usando **Microsoft Semantic Kernel** sem depender de chamadas a LLM (OpenAI/Azure). Tudo funciona localmente usando armazenamento JSON + heurísticas determinísticas.

## Funcionalidades

- Gerenciar tarefas: adicionar, listar, concluir, sugerir próxima.
- Gerenciar notas: adicionar, listar, buscar por termo, resumir (resumo determinístico sem LLM).
- Armazenamento persistido em `data/*.json` (criado automaticamente).
- Plugins registrados como funções do SK (`TaskPlugin`, `NotesPlugin`).
- Abstração `ISummarizer` para permitir mocks em testes sem rede.

## Estrutura

```
Infra/
  JsonMemoryStore.cs        -> Persistência simples em JSON
  ISummarizer.cs            -> Interface para sumarização
  DeterministicSummarizer.cs-> Implementação padrão (sem IA)
  MockSummarizer.cs         -> Mock para testes
Plugins/
  TaskPlugin.cs             -> Operações de tarefas
  NotesPlugin.cs            -> Operações de notas + resumo
Program.cs                  -> Loop de interação (CLI)
SkOfflineCourse.Tests/      -> Projeto de testes xUnit
```

## Rodando a aplicação

```bash
# Build
 dotnet build
# Executar
 dotnet run
```

Exemplos de comandos dentro do app:
```
add tarefa Comprar café
listar tarefas
concluir 1
add nota Ideia para arquitetura offline
buscar nota arquitetura
resumo 1
quit
```

## Testes

Os testes usam `MockSummarizer` para garantir saída determinística:
```bash
dotnet test
```

## Como funciona o "resumo" sem LLM
A classe `DeterministicSummarizer` pega a primeira frase (até o primeiro ponto) ou corta o texto em 120 caracteres e adiciona `...` se necessário. Isso garante comportamento reprodutível.

## Extensões / Próximos Passos
- Adicionar mais heurísticas de roteamento no `IntentRouter`.
- Implementar memória vetorial local (ex: Annoy / Faiss wrapper) para buscas semânticas offline.
- Incluir benchmark simples de desempenho para operações em lote.
- Integrar futuramente uma LLM real atrás de uma flag de configuração.

## Licença
Uso educacional/demonstração.
