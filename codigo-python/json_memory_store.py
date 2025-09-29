import json
import os
from typing import List, TypeVar, Generic

T = TypeVar('T')

class JsonMemoryStore(Generic[T]):
    def __init__(self, folder: str):
        self._folder = folder
        os.makedirs(folder, exist_ok=True)
    
    def _path_for(self, key: str) -> str:
        return os.path.join(self._folder, f"{key}.json")
    
    def load_list(self, key: str, item_type: type) -> List[dict]:
        path = self._path_for(key)
        if not os.path.exists(path):
            return []
        with open(path, 'r', encoding='utf-8') as f:
            return json.load(f)
    
    def save_list(self, key: str, items: List[dict]):
        path = self._path_for(key)
        with open(path, 'w', encoding='utf-8') as f:
            json.dump(items, f, indent=2, ensure_ascii=False)