import asyncio
import sys
import openai
from json_memory_store import JsonMemoryStore
from task_plugin import TaskPlugin
from notes_plugin import NotesPlugin
from summarizer import AISummarizer
from ai_intent_router import AIIntentRouter

async def main():
    # Configurar cliente OpenAI para modelo local
    try:
        import os
        os.environ["OPENAI_API_KEY"] = "apiKey"
        os.environ["OPENAI_BASE_URL"] = "http://localhost:11434/v1/"
        
        client = openai.OpenAI()
        
        # Testar conexão
        client.chat.completions.create(
            model="llama3.1:8b",
            messages=[{"role": "user", "content": "test"}],
            max_tokens=1
        )
        
        print("Modelo de IA conectado com sucesso!")
    except Exception as e:
        print(f"Erro ao conectar ao modelo de IA: {e}")
        print("\nVerifique se:")
        print("1. O Ollama está instalado e rodando")
        print("2. O modelo llama3.1:8b está disponível")
        print("3. O serviço está em http://localhost:11434")
        print("\nPara instalar: ollama pull llama3.1:8b")
        sys.exit(1)
    
    # "Memória" persistida em JSON
    store = JsonMemoryStore("data")
    
    # Criar plugins
    tasks = TaskPlugin(store, client)
    notes = NotesPlugin(store, AISummarizer(client))
    
    # Router usando LLM
    router = AIIntentRouter(client)
    
    print("=== Assistente Pessoal com IA ===")
    print("O que posso fazer por você:")
    print()
    print("Gerenciar suas tarefas:")
    print("  • Criar tarefas - ex: \"Preciso comprar café amanhã\"")
    print("  • Mostrar suas tarefas - ex: \"Mostre minhas tarefas pendentes\"")
    print("  • Concluir tarefas - ex: \"Marquei como concluída a tarefa 2\"")
    print("  • Recomendar o que fazer - ex: \"O que devo fazer agora?\"")
    print()
    print("Organizar suas notas:")
    print("  • Salvar anotações - ex: \"Anote que a reunião foi adiada para sexta\"")
    print("  • Ver suas anotações - ex: \"Mostrar todas as minhas notas\"")
    print("  • Buscar informações - ex: \"Encontre minhas notas sobre reunião\"")
    print("  • Resumir conteúdo - ex: \"Faça um resumo da nota 2\"")
    print()
    print("Digite 'sair' ou 'exit' para encerrar")
    print("----------------------------------------")
    
    while True:
        try:
            user_input = input("> ").strip()
            if not user_input:
                continue
            
            if user_input.lower() in ['exit', 'quit', 'sair']:
                break
            
            # Usar o router baseado em LLM
            plugin_name, function_name, args = await router.route_async(user_input)
            
            if not plugin_name or not function_name:
                print("Desculpe, não entendi o que você precisa. Tente dizer de outra forma ou consulte as sugestões acima.")
                print("   Por exemplo: \"Preciso comprar café\" ou \"Mostre minhas tarefas\".")
                continue
            
            # Executar função apropriada
            result = None
            if plugin_name == "Tasks":
                if function_name == "AddTask":
                    result = await tasks.add_task(args.get("title", ""))
                elif function_name == "ListTasks":
                    result = await tasks.list_tasks()
                elif function_name == "CompleteTask":
                    index = int(args.get("index", 0)) if args.get("index") else 0
                    result = await tasks.complete_task(index)
                elif function_name == "RecommendNext":
                    result = await tasks.recommend_next()
            
            elif plugin_name == "Notes":
                if function_name == "AddNote":
                    result = await notes.add_note(args.get("content", ""))
                elif function_name == "ListNotes":
                    result = await notes.list_notes()
                elif function_name == "SearchNotes":
                    result = await notes.search_notes(args.get("term", ""))
                elif function_name == "SummarizeNote":
                    index = int(args.get("index", 0)) if args.get("index") else 0
                    result = await notes.summarize_note(index)
            
            if result:
                print(result)
            else:
                print("Função não encontrada.")
        
        except KeyboardInterrupt:
            break
        except Exception as e:
            print(f"Erro: {e}")

if __name__ == "__main__":
    asyncio.run(main())