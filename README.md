# Laboratório Docker — Integração DevOps (Unidade 3)

API ASP.NET Core (.NET 10) empacotada em imagem Docker versionada, implantada em
**staging** e **production**, validada por **health check**, com **falha controlada**,
**rollback** e **Docker Compose**.

## Estrutura

```
.
├── README.md
├── DevOpsWebApi/
│   ├── Program.cs          # endpoints /, /health e /version
│   ├── DevOpsWebApi.csproj
│   ├── Dockerfile          # multi-stage (sdk:10.0 -> aspnet:10.0)
│   ├── .dockerignore
│   └── compose.yaml        # staging + production com healthcheck
└── evidencias/             # saída real dos comandos executados (00 a 12)
```

Versões no Git (tags): `v1.0.0`, `v1.1.0`, `v1.2.0` (falha proposital) e, no `main`,
o revert da 1.2.0, ou seja, o código atual é o da 1.1.0.

## Endpoints

| Rota       | Desde | Retorno                                              |
|------------|-------|------------------------------------------------------|
| `/`        | 1.0.0 | aplicação, versão e ambiente                         |
| `/health`  | 1.0.0 | `status: healthy`, versão, ambiente e timestamp      |
| `/version` | 1.1.0 | versão, ambiente e mensagem da nova funcionalidade   |

A configuração vem de variáveis de ambiente: `APP_ENVIRONMENT` (padrão `development`) e
`APP_VERSION` (padrão `local`).

> Todos os comandos abaixo são executados dentro da pasta `DevOpsWebApi/`.
> No Linux/macOS use `curl` no lugar de `curl.exe`.

## Teste local (sem Docker)

```bash
dotnet run
```

Evidência: [`evidencias/01-teste-local.txt`](evidencias/01-teste-local.txt)

## Construção

```bash
docker build -t devops-webapi:1.0.0 .
docker images
```

Evidência: [`evidencias/02-build-1.0.0.txt`](evidencias/02-build-1.0.0.txt)

## Execução

```bash
docker run -d --name devops-api -p 8080:8080 devops-webapi:1.0.0
curl.exe -f http://localhost:8080/health
docker logs devops-api
docker stop devops-api
docker rm devops-api
```

> Na máquina usada, a porta 8080 do host já estava ocupada por outro processo, então a
> execução registrada usou `-p 8090:8080`. Evidência:
> [`evidencias/03-container-1.0.0.txt`](evidencias/03-container-1.0.0.txt)

## Staging

```bash
docker run -d --name devops-api-staging -p 8081:8080 -e APP_ENVIRONMENT=staging -e APP_VERSION=1.0.0 devops-webapi:1.0.0
```

## Production

```bash
docker run -d --name devops-api-prod -p 8082:8080 -e APP_ENVIRONMENT=production -e APP_VERSION=1.0.0 devops-webapi:1.0.0
```

Evidência: [`evidencias/04-staging-production-1.0.0.txt`](evidencias/04-staging-production-1.0.0.txt)

## Health Check

```bash
curl.exe -f http://localhost:8081/health
curl.exe -f http://localhost:8082/health
```

## Nova versão (1.1.0)

```bash
docker build -t devops-webapi:1.1.0 .

# deploy somente em staging
docker rm -f devops-api-staging
docker run -d --name devops-api-staging -p 8081:8080 -e APP_ENVIRONMENT=staging -e APP_VERSION=1.1.0 devops-webapi:1.1.0

# quality gate manual
curl.exe -f http://localhost:8081/health
curl.exe -f http://localhost:8081/version

# promoção para production
docker rm -f devops-api-prod
docker run -d --name devops-api-prod -p 8082:8080 -e APP_ENVIRONMENT=production -e APP_VERSION=1.1.0 devops-webapi:1.1.0
curl.exe -f http://localhost:8082/health
```

Evidências: [`05-build-1.1.0.txt`](evidencias/05-build-1.1.0.txt),
[`06-staging-1.1.0-quality-gate.txt`](evidencias/06-staging-1.1.0-quality-gate.txt),
[`07-promocao-1.1.0-production.txt`](evidencias/07-promocao-1.1.0-production.txt)

## Falha controlada (1.2.0)

O `/health` foi alterado para retornar `500 Internal Server Error`:

```bash
docker build -t devops-webapi:1.2.0 .
docker rm -f devops-api-staging
docker run -d --name devops-api-staging -p 8081:8080 -e APP_ENVIRONMENT=staging -e APP_VERSION=1.2.0 devops-webapi:1.2.0
curl.exe -f http://localhost:8081/health   # falha: HTTP 500 -> NÃO promover
```

Evidência: [`evidencias/08-falha-1.2.0-staging.txt`](evidencias/08-falha-1.2.0-staging.txt)

## Rollback

