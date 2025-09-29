from semantic_kernel.functions import kernel_function
from json_memory_store import JsonMemoryStore
from summarizer import ISummarizer, DeterministicSummarizer
from typing import List, Dict, Any
from datetime import datetime

class NotesPlugin:
    def __init__(self, store: JsonMemoryStore, summarizer: ISummarizer = None):
        self._store = store
        self._summarizer = summarizer or DeterministicSummarizer()
        self._key = "notes"
    
    @kernel_function(description="Adiciona uma nota")
    def add_note(self, content: str) -> str:
        notes = self._store.load_list(self._key, dict)
        note = {
            "content": content,
            "created_at": datetime.now().isoformat()
        }
        notes.append(note)
        self._store.save_list(self._key, notes)
        return "Nota salva."
    
    @kernel_function(description="Lista notas")
    def list_notes(self) -> str:
        notes = self._store.load_list(self._key, dict)
        if not notes:
            return "Sem notas."
        
        result = []
        for i, note in enumerate(notes, 1):
            created = datetime.fromisoformat(note["created_at"])
            content_preview = self._trim(note["content"], 80)
            result.append(f"{i}. {created.strftime('%d/%m %H:%M')} – {content_preview}")
        
        return "\n".join(result)
    
    @kernel_function(description="Busca notas por termo (case-insensitive)")
    def search_notes(self, term: str) -> str:
        notes = self._store.load_list(self._key, dict)
        if not term or not term.strip():
            return "Informe um termo."
        
        found = []
        for i, note in enumerate(notes, 1):
            if term.lower() in note["content"].lower():
                content_preview = self._trim(note["content"], 80)
                found.append(f"{i}. {content_preview}")
        
        if not found:
            return "Nenhuma nota encontrada."
        
        return "\n".join(found)
    
    @kernel_function(description="Resumo curto (via ISummarizer, sem LLM)")
    def summarize_note(self, index: int) -> str:
        notes = self._store.load_list(self._key, dict)
        if index < 1 or index > len(notes):
            return "Índice inválido."
        
        content = notes[index - 1]["content"]
        summary = self._summarizer.summarize(content, 120)
        return f"🧾 Resumo: {summary}"
    
    def _trim(self, text: str, max_len: int) -> str:
        return text if len(text) <= max_len else text[:max_len] + "..."