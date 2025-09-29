import json
import os
from typing import List, TypeVar, Generic
import aiofiles

T = TypeVar('T')

class JsonMemoryStore(Generic[T]):
    def __init__(self, folder: str):
        self.folder = folder
        os.makedirs(folder, exist_ok=True)
    
    def _path_for(self, key: str) -> str:
        return os.path.join(self.folder, f"{key}.json")
    
    async def load_list_async(self, key: str, item_type: type) -> List[T]:
        path = self._path_for(key)
        if not os.path.exists(path):
            return []
        
        async with aiofiles.open(path, 'r', encoding='utf-8') as f:
            content = await f.read()
            return json.loads(content) if content else []
    
    async def save_list_async(self, key: str, items: List[T]):
        path = self._path_for(key)
        async with aiofiles.open(path, 'w', encoding='utf-8') as f:
            await f.write(json.dumps(items, indent=2, ensure_ascii=False))