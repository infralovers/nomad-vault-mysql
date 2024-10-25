terraform {
  required_version = ">= 1.6"
  required_providers {
    vault = {
      source = "hashicorp/vault"
    }
  }
}

provider "vault" {
  skip_tls_verify = true
}

variable "prefix" {
  type    = string
}
variable "app_database_password" {
  type      = string
  sensitive = true
}
variable "app_root_password" {
  type      = string
  sensitive = true
}
variable "mysql_server"  {
    type = string
}

resource "vault_mount" "kv" {
  path = "${var.prefix}/kv"
  type = "kv-v2"
}

resource "vault_kv_secret_v2" "app" {
  mount = vault_mount.kv.path
  name  = "database"
  data_json = jsonencode(
    {
      database = "app",
      username = "app",
      password = var.app_database_password
    }
  )
}
resource "vault_kv_secret_v2" "admin" {
  mount = vault_mount.kv.path
  name  = "database_root"
  data_json = jsonencode(
    {
      username = "root",
      password = var.app_root_password
    }
  )
}

resource "vault_mount" "db" {
  path = "${var.prefix}/db"
  type = "database"
}

resource "vault_database_secret_backend_connection" "mysql" {
  backend       = vault_mount.db.path
  name          = "mysql"
  allowed_roles = [ "*" ]

  mysql {
    connection_url = "{{username}}:{{password}}@tcp(${var.mysql_server}:3306)/"
    username       = "root"
    password       = var.app_root_password
  }
}

resource "vault_database_secret_backend_role" "app" {
  backend             = vault_mount.db.path
  name                = "app"
  db_name             = vault_database_secret_backend_connection.mysql.name
  creation_statements = ["CREATE USER '{{name}}'@'%' IDENTIFIED BY '{{password}}';GRANT ALL ON app.* TO '{{name}}'@'%';"]
  default_ttl         = 3600
  max_ttl             = 7200
}

resource "vault_database_secret_backend_role" "admin" {
  backend             = vault_mount.db.path
  name                = "admin"
  db_name             = vault_database_secret_backend_connection.mysql.name
  creation_statements = ["CREATE USER '{{name}}'@'%' IDENTIFIED BY '{{password}}';GRANT ALL ON *.* TO '{{name}}'@'%';"]
  default_ttl         = 900
  max_ttl             = 3600
}

resource "vault_mount" "transit" {
  path = "${var.prefix}/transit"
  type = "transit"
}

resource "vault_transit_secret_backend_key" "app" {
  backend = vault_mount.transit.path
  name    = "app"
}

data "vault_policy_document" "nomad-dynamic-app" {

  rule {
    path         = "${vault_mount.kv.path}/data/${vault_kv_secret_v2.app.name}"
    capabilities = ["read"]
  }
  rule {
    path         = "${vault_mount.db.path}/creds/${vault_database_secret_backend_role.app.name}"
    capabilities = ["read"]
  }
  rule {
    path         = "${vault_mount.transit.path}/encrypt/${vault_transit_secret_backend_key.app.name}"
    capabilities = ["create", "update"]
  }
  rule {
    path         = "${vault_mount.transit.path}/decrypt/${vault_transit_secret_backend_key.app.name}"
    capabilities = ["create", "update"]
  }
}

data "vault_policy_document" "nomad-mysql" {
  rule {
    path         = "${vault_mount.kv.path}/data/${vault_kv_secret_v2.admin.name}"
    capabilities = ["read"]
  }
}

resource "vault_policy" "nomad-dynamic-app" {
  name   = "nomad-dynamic-app"
  policy = data.vault_policy_document.nomad-dynamic-app.hcl
}
resource "vault_policy" "nomad-mysql" {
  name   = "nomad-mysql"
  policy = data.vault_policy_document.nomad-mysql.hcl
}