from dataclasses import dataclass
from datetime import datetime
from typing import List
import openai
from json_memory_store import JsonMemoryStore

@dataclass
class TaskItem:
    title: str
    done: bool

class TaskPlugin:
    def __init__(self, store: JsonMemoryStore, client: openai.OpenAI):
        self.store = store
        self.client = client
        self.key = "tasks"
    
    async def add_task(self, title: str) -> str:
        tasks = await self.store.load_list_async(self.key, dict)
        task_dict = {"title": title, "done": False}
        tasks.append(task_dict)
        await self.store.save_list_async(self.key, tasks)
        return f"Tarefa adicionada: {title}"
    
    async def list_tasks(self) -> str:
        tasks = await self.store.load_list_async(self.key, dict)
        if not tasks:
            return "Sem tarefas."
        
        result = []
        for i, task in enumerate(tasks, 1):
            status = 'x' if task.get('done', False) else ' '
            result.append(f"{i}. [{status}] {task.get('title', '')}")
        
        return "\n".join(result)
    
    async def complete_task(self, index: int) -> str:
        tasks = await self.store.load_list_async(self.key, dict)
        if index < 1 or index > len(tasks):
            return "Índice inválido."
        
        task = tasks[index - 1]
        task["done"] = True
        await self.store.save_list_async(self.key, tasks)
        return f"Concluída: {task.get('title', '')}"
    
    async def recommend_next(self) -> str:
        tasks = await self.store.load_list_async(self.key, dict)
        
        pending_tasks = [t for t in tasks if not t.get('done', False)]
        if not pending_tasks:
            return "Tudo em dia!"
        
        all_tasks_text = "\n".join([
            f"{i+1}. [{'CONCLUÍDA' if task.get('done', False) else 'PENDENTE'}] {task.get('title', '')}"
            for i, task in enumerate(tasks)
        ])
        
        prompt = f"""Com base na lista de tarefas abaixo, recomende qual seria a mais importante a ser feita em seguida.
Considere fatores como: tarefas já concluídas, prioridades implícitas, e dependências lógicas.
Escolha apenas entre as tarefas PENDENTES.

LISTA DE TAREFAS:
{all_tasks_text}

Forneça sua recomendação com o seguinte formato:
'Recomendo a tarefa X: [título da tarefa]' (onde X é o número da tarefa)"""
        
        try:
            response = self.client.chat.completions.create(
                model="llama3.1:8b",
                messages=[{"role": "user", "content": prompt}],
                max_tokens=100
            )
            
            recommendation = response.choices[0].message.content.strip()
            return f"🤖 {recommendation}"
        
        except Exception:
            return f"🤖 Recomendo a tarefa 1: {pending_tasks[0].get('title', '')}"