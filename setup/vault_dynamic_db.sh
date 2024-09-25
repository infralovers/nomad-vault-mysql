#!/usr/bin/env bash

set -o xtrace

VAULT_MOUNT="dynamic-app/db"
MYSQL_ENDPOINT="mysql-server.service.consul"
VAULT_ADDR=${VAULT_ADDR:-}
VAULT_TOKEN=${VAULT_TOKEN:-}
VAULT_FORMAT="json"

export VAULT_FORMAT

# vault secrets enable -path=$VAULT_MOUNT database

vault write $VAULT_MOUNT/config/mysql \
    plugin_name=mysql-database-plugin \
    connection_url="{{username}}:{{password}}@tcp(${MYSQL_ENDPOINT}:3306)/" \
    allowed_roles="*" \
    username="root" \
    password="super-duper-password"

# vault write  -force $VAULT_MOUNT/database/rotate-root/mysql

vault write $VAULT_MOUNT/roles/app-long \
    db_name=mysql \
    creation_statements="CREATE USER '{{name}}'@'%' IDENTIFIED BY '{{password}}';GRANT ALL ON my_app.* TO '{{name}}'@'%';" \
    default_ttl="1h" \
    max_ttl="24h"

# Create a role with a shorter TTL
vault write $VAULT_MOUNT/roles/app \
    db_name=mysql \
    creation_statements="CREATE USER '{{name}}'@'%' IDENTIFIED BY '{{password}}';GRANT ALL ON my_app.* TO '{{name}}'@'%';" \
    default_ttl="3m" \
    max_ttl="6m"

vault read $VAULT_MOUNT/creds/app

vault read $VAULT_MOUNT/creds/app-long


POLICY=""
if [ -n "$(vault policy list | grep nomad-dynamic-app)" ]; then
  POLICY=$(vault policy read nomad-dynamic-app | jq -r '.policy')
fi 

echo "
$POLICY

path \"$VAULT_MOUNT/creds/app\" { 
  capabilities = [ \"read\"] 
}
"  | vault policy write nomad-dynamic-app - 
