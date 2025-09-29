import json
import re
import openai
from typing import Dict, List, Optional, Tuple, Any

class AIIntentRouter:
    def __init__(self, client: openai.OpenAI):
        self.client = client
        self.plugin_functions = {
            "Tasks": ["AddTask", "ListTasks", "CompleteTask", "RecommendNext"],
            "Notes": ["AddNote", "ListNotes", "SearchNotes", "SummarizeNote"]
        }
    
    async def route_async(self, input_text: str) -> Tuple[Optional[str], Optional[str], Dict[str, Any]]:
        if not input_text or not input_text.strip():
            return None, None, {}
        
        args = {}
        
        try:
            prompt = f"""Você é um assistente especializado em identificar intenções de usuários e rotear comandos para funções adequadas.

Analise a entrada do usuário e determine qual função deve ser chamada de acordo com as seguintes opções disponíveis:

Plugin Tasks:
- AddTask: Adiciona uma tarefa (parâmetro: title)
- ListTasks: Lista todas as tarefas (sem parâmetros)
- CompleteTask: Marca uma tarefa como concluída (parâmetro: index - número inteiro)
- RecommendNext: Sugere a próxima tarefa a ser feita (sem parâmetros)

Plugin Notes:
- AddNote: Adiciona uma nota (parâmetro: content)
- ListNotes: Lista todas as notas (sem parâmetros)
- SearchNotes: Busca notas por um termo (parâmetro: term)
- SummarizeNote: Gera um resumo de uma nota específica (parâmetro: index - número inteiro)

Entrada do usuário: {input_text}

Responda em formato JSON:
{{
  "plugin": "[nome do plugin: Tasks ou Notes]",
  "function": "[nome da função]",
  "parameters": {{
    // parâmetros necessários para a função (se houver)
  }}
}}

Se a entrada não corresponder a nenhuma função, retorne plugin e function como null."""

            response = self.client.chat.completions.create(
                model="llama3.1:8b",
                messages=[{"role": "user", "content": prompt}],
                max_tokens=200
            )
            
            response_text = response.choices[0].message.content.strip()
            
            # Extrair JSON da resposta
            json_match = re.search(r'\{.*\}', response_text, re.DOTALL)
            if json_match:
                json_text = json_match.group()
                
                try:
                    route_info = json.loads(json_text)
                    
                    plugin = route_info.get("plugin")
                    function = route_info.get("function")
                    parameters = route_info.get("parameters", {})
                    
                    if plugin and function:
                        if plugin in self.plugin_functions and function in self.plugin_functions[plugin]:
                            # Adicionar parâmetros
                            if parameters:
                                args.update(parameters)
                            
                            # Tratamentos específicos
                            if function == "AddTask" and "title" not in args:
                                title = self._extract_content_after_keyword(input_text, "tarefa")
                                args["title"] = title if title else "Sem título"
                            elif function == "AddNote" and "content" not in args:
                                content = self._extract_content_after_keyword(input_text, "nota")
                                args["content"] = content if content else "Vazio"
                            
                            return plugin, function, args
                
                except json.JSONDecodeError:
                    pass
        
        except Exception as e:
            print(f"Erro ao rotear com modelo de IA: {e}")
        
        return None, None, args
    
    def _extract_content_after_keyword(self, input_text: str, keyword: str) -> str:
        keyword_index = input_text.lower().find(keyword.lower())
        if keyword_index < 0:
            return ""
        
        return input_text[keyword_index + len(keyword):].strip()