from datetime import datetime
import os
import json
import redis
from openai import OpenAI
from pymongo import MongoClient
from pymongo.errors import PyMongoError
from typing import List, Dict, Optional
from dotenv import load_dotenv
import requests
import tempfile
from langchain_community.document_loaders import PyPDFLoader, UnstructuredFileLoader

load_dotenv()

class FileProcessor:
    def __init__(self):
        self.mongo = MongoClient(os.getenv("MONGO_URI"))
        self.db = self.mongo[os.getenv("MONGO_DB")]
        
    def process_file(self, file_path: str, file_type: str) -> List[str]:
        """Processa arquivos PDF, DOCX e TXT"""
        try:
            loaders = {
                'pdf': PyPDFLoader,
                'docx': UnstructuredFileLoader,
                'txt': UnstructuredFileLoader
            }
            
            loader = loaders[file_type](file_path)
            return [doc.page_content for doc in loader.load()]
        except Exception as e:
            print(f"Erro ao processar arquivo: {str(e)}")
            return []

class LLMService:
    def __init__(self):
        self.redis = redis.Redis(
            host=os.getenv("REDIS_HOST"),
            port=int(os.getenv("REDIS_PORT")),
            password=os.getenv("REDIS_PASSWORD"),
            decode_responses=True)
        print("Redis: ", self.redis.ping())
        self.openai = OpenAI(
            base_url=os.getenv("OPENROUTER_URL"),
            api_key=os.getenv("OPENROUTER_API_KEY"))
        
        self.mongo = MongoClient(os.getenv("MONGO_URI"))
        print("MongoDB: ", self.mongo.admin.command('ping'))
        self.db = self.mongo[os.getenv("MONGO_DB", "Chat")]
        self.file_processor = FileProcessor()
        
    def get_agent_config(self, agent_id: int) -> Optional[Dict]:
        """Busca configuração do agente na API C#"""
        try:
            headers = {
                "Authorization": f"Bearer {os.getenv('CSHARP_API_TOKEN')}"
            }
            response = requests.get(
                f"http://{os.getenv('CSHARP_API_HOST', 'localhost')}:7254/api/agent/Agent/{agent_id}",
                headers=headers
            )
            return response.json()['config']
        except Exception as e:
            print(f"Erro ao buscar config do agente: {str(e)}")
            return None
            
    def get_context(self, agent_id: int) -> str:
        """Busca contexto de arquivos no MongoDB"""
        try:
            files = self.db.files.find({"agent_id": agent_id})
            return "\n".join([f['content'] for f in files])
        except PyMongoError as e:
            print(f"Erro MongoDB: {str(e)}")
            return ""
    
    def generate_response(self, _model:str, system_prompt: str, context: str, message: str) -> str:
        """Gera resposta usando LLM"""
        try:
            completion = self.openai.chat.completions.create(
                model=_model,
                messages=[
                    {"role": "system", "content": f"{system_prompt}\nContexto:\n{context}"},
                    {"role": "user", "content": message}
                ]
            )
            return completion.choices[0].message.content
            
        except Exception as e:
            print(f"Erro ao gerar resposta: {str(e)}")
            return "Desculpe, ocorreu um erro ao processar sua mensagem."
    
    def handle_message(self, data: Dict):
        """Processa uma mensagem recebida"""
        try:
            print("Processando mensagem...")
            config = self.get_agent_config(data['agent_id'])
            if not config:
                print("Config não encontrada!")
                return
            print("Config encontrada, continuando...")
            context = self.get_context(data['agent_id'])

            print("Context encontrado, continuando...")
            response = self.generate_response(
                config["model"],
                config['systemPrompt'],
                context,
                data['message']
            )
            print("Response gerada com sucesso")
            # Salvar no MongoDB
            self.db.history.insert_one({
                "conversation_id": data['conversation_id'],
                "user_id": data['user_id'],
                "agent_id": data['agent_id'],
                "input": data['message'],
                "output": response,
                "timestamp": datetime.utcnow()
            })

            # Log antes de publicar a resposta no Redis
            print(f"Publicando resposta no Redis: {response}")

            # Publicar resposta
            self.redis.publish(
                f"user:{data['user_id']}:responses",
                json.dumps({
                    "conversation_id": data['conversation_id'],
                    "text": response
                })
            )

        except Exception as e:
            print(f"Erro no processamento da mensagem: {str(e)}")
    def process_files(self):
        """Processa arquivos enviados via Redis"""
        pubsub = self.redis.pubsub()
        pubsub.subscribe('file_uploads')
        
        for message in pubsub.listen():
            if message['type'] == 'message':
                try:
                    data = json.loads(message['data'])
                    with tempfile.NamedTemporaryFile() as tmp:
                        tmp.write(data['content'].encode())
                        chunks = self.file_processor.process_file(tmp.name, data['file_type'])
                        
                        self.db.files.insert_one({
                            "agent_id": data['agent_id'],
                            "file_name": data['file_name'],
                            "content": "\n".join(chunks),
                            "uploaded_at": datetime.utcnow()
                        })
                        
                except Exception as e:
                    print(f"Erro ao processar arquivo: {str(e)}")

    def run(self):
        """Inicia os processamentos"""
        pubsub = self.redis.pubsub()
        pubsub.subscribe('chat_messages')
        
        print("Serviço LLM iniciado...")
        for message in pubsub.listen():
            if message['type'] == 'message':
                print("Mensagem recebida...")
                try:
                    data = json.loads(message['data'])
                    self.handle_message(data)
                except Exception as e:
                    print(f"Erro geral: {str(e)}")

if __name__ == "__main__":
    service = LLMService()
    
    import threading
    threading.Thread(target=service.process_files, daemon=True).start()
    service.run()