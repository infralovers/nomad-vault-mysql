#!/usr/bin/env bash

set -o xtrace

VAULT_MOUNT="dynamic-app/kv"

VAULT_ADDR=${VAULT_ADDR:-}
VAULT_TOKEN=${VAULT_TOKEN:-}

if [ -z "$VAULT_ADDR" ]; then
  echo "VAULT_ADDR is not set"
  exit 1
fi
if [ -z "$VAULT_TOKEN" ]; then
  echo "VAULT_TOKEN is not set"
  exit 1
fi

vault secrets enable -path=$VAULT_MOUNT kv 
vault kv put $VAULT_MOUNT/database username="root" password="super-duper-password"

if [ -n "$(vault policy list | grep nomad-dynamic-app)" ]; then
  exit 0
fi 
echo "
path \"$VAULT_MOUNT/database\" { 
  capabilities = [ \"read\" ] 
}
" | vault policy write nomad-dynamic-app - 

