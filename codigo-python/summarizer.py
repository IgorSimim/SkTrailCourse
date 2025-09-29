from abc import ABC, abstractmethod
import openai
from typing import Optional

class ISummarizer(ABC):
    @abstractmethod
    def summarize(self, text: str, max_chars: int = 120) -> str:
        pass

class AISummarizer(ISummarizer):
    def __init__(self, client: openai.OpenAI):
        self.client = client
    
    def summarize(self, text: str, max_chars: int = 120) -> str:
        if not text or not text.strip():
            return ""
        
        prompt = f"""Você é um assistente especializado em criar resumos concisos.
Resumir o seguinte texto em no máximo 120 caracteres, mantendo as informações mais importantes:

{text}"""
        
        try:
            response = self.client.chat.completions.create(
                model="llama3.1:8b",
                messages=[{"role": "user", "content": prompt}],
                max_tokens=50
            )
            
            summary = response.choices[0].message.content.strip()
            
            if len(summary) > max_chars:
                summary = summary[:max_chars] + "..."
            
            return summary
        except Exception:
            return text[:max_chars] + "..." if len(text) > max_chars else text