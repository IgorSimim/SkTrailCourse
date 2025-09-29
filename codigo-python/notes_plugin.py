from dataclasses import dataclass
from datetime import datetime
from typing import List
from json_memory_store import JsonMemoryStore
from summarizer import ISummarizer

@dataclass
class Note:
    content: str
    created_at: str

class NotesPlugin:
    def __init__(self, store: JsonMemoryStore, summarizer: ISummarizer):
        self.store = store
        self.summarizer = summarizer
        self.key = "notes"
    
    async def add_note(self, content: str) -> str:
        notes = await self.store.load_list_async(self.key, dict)
        note_dict = {
            "content": content,
            "created_at": datetime.now().strftime("%d/%m %H:%M")
        }
        notes.append(note_dict)
        await self.store.save_list_async(self.key, notes)
        return "Nota salva."
    
    async def list_notes(self) -> str:
        notes = await self.store.load_list_async(self.key, dict)
        if not notes:
            return "Sem notas."
        
        result = []
        for i, note in enumerate(notes, 1):
            content = self._trim(note.get('content', ''), 80)
            created_at = note.get('created_at', '')
            result.append(f"{i}. {created_at} – {content}")
        
        return "\n".join(result)
    
    async def search_notes(self, term: str) -> str:
        notes = await self.store.load_list_async(self.key, dict)
        if not term or not term.strip():
            return "Informe um termo."
        
        found = []
        for i, note in enumerate(notes, 1):
            if term.lower() in note.get('content', '').lower():
                content = self._trim(note.get('content', ''), 80)
                found.append(f"{i}. {content}")
        
        if not found:
            return "Nenhuma nota encontrada."
        
        return "\n".join(found)
    
    async def summarize_note(self, index: int) -> str:
        notes = await self.store.load_list_async(self.key, dict)
        if index < 1 or index > len(notes):
            return "Índice inválido."
        
        text = notes[index - 1].get('content', '')
        summary = self.summarizer.summarize(text, 120)
        return f"Resumo: {summary}"
    
    @staticmethod
    def _trim(text: str, max_length: int) -> str:
        return text if len(text) <= max_length else text[:max_length] + "..."