Simulação: a 1.2.0 chegou à produção, o health check falhou e voltamos para a 1.1.0.

```bash
docker rm -f devops-api-prod
docker run -d --name devops-api-prod -p 8082:8080 -e APP_ENVIRONMENT=production -e APP_VERSION=1.1.0 devops-webapi:1.1.0
curl.exe -f http://localhost:8082/health   # HTTP 200, versão 1.1.0
```

Evidência: [`evidencias/09-incidente-e-rollback.txt`](evidencias/09-incidente-e-rollback.txt)

## Docker Compose

```bash
docker rm -f devops-api-staging devops-api-prod
docker compose up -d
docker compose ps
docker compose logs
curl.exe -f http://localhost:8081/health
curl.exe -f http://localhost:8082/health
docker compose config
docker compose down
```

Evidência: [`evidencias/10-docker-compose.txt`](evidencias/10-docker-compose.txt)

### Desafio avançado: healthcheck no Compose

A imagem `mcr.microsoft.com/dotnet/aspnet:10.0` (Ubuntu 24.04) **não tem `curl` nem
`wget`**, então o exemplo com `curl` dos slides não funcionaria. Ela tem `bash`, então o
healthcheck usa o `/dev/tcp` do bash para fazer um `GET /health` e só considera saudável
se a primeira linha da resposta contém ` 200 `:

```yaml
test:
  - CMD
  - bash
  - -c
  - 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n" >&3 && head -n 1 <&3 | grep -q " 200 "'
```

Resultado: staging e production ficaram `(healthy)` com a 1.1.0, e o mesmo comando
marcou a 1.2.0 como `(unhealthy)`
([`evidencias/11-healthcheck-detecta-1.2.0.txt`](evidencias/11-healthcheck-detecta-1.2.0.txt)).
Instalar `curl` na imagem também resolveria, mas aumentaria a imagem final só para isso.

## Limpeza

```bash
docker compose down
docker ps -a
docker images
docker rmi devops-webapi:1.0.0 devops-webapi:1.1.0 devops-webapi:1.2.0   # opcional
```

## Defesa da atividade

1. **Imagem × container:** a imagem é o molde imutável (filesystem, runtime, aplicação
   e metadados). O container é uma instância em execução criada a partir dela; uma mesma
   imagem gera vários containers (aqui, staging e production).
2. **Função do Dockerfile:** descrever, de forma reproduzível e versionável, os passos para
   construir a imagem (imagem base, restore, publish, porta e comando de entrada).
3. **`8081:8080`:** `porta-do-host:porta-do-container`. Uma requisição em
   `localhost:8081` no host é encaminhada para a porta 8080 dentro do container.
4. **Variáveis de ambiente:** separam configuração do código. O mesmo artefato
   (`devops-webapi:1.1.0`) roda como staging ou production mudando só
   `APP_ENVIRONMENT`/`APP_VERSION`, sem rebuild ("build once, deploy many").
5. **Multi-stage build:** o SDK, compiladores e código-fonte ficam só no estágio de build.
   A imagem final leva apenas o runtime ASP.NET e os arquivos publicados: fica menor e
   com menos superfície de ataque.
6. **Tags 1.0.0 / 1.1.0:** identificam com precisão o que está implantado, permitem
   reproduzir a mesma versão e tornam o rollback possível. `latest` muda de significado
   ao longo do tempo.
7. **Staging primeiro:** para validar a release (subiu? responde? `/version` funciona?
   health OK?) antes de expô-la em produção.
8. **Como o health check detectou a falha:** `curl -f` retorna erro (exit 22) quando o
   status HTTP é ≥ 400. A 1.2.0 respondeu **HTTP 500** no `/health`, então o gate falhou
   e a promoção deveria ser bloqueada. No Compose, o container ficou `unhealthy`.
9. **Como foi o rollback:** removendo o container de production da 1.2.0 e criando outro
   com a imagem anterior conhecida como boa, `devops-webapi:1.1.0`, que continuava
   disponível localmente. No Git, o código foi revertido com `git revert`.
10. **GitHub Actions:** um workflow disparado no `push`/tag faria `dotnet build/test`,
    `docker build -t devops-webapi:<tag>`, push para um registry (ex.: GHCR), deploy em
    staging, health check automático como gate e, só se passar, deploy em production
    (com aprovação via *environments*). Em caso de falha, reimplantaria a tag anterior.

### Pergunta de fixação

Diante da 1.2.0 falhando no `/health`: **interromper a promoção (3) e investigar a causa
(4)**. Promover mesmo assim ou gerar outra imagem sem investigar não se sustenta. A
informação que sustenta a decisão é o health check de staging retornando **HTTP 500**
(`curl -f` com exit 22 / container `unhealthy`), enquanto a 1.1.0 retornava 200.
