# Nomad Vault MySQL Demo

Multi-implementation showcase of a web application demonstrating Vault Transit (encryption), Transform (FPE), and dynamic database credentials with Nomad orchestration.

## Implementations

This repository contains two language implementations of the same application:

### Python (Flask)
- **Source:** [app/python](app/python)
- **Dockerfile:** [app/python/Dockerfile](app/python/Dockerfile)
- **Dependencies:** Managed via [app/python/pyproject.toml](app/python/pyproject.toml) with `uv`
- **Config:** [app/python/config/config.ini](app/python/config/config.ini)
- **Image:** `ghcr.io/infralovers/nomad-vault-mysql-python`

### .NET (ASP.NET Core MVC)
- **Source:** [app/dotnet](app/dotnet)
- **Dockerfile:** [app/dotnet/Dockerfile](app/dotnet/Dockerfile)
- **Dependencies:** Managed via [app/dotnet/NomadVaultMySqlDotnet.csproj](app/dotnet/NomadVaultMySqlDotnet.csproj)
- **Config:** [app/dotnet/config/config.ini](app/dotnet/config/config.ini)
- **Image:** `ghcr.io/infralovers/nomad-vault-mysql-dotnet`

## Running Locally

### Python Flask

```bash
docker compose up -d --build
```

Access at `http://localhost:8080`

### .NET ASP.NET Core

```bash
docker compose -f docker-compose.dotnet.yml up -d --build
```

Access at `http://localhost:8080`

## Infrastructure as Code

### Nomad Job Specifications

Jobs are organized by implementation and use case:

- **[nomad/python](nomad/python)** — Python app job variants (app_dynamic, app_hardcoded, app_static, app_transit, app_transit_connect, app_transit_connect_traefik)
- **[nomad/dotnet](nomad/dotnet)** — .NET app job variants
- **[nomad/mysql](nomad/mysql)** — Canonical MySQL database jobs (shared, not duplicated)
- **[nomad/tools](nomad/tools)** — Utility jobs (e.g., whoami)

### Setup

Terraform, Vault setup scripts, and infrastructure provisioning are in [setup/](setup/):

```bash
cd setup
terraform init
terraform apply
./vault_transit.sh
./vault_dynamic_db.sh
./vault_kv.sh
```

## Container Images

Both implementations are automatically built and published to GitHub Container Registry on commits to `main`:

- Python: `ghcr.io/infralovers/nomad-vault-mysql-python`
- .NET: `ghcr.io/infralovers/nomad-vault-mysql-dotnet`

See [.github/workflows/ci.yml](.github/workflows/ci.yml) for CI/CD configuration.

## Features

- **Vault Transit:** Encrypt/decrypt sensitive fields (birth_date, address, salary, ssn, ccn)
- **Vault Transform:** Format-preserving encryption (FPE) for SSN and CCN
- **Dynamic Database Credentials:** Request temporary MySQL credentials from Vault
- **CRUD Operations:** Add, view, and update customer records
- **Web UI:** Bulma CSS-styled forms and tables
- **API Endpoints:** `/health`, `/customers`, `/customer?cust_no={id}`, `/records`, `/dbview`

## Configuration

Both implementations read from a default `config/config.ini` file. Key settings:

```ini
[DATABASE]
Address = mysql
Port = 3306
Database = my_app
User = root
Password = root

[VAULT]
Enabled = False
InjectToken = False
Address = http://vault:8200
Token = root
KeyPath = dynamic-app/transit
KeyName = app
Transform = False
```

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for release history.

## License

See repository for licensing details.
