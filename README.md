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

O serviço `db-init` aguarda o banco, inicializa a versão 1 em bancos vazios e aplica as migrações pendentes. Ele deve terminar com sucesso antes da API iniciar. Na primeira execução são criados os status e um usuário de demonstração de ID 1, sem senha utilizável. Se o banco já contiver objetos sem o marcador de inicialização, o processo interrompe a execução para evitar alterar dados existentes.

- Swagger: http://localhost:8080/swagger
- Saúde da API e conexão ao banco: http://localhost:8080/health
- Resumo de tarefas do usuário de demonstração: http://localhost:8080/api/v1/tasks/summary/1

O Compose publica a API apenas na interface local e não publica a porta do banco. O volume `sql_data` mantém os dados entre reinicializações e `docker compose down`. A senha configurada na criação do volume continua sendo a senha do SQL Server; alterá-la em `.env` não muda a senha existente.
Se a porta 8080 estiver em uso, adicione `TASKPILOT_API_PORT=8081` ao `.env` e ajuste as URLs e `TASKPILOT_API_URL` do cliente.

```powershell
docker compose down
```

**Atenção:** `docker compose down --volumes` apaga permanentemente o banco do projeto. Antes de aplicar migrações, remover volumes ou atualizar o SQL Server, faça um backup e copie-o para fora do volume. Bancos externos sem o marcador de versão não são importados automaticamente; sua adoção requer análise e migração separadas.

Este Compose é apenas para desenvolvimento: usa edição Developer, conexão interna com certificado confiado sem validação e credenciais administrativas. A API ainda não implementa autenticação; não a exponha na internet ou em redes não confiáveis.

## Migrações do banco

O histórico fica em `dbo.TaskPilotSchemaVersion`. Bancos criados pela versão anterior, que já possuem a marcação de versão 1, recebem as novas migrações sem recriar tabelas ou dados. A versão 2 adiciona metadados ao histórico; seu registro de versão 1 não tem data nem checksum retroativos.
Os scripts referenciados por `init.sql` compõem a versão 1 e devem permanecer inalterados; mudanças de esquema devem ser feitas em novas migrações.

Para alterar o esquema:

1. Faça backup e adicione um arquivo em `src/Banco de dados/Migrations`, com a próxima versão sequencial de quatro dígitos, por exemplo `0003_adicionar_indice.sql`. Use somente letras minúsculas, números e sublinhados no nome e grave o arquivo em UTF-8 com finais de linha LF.
2. Escreva SQL para uma única transação, sem `GO`, comandos `:sqlcmd`, `USE` ou comandos de controle de transação. Para objetos que exigem ser o primeiro comando do lote, como procedures, use SQL dinâmico com `EXEC(N'...')`. Não inclua operações que não possam ser revertidas pela transação.
3. Pare a API com `docker compose stop api`, execute `docker compose up -d` e confira `docker compose logs db-init`. Cada migração é aplicada uma vez e registrada na mesma transação; falhas impedem a inicialização da API. Uma API que já esteja em execução não é interrompida automaticamente. Uma trava no banco serializa inicializadores concorrentes; caso dois processos tentem criar o banco vazio ao mesmo tempo, um deles pode precisar ser executado novamente.

Os arquivos aplicados são identificados pelo nome e checksum SHA-256. Não edite, renomeie ou remova uma migração aplicada: crie outra para corrigir o esquema. O inicializador também recusa lacunas ou versões desconhecidas no histórico. Após uma falha, investigue os logs e corrija a migração ainda não aplicada antes de tentar novamente. As transações não substituem backups nem testes em uma cópia dos dados.

Os testes de migração usam um banco e volume Docker temporários isolados e os removem ao terminar. No Windows, com a política local permitindo executar scripts PowerShell:

```powershell
.\docker\tests\Test-DatabaseMigrations.ps1
```

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
