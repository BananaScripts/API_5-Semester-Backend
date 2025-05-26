from datetime import datetime
import os
import json
import redis
import google.generativeai as genai
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
        
        # Configuração do Gemini
        genai.configure(api_key=os.getenv("GEMINI_API_KEY"))
        self.model_name = 'model/gemini-1.5-flash-latest'  # Modelo padrão
        
        self.mongo = MongoClient(os.getenv("MONGO_URI"))
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
    
    def generate_response(self, _model: str, system_prompt: str, context: str, message: str) -> str:
        """Gera resposta usando Gemini"""
        try:
            model = genai.GenerativeModel(_model)
            
            # Combinando prompt do sistema e contexto
            full_prompt = f"{system_prompt}\n\nContexto:\n{context}\n\nPergunta: {message}"
            
            response = model.generate_content(full_prompt)
            
            # Verifica se há conteúdo válido na resposta
            if response.candidates and len(response.candidates) > 0:
                return response.text
            else:
                return "Desculpe, não consegui gerar uma resposta adequada."
                
        except Exception as e:
            print(f"Erro ao gerar resposta: {str(e)}")
            return "Desculpe, ocorreu um erro ao processar sua mensagem."
    
    @staticmethod
    def validate_message_data(data: Dict) -> bool:
        required_keys = {"conversation_id", "user_id", "agent_id", "message"}
        return all(key in data for key in required_keys)

    def handle_message(self, data: Dict):
        """Processa uma mensagem recebida"""
        try:
            print("Processando mensagem...")

            if not self.validate_message_data(data):
                print("Dados da mensagem inválidos. Ignorando.")
                return

            config = self.get_agent_config(data['agent_id'])
            if not config:
                print("Config não encontrada!")
                return

            print("Config encontrada, continuando...")
            context = self.get_context(data['agent_id'])

            print("Contexto encontrado, gerando resposta...")
            response = self.generate_response(
                config.get("model", self.model_name),  # Usa modelo padrão se não configurado
                config["systemPrompt"],
                context,
                data["message"]
            )

            print("Resposta gerada, salvando no MongoDB...")
            result = self.db.history.insert_one({
                "conversation_id": data["conversation_id"],
                "user_id": data["user_id"],
                "agent_id": data["agent_id"],
                "input": data["message"],
                "output": response,
                "timestamp": datetime.utcnow()
            })
            print(f"Mensagem salva com ID: {result.inserted_id}")

            redis_key = f"user:{data['user_id']}:responses:{data['conversation_id']}"
            redis_value = json.dumps({
                "conversation_id": data["conversation_id"],
                "text": response
            })

            print("Publicando resposta no Redis...")
            try:
                # Publicar no canal (tempo real)
                self.redis.publish(f"user:{data['user_id']}:responses", redis_value)

                # Armazenar com TTL de 5 minutos
                self.redis.set(redis_key, redis_value, ex=300)
                print(f"Resposta armazenada no Redis com expire (chave: {redis_key})")

            except Exception as pub_err:
                print(f"Erro ao publicar ou armazenar no Redis: {str(pub_err)}. Resposta salva no banco.")

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

                        # ✅ Marca arquivo como processado com TTL
                        status_key = f"file_processed:{data['agent_id']}:{data['file_name']}"
                        self.redis.set(status_key, "OK", ex=300)
                        print(f"Arquivo processado, status gravado no Redis (chave: {status_key})")

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