from semantic_kernel.functions import kernel_function
from json_memory_store import JsonMemoryStore
from typing import List, Dict, Any
from datetime import datetime

class TaskPlugin:
    def __init__(self, store: JsonMemoryStore):
        self._store = store
        self._key = "tasks"
    
    @kernel_function(description="Adiciona uma tarefa")
    def add_task(self, title: str) -> str:
        tasks = self._store.load_list(self._key, dict)
        task = {"title": title, "done": False, "created_at": datetime.now().isoformat()}
        tasks.append(task)
        self._store.save_list(self._key, tasks)
        return f"Tarefa adicionada: {title}"
    
    @kernel_function(description="Lista as tarefas")
    def list_tasks(self) -> str:
        tasks = self._store.load_list(self._key, dict)
        if not tasks:
            return "Sem tarefas."
        
        result = []
        for i, task in enumerate(tasks, 1):
            status = "x" if task.get("done", False) else " "
            result.append(f"{i}. [{status}] {task['title']}")
        
        return "\n".join(result)
    
    @kernel_function(description="Conclui tarefa pelo índice (1-based)")
    def complete_task(self, index: int) -> str:
        tasks = self._store.load_list(self._key, dict)
        if index < 1 or index > len(tasks):
            return "Índice inválido."
        
        task = tasks[index - 1]
        task["done"] = True
        self._store.save_list(self._key, tasks)
        return f"🎉 Concluída: {task['title']}"
    
    @kernel_function(description="Sugere próxima tarefa (heurística simples)")
    def recommend_next(self) -> str:
        tasks = self._store.load_list(self._key, dict)
        for i, task in enumerate(tasks):
            if not task.get("done", False):
                return f"Próxima tarefa sugerida: {i+1}. {task['title']}"
        return "Tudo em dia!"