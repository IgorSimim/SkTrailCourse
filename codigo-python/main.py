import asyncio
from semantic_kernel import Kernel
from json_memory_store import JsonMemoryStore
from task_plugin import TaskPlugin
from notes_plugin import NotesPlugin
from summarizer import DeterministicSummarizer
from intent_router import IntentRouter

async def main():
    # Criar kernel sem LLM
    kernel = Kernel()
    
    # Memória persistida em JSON
    store = JsonMemoryStore("data")
    tasks = TaskPlugin(store)
    notes = NotesPlugin(store, DeterministicSummarizer())
    
    # Registrar plugins no kernel
    kernel.add_plugin(tasks, plugin_name="Tasks")
    kernel.add_plugin(notes, plugin_name="Notes")
    
    # Router heurístico
    router = IntentRouter()
    
    print("=== SK Offline Assistant (sem LLM) ===")
    print("Dicas:")
    print("- tarefas: 'add tarefa ...', 'listar tarefas', 'concluir 2', 'sugerir proxima'")
    print("- notas: 'add nota ...', 'listar notas', 'buscar nota termo', 'resumo 3'")
    print("- sair: 'exit' ou 'quit'")
    print("----------------------------------------")
    
    while True:
        user_input = input("> ").strip()
        if not user_input:
            continue
        if user_input.lower() in ["exit", "quit"]:
            break
        
        plugin_name, function_name, args = router.route(user_input)
        
        if not plugin_name or not function_name:
            print("Não entendi. Tente: 'add tarefa Comprar café', 'listar tarefas', 'add nota ideia...', 'buscar nota café'.")
            continue
        
        try:
            result = await kernel.invoke(plugin_name, function_name, **args)
            print(result)
        except Exception as ex:
            print(f"Erro: {ex}")

if __name__ == "__main__":
    asyncio.run(main())