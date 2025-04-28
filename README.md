# Anotações Importantes para Documentação Futura

## Configuração do Projeto no VSCode  

Para configurar a base do projeto no VSCode, segui este guia:  
[ASP.NET Core Web API - Microsoft Docs](https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-web-api?view=aspnetcore-9.0&tabs=visual-studio-code)  

---

## Estrutura Atual do Sistema  

Atualmente, o sistema possui apenas um **controller provisório**, chamado `TemporaryUser`.  
Esse controller foi g erado a partir do template padrão do ASP.NET Core e servirá como base inicial para o desenvolvimento.  

---

## Como Rodar o Projeto  

Para iniciar o projeto localmente, siga os passos abaixo:  

1. Acesse a pasta do projeto:  
    ```sh
    cd src
    ```
2. Crie um perfil de certificação:
    ```sh
    dotnet dev-certs https --trust
    ```
    Caso apareça um prompt de confirmação na tela apenas aperte "Sim"
3. Rode a aplicação no ambiente de desenvolvimento:  
    ```sh
    dotnet run --launch-profile https
    ```
3. No console, será exibida a **URL da API** que está rodando localmente.  
   Você pode testar o sistema chamando essa URL diretamente no navegador ou via ferramentas como Postman e cURL.  

   Caso queira ver as rotas atuais, você deve rodar `localhost:<port>/swagger`

---

## Troubleshooting  

### Erro: `net::ERR_CERT_INVALID`  
Se a página não for considerada segura e exibir esse erro, siga os passos abaixo:  

1. Acesse a pasta do projeto:  
    ```sh
    cd src
    ```
2. Limpe os perfis de certificação:
    ```sh
    dotnet dev-certs https --clean
    ```
3. Crie um perfil de confiança para certificação web:  
    ```sh
    dotnet dev-certs https --trust
    ```
4. Quando aparecer uma janela de confirmação, clique em **"Sim"**.  
5. Rode o programa agora com o perfil de confiança:  
    ```sh
    dotnet run --launch-profile https
    ```