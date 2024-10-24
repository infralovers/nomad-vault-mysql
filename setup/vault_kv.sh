#!/usr/bin/env bash

set -o xtrace

VAULT_MOUNT="dynamic-app/kv"
VAULT_ADDR=${VAULT_ADDR:-}
VAULT_TOKEN=${VAULT_TOKEN:-}
VAULT_FORMAT="json"

export VAULT_FORMAT

if [ -z "$VAULT_ADDR" ]; then
  echo "VAULT_ADDR is not set"
  exit 1
fi
if [ -z "$VAULT_TOKEN" ]; then
  echo "VAULT_TOKEN is not set"
  exit 1
fi

if [ "$(vault secrets list  | jq  ' . | keys' | grep "$VAULT_MOUNT" | wc -l  | tr -d ' ')" -eq 0 ]; then
  vault secrets enable -path=$VAULT_MOUNT kv 
fi
vault kv put $VAULT_MOUNT/database username="app" password="my-app-super-password" database="app"
vault kv put $VAULT_MOUNT/database_root username="root" password="super-duper-password"

if [ -n "$(vault policy list | grep nomad-dynamic-app)" ]; then
  exit 0
fi 

echo "
path \"$VAULT_MOUNT/database\" { 
  capabilities = [ \"read\" ] 
}
" | vault policy write nomad-dynamic-app - 


echo "
path \"$VAULT_MOUNT/database_root\" { 
  capabilities = [ \"read\" ] 
}
" | vault policy write nomad-mysql - 
