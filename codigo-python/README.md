# SK Offline Course - Python

Versão Python do assistente de linha de comando usando **Microsoft Semantic Kernel** sem depender de chamadas a LLM. Tudo funciona localmente usando armazenamento JSON + heurísticas determinísticas.

## Funcionalidades

- Gerenciar tarefas: adicionar, listar, concluir, sugerir próxima
- Gerenciar notas: adicionar, listar, buscar por termo, resumir (resumo determinístico sem LLM)
- Armazenamento persistido em `data/*.json` (criado automaticamente)
- Plugins registrados como funções do SK (`TaskPlugin`, `NotesPlugin`)
- Abstração `ISummarizer` para permitir mocks em testes sem rede

## Estrutura

```
json_memory_store.py      -> Persistência simples em JSON
summarizer.py             -> Interface e implementações para sumarização
intent_router.py          -> Roteamento de comandos (heurístico)
task_plugin.py            -> Operações de tarefas
notes_plugin.py           -> Operações de notas + resumo
main.py                   -> Loop de interação (CLI)
test_plugins.py           -> Testes unitários
```

## Instalação e Execução

### Opção 1: Ambiente Virtual (Recomendado)
```bash
# Criar ambiente virtual
python -m venv venv

# Ativar ambiente virtual
source venv/bin/activate     # Linux/Mac
# ou
venv\Scripts\activate        # Windows

# Instalar dependências
pip install -r requirements.txt

# Executar
python main.py
```

### Opção 2: Instalação Global
```bash
# Instalar dependências
pip install -r requirements.txt

# Executar
python main.py
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

```bash
python -m unittest test_plugins.py
```

## Como funciona o "resumo" sem LLM

A classe `DeterministicSummarizer` pega a primeira frase (até o primeiro ponto) ou corta o texto em 120 caracteres e adiciona `...` se necessário. Isso garante comportamento reprodutível.

## Diferenças da versão C#

- Usa `semantic-kernel` Python em vez de .NET
- Funções assíncronas com `asyncio`
- Armazenamento JSON simplificado
- Testes com `unittest` em vez de xUnit

## Extensões / Próximos Passos

- Adicionar mais heurísticas de roteamento
- Implementar memória vetorial local para buscas semânticas offline
- Incluir benchmark de desempenho
- Integrar LLM real atrás de flag de configuração