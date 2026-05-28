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

variable "nomad_server" {
  type    = string
  default = "http://192.168.56.11:4646"
}

variable "prefix" {
  type = string
}
variable "app_database_password" {
  type      = string
  sensitive = true
}
variable "app_root_password" {
  type      = string
  sensitive = true
}
variable "mysql_server" {
  type    = string
  default = ""
}

resource "vault_mount" "kv" {
  path = "${var.prefix}/kv"
  type = "kv-v2"
}

resource "vault_jwt_auth_backend" "nomad" {
  path               = "jwt-nomad"
  jwks_url           = "${var.nomad_server}/.well-known/jwks.json"
  jwt_supported_algs = ["RS256", "EdDSA", ]
  default_role       = "nomad"
}


resource "vault_jwt_auth_backend_role" "nomad" {
  backend                 = vault_jwt_auth_backend.nomad.path
  role_name               = "nomad"
  role_type               = "jwt"
  bound_audiences         = ["nomad"]
  user_claim              = "/nomad_job_id"
  user_claim_json_pointer = true
  claim_mappings = {
    nomad_job_id    = "nomad_job_id"
    nomad_namespace = "nomad_namespace"
    nomad_task      = "nomad_task"
  }
  token_type     = "service"
  token_period   = 1800
  token_policies = ["default", vault_policy.nomad-auth.name, vault_policy.nomad-mysql.name, vault_policy.nomad-dynamic-app.name]


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
  count = var.mysql_server != "" ? 1 : 0
  path  = "${var.prefix}/db"
  type  = "database"
}

resource "vault_database_secret_backend_connection" "mysql" {
  count         = var.mysql_server != "" ? 1 : 0
  backend       = vault_mount.db[count.index].path
  name          = "mysql"
  allowed_roles = ["*"]

  mysql {
    connection_url = "{{username}}:{{password}}@tcp(${var.mysql_server}:3306)/"
    username       = "root"
    password       = var.app_root_password
  }
}

resource "vault_database_secret_backend_role" "app" {
  count = var.mysql_server != "" ? 1 : 0

  backend             = vault_mount.db[count.index].path
  name                = "app"
  db_name             = vault_database_secret_backend_connection.mysql[count.index].name
  creation_statements = ["CREATE USER '{{name}}'@'%' IDENTIFIED BY '{{password}}';GRANT ALL ON app.* TO '{{name}}'@'%';"]
  default_ttl         = 3600
  max_ttl             = 7200
}

resource "vault_database_secret_backend_role" "admin" {
  count = var.mysql_server != "" ? 1 : 0

  backend             = vault_mount.db[count.index].path
  name                = "admin"
  db_name             = vault_database_secret_backend_connection.mysql[count.index].name
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


data "vault_policy_document" "nomad-auth" {

  rule {
    path         = "kv/data/{{identity.entity.aliases.AUTH_METHOD_ACCESSOR.metadata.nomad_namespace}}/{{identity.entity.aliases.AUTH_METHOD_ACCESSOR.metadata.nomad_job_id}}/*"
    capabilities = ["read"]
  }
  rule {
    path         = "kv/data/{{identity.entity.aliases.AUTH_METHOD_ACCESSOR.metadata.nomad_namespace}}/{{identity.entity.aliases.AUTH_METHOD_ACCESSOR.metadata.nomad_job_id}}"
    capabilities = ["read"]
  }
  rule {
    path         = "kv/metadata/{{identity.entity.aliases.AUTH_METHOD_ACCESSOR.metadata.nomad_namespace}}/*"
    capabilities = ["list"]
  }
  rule {
    path         = "kv/metadata/*"
    capabilities = ["list"]
  }
}


data "vault_policy_document" "nomad-dynamic-app" {

  rule {
    path         = "${vault_mount.kv.path}/data/${vault_kv_secret_v2.app.name}"
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
  rule {
    path         = "${vault_mount.db[0].path}/creds/${vault_database_secret_backend_role.app[0].name}"
    capabilities = ["read"]
  }
}

resource "vault_policy" "nomad-auth" {
  name   = "nomad-workloads"
  policy = data.vault_policy_document.nomad-auth.hcl
}
resource "vault_policy" "nomad-dynamic-app" {
  name   = "nomad-dynamic-app"
  policy = data.vault_policy_document.nomad-dynamic-app.hcl
}
resource "vault_policy" "nomad-mysql" {
  name   = "nomad-mysql"
  policy = data.vault_policy_document.nomad-mysql.hcl
}