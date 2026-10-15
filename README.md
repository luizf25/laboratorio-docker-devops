# Laboratório Docker — Integração DevOps (Unidade 3)

API ASP.NET Core (.NET 10) empacotada em imagem Docker versionada, implantada em
staging e production, validada por health check, com falha controlada, rollback e
Docker Compose.

## Estrutura

```
DevOpsWebApi/
├── Program.cs          # endpoints /, /health e /version
├── DevOpsWebApi.csproj
├── Dockerfile          # multi-stage (sdk:10.0 -> aspnet:10.0)
├── .dockerignore
└── compose.yaml        # staging + production com healthcheck
```

Versões marcadas com tags Git: `v1.0.0`, `v1.1.0` e `v1.2.0` (falha proposital no
`/health`). A 1.2.0 foi revertida no `main`, então o código atual é o da 1.1.0.

## Endpoints

| Rota       | Desde | Retorno                                            |
|------------|-------|----------------------------------------------------|
| `/`        | 1.0.0 | aplicação, versão e ambiente                       |
| `/health`  | 1.0.0 | `status: healthy`, versão, ambiente e timestamp    |
| `/version` | 1.1.0 | versão, ambiente e mensagem da nova funcionalidade |

Configuração por variáveis de ambiente: `APP_ENVIRONMENT` (padrão `development`) e
`APP_VERSION` (padrão `local`).

> Comandos executados dentro de `DevOpsWebApi/`. No Linux/macOS use `curl` no lugar de
> `curl.exe`.

## Construção

```bash
docker build -t devops-webapi:1.0.0 .
docker images
```

## Execução

```bash
docker run -d --name devops-api -p 8080:8080 devops-webapi:1.0.0
docker logs devops-api
docker stop devops-api
docker rm devops-api
```

> Na máquina usada, a porta 8080 do host já estava ocupada, então a execução usou
> `-p 8090:8080`.

## Staging

```bash
docker run -d --name devops-api-staging -p 8081:8080 -e APP_ENVIRONMENT=staging -e APP_VERSION=1.0.0 devops-webapi:1.0.0
```

## Production

```bash
docker run -d --name devops-api-prod -p 8082:8080 -e APP_ENVIRONMENT=production -e APP_VERSION=1.0.0 devops-webapi:1.0.0
```

## Health Check

```bash
curl.exe -f http://localhost:8081/health
curl.exe -f http://localhost:8082/health
```

## Nova versão

```bash
docker build -t devops-webapi:1.1.0 .

docker rm -f devops-api-staging
docker run -d --name devops-api-staging -p 8081:8080 -e APP_ENVIRONMENT=staging -e APP_VERSION=1.1.0 devops-webapi:1.1.0
curl.exe -f http://localhost:8081/health
curl.exe -f http://localhost:8081/version

docker rm -f devops-api-prod
docker run -d --name devops-api-prod -p 8082:8080 -e APP_ENVIRONMENT=production -e APP_VERSION=1.1.0 devops-webapi:1.1.0
```

## Falha controlada

```bash
docker build -t devops-webapi:1.2.0 .
docker rm -f devops-api-staging
docker run -d --name devops-api-staging -p 8081:8080 -e APP_ENVIRONMENT=staging -e APP_VERSION=1.2.0 devops-webapi:1.2.0
curl.exe -f http://localhost:8081/health   # HTTP 500: não promover
```

## Rollback

```bash
docker rm -f devops-api-prod
docker run -d --name devops-api-prod -p 8082:8080 -e APP_ENVIRONMENT=production -e APP_VERSION=1.1.0 devops-webapi:1.1.0
curl.exe -f http://localhost:8082/health
```

## Docker Compose

```bash
docker compose up -d
docker compose ps
docker compose logs
docker compose config
docker compose down
```

### Healthcheck

A imagem `mcr.microsoft.com/dotnet/aspnet:10.0` não tem `curl` nem `wget`, mas tem
`bash`. O healthcheck do `compose.yaml` usa o `/dev/tcp` do bash para fazer um
`GET /health` e só considera o container saudável se a resposta for HTTP 200.
