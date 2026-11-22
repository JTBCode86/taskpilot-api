# TaskPilot API

API de tarefas e usuários em ASP.NET Core, com SQL Server e cliente de exemplo em Python. A solução separa domínio, aplicação, infraestrutura e API. Os endpoints de tarefas permitem criar, concluir e consultar um resumo por usuário.

Para criar uma tarefa, informe um título não vazio com até 256 caracteres e o ID de um usuário existente. Um título inválido retorna HTTP 400; um usuário inexistente retorna HTTP 404. Os erros usam o formato Problem Details.

## Ambiente de desenvolvimento com Docker

Pré-requisitos: Docker Engine com Compose e containers Linux, Git e pelo menos 2 GB de memória disponíveis para o SQL Server. A imagem oficial do SQL Server para Linux requer arquitetura x86-64 suportada. Não é necessário instalar SQL Server nem o SDK .NET no computador.

Na raiz do repositório, copie `.env.example` para `.env` e substitua a senha de exemplo por uma senha forte, exclusiva para desenvolvimento. Para o formato da string de conexão usado aqui, não inclua ponto e vírgula, aspas ou cifrão na senha. Ela deve atender à política de complexidade do SQL Server. Nunca compartilhe ou versione `.env`.

No PowerShell:

```powershell
Copy-Item .env.example .env
# Edite .env e altere MSSQL_SA_PASSWORD.
docker compose up --build -d
docker compose ps -a
docker compose logs db-init api
```

O serviço `db-init` aguarda o banco e executa os scripts versionados em uma transação na primeira inicialização. Ele deve terminar com sucesso antes da API iniciar. Na primeira execução são criados os status e um usuário de demonstração de ID 1, sem senha utilizável. Se o banco já contiver objetos sem o marcador de inicialização, o processo interrompe a execução para evitar alterar dados existentes.

- Swagger: http://localhost:8080/swagger
- Saúde da API e conexão ao banco: http://localhost:8080/health
- Resumo de tarefas do usuário de demonstração: http://localhost:8080/api/v1/tasks/summary/1

O Compose publica a API apenas na interface local e não publica a porta do banco. O volume `sql_data` mantém os dados entre reinicializações e `docker compose down`. A senha configurada na criação do volume continua sendo a senha do SQL Server; alterá-la em `.env` não muda a senha existente.
Se a porta 8080 estiver em uso, adicione `TASKPILOT_API_PORT=8081` ao `.env` e ajuste as URLs e `TASKPILOT_API_URL` do cliente.

```powershell
docker compose down
```

**Atenção:** `docker compose down --volumes` apaga permanentemente o banco do projeto. Antes de remover volumes ou atualizar o SQL Server, faça um backup e copie-o para fora do volume. Para bancos já existentes, planeje migração e backup separadamente; o inicializador não faz migrações incrementais nem importa bancos existentes. Novas alterações de esquema precisarão de uma estratégia de migração versionada.

Este Compose é apenas para desenvolvimento: usa edição Developer, conexão interna com certificado confiado sem validação e credenciais administrativas. A API ainda não implementa autenticação; não a exponha na internet ou em redes não confiáveis.

## Cliente Python opcional

O cliente usa Python 3.9+ e não é necessário para executar a API:

```powershell
cd src\taskpilot_client
python -m pip install -r requirements.txt
python main.py
```

Por padrão o cliente acessa `http://localhost:8080`. Para outra instância, defina `TASKPILOT_API_URL` antes de executá-lo. Para HTTPS, o certificado deve ser confiável pelo ambiente Python. O exemplo cria e conclui uma tarefa para o usuário 1.

## Desenvolvimento e testes .NET

Para compilar ou executar testes fora dos containers, instale o SDK .NET 10:

```powershell
dotnet build .\src\TaskPilot\TaskPilot.API\TaskPilot.API.csproj
dotnet test .\src\TaskPilot\TaskPilot.sln
```

Os testes de integração exigem uma instância de desenvolvimento dedicada, já inicializada pelo Compose. Eles criam tarefas permanentes no banco; não os execute em produção. Para habilitá-los:

```powershell
$env:TASKPILOT_INTEGRATION_URL = "http://localhost:8080"
dotnet test .\src\TaskPilot\TaskPilot.Integration.Tests\TaskPilot.Integration.Tests.csproj
Remove-Item Env:\TASKPILOT_INTEGRATION_URL
```

Sem essa variável, os testes de integração são marcados como inconclusivos. Para executar a API diretamente no host, configure `ConnectionStrings__TaskPilotDBConnection` com uma conexão SQL Server válida; o Compose não publica o banco no host por padrão.

## Estrutura

- `src/TaskPilot/TaskPilot.Domain`: entidades e contratos.
- `src/TaskPilot/TaskPilot.Application`: casos de uso e regras de aplicação.
- `src/TaskPilot/TaskPilot.Infrastructure`: acesso ao SQL Server e serviços externos.
- `src/TaskPilot/TaskPilot.API`: endpoints HTTP e configuração.
- `src/Banco de dados`: scripts SQL e sequência de inicialização.
- `src/taskpilot_client`: cliente Python de demonstração.
