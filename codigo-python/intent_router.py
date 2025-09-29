import re
from typing import Tuple, Optional, Dict, Any

class IntentRouter:
    def route(self, input_text: str) -> Tuple[Optional[str], Optional[str], Dict[str, Any]]:
        input_text = input_text.strip().lower()
        args = {}
        
        # Tarefas
        if input_text.startswith(("add tarefa", "adicionar tarefa")):
            title = self._extract_after_keyword(input_text, "tarefa")
            args["title"] = title if title else "Sem título"
            return ("Tasks", "add_task", args)
        
        if input_text == "listar tarefas":
            return ("Tasks", "list_tasks", args)
        
        if input_text.startswith("concluir"):
            num = self._extract_int(input_text)
            args["index"] = num
            return ("Tasks", "complete_task", args)
        
        if "sugerir" in input_text and "proxima" in input_text:
            return ("Tasks", "recommend_next", args)
        
        # Notas
        if input_text.startswith(("add nota", "adicionar nota")):
            content = self._extract_after_keyword(input_text, "nota")
            args["content"] = content if content else "Vazio"
            return ("Notes", "add_note", args)
        
        if input_text == "listar notas":
            return ("Notes", "list_notes", args)
        
        if input_text.startswith("buscar nota"):
            term = self._extract_after_keyword(input_text, "nota")
            args["term"] = term
            return ("Notes", "search_notes", args)
        
        if input_text.startswith("resumo"):
            num = self._extract_int(input_text)
            args["index"] = num
            return ("Notes", "summarize_note", args)
        
        return (None, None, args)
    
    def _extract_after_keyword(self, text: str, keyword: str) -> str:
        idx = text.find(keyword)
        if idx >= 0:
            return text[idx + len(keyword):].strip()
        return ""
    
    def _extract_int(self, text: str) -> int:
        numbers = re.findall(r'\d+', text)
        return int(numbers[0]) if numbers else -1