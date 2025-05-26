import google.generativeai as genai

# Substitua aqui pela sua chave (ou use dotenv se preferir)
genai.configure(api_key="AIzaSyBkrt7Caaj-uC3nZo6N4H_k2wg2QkHRk3Y")

# Lista os modelos disponíveis
models = genai.list_models()

for model in models:
    print(f"Nome: {model.name}")
    print(f"Métodos suportados: {model.supported_generation_methods}")
    print("-" * 40)
