from abc import ABC, abstractmethod

class ISummarizer(ABC):
    @abstractmethod
    def summarize(self, text: str, max_chars: int = 120) -> str:
        pass

class DeterministicSummarizer(ISummarizer):
    def summarize(self, text: str, max_chars: int = 120) -> str:
        if not text or not text.strip():
            return ""
        
        dot_index = text.find('.')
        candidate = text[:dot_index+1] if dot_index > 0 else text
        
        if len(candidate) > max_chars:
            candidate = candidate[:max_chars] + "..."
        
        return candidate

class MockSummarizer(ISummarizer):
    def summarize(self, text: str, max_chars: int = 120) -> str:
        return f"Mock resumo: {text[:50]}..."