# Assistente Pessoal com IA - Python

Versão Python do assistente pessoal que gerencia tarefas e notas usando IA local.

## Pré-requisitos

1. **Ollama instalado e rodando:**
```bash
# Instalar Ollama (Linux/Mac)
curl -fsSL https://ollama.ai/install.sh | sh

# Baixar modelo
ollama pull llama3.1:8b

# Verificar se está rodando
ollama list
```

## Instalação

### Opção 1: Instalação direta
1. Instalar dependências:
```bash
pip install -r requirements.txt
```

### Opção 2: Usando ambiente virtual (recomendado)
1. Criar ambiente virtual:
```bash
python3 -m venv venv
```

2. Ativar ambiente virtual:
```bash
# Windows
venv\Scripts\activate

# Linux/Mac
source venv/bin/activate
```

3. Instalar dependências (certifique-se que o venv está ativo):
```bash
pip install -r requirements.txt
```

**Importante**: Sempre ative o ambiente virtual antes de executar o programa!

4. Certificar que o modelo local está rodando em `http://localhost:11434/v1/`

## Execução

**Se usando venv, ative primeiro:**
```bash
source venv/bin/activate  # Linux/Mac
# ou
venv\Scripts\activate     # Windows
```

**Executar o programa:**
```bash
python3 main.py
```

## Funcionalidades

- **Gerenciamento de Tarefas**: Adicionar, listar, concluir e receber recomendações
- **Gerenciamento de Notas**: Adicionar, listar, buscar e resumir notas
- **IA Router**: Interpreta comandos em linguagem natural
- **Persistência**: Dados salvos em arquivos JSON

## Estrutura

- `main.py` - Aplicação principal
- `json_memory_store.py` - Armazenamento em JSON
- `task_plugin.py` - Plugin de tarefas
- `notes_plugin.py` - Plugin de notas
- `summarizer.py` - Interface e implementação de resumos
- `ai_intent_router.py` - Roteador inteligente de comandos