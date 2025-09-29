import unittest
import tempfile
import shutil
from json_memory_store import JsonMemoryStore
from task_plugin import TaskPlugin
from notes_plugin import NotesPlugin
from summarizer import MockSummarizer

class TestPlugins(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.mkdtemp()
        self.store = JsonMemoryStore(self.temp_dir)
        self.task_plugin = TaskPlugin(self.store)
        self.notes_plugin = NotesPlugin(self.store, MockSummarizer())
    
    def tearDown(self):
        shutil.rmtree(self.temp_dir)
    
    def test_add_and_list_tasks(self):
        result = self.task_plugin.add_task("Comprar café")
        self.assertIn("Comprar café", result)
        
        tasks = self.task_plugin.list_tasks()
        self.assertIn("Comprar café", tasks)
        self.assertIn("[ ]", tasks)
    
    def test_complete_task(self):
        self.task_plugin.add_task("Tarefa teste")
        result = self.task_plugin.complete_task(1)
        self.assertIn("Concluída", result)
        
        tasks = self.task_plugin.list_tasks()
        self.assertIn("[x]", tasks)
    
    def test_add_and_search_notes(self):
        self.notes_plugin.add_note("Ideia para arquitetura offline")
        
        result = self.notes_plugin.search_notes("arquitetura")
        self.assertIn("arquitetura", result.lower())
    
    def test_summarize_note(self):
        self.notes_plugin.add_note("Esta é uma nota longa para testar o resumo.")
        
        result = self.notes_plugin.summarize_note(1)
        self.assertIn("Mock resumo", result)

if __name__ == "__main__":
    unittest.main()