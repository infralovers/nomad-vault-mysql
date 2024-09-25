#!/usr/bin/env bash

set -o xtrace

VAULT_MOUNT="dynamic-app/transit"
TRANIT_KEY="app"
MYSQL_ENDPOINT="mysql-server.service.consul"
VAULT_ADDR=${VAULT_ADDR:-}
VAULT_TOKEN=${VAULT_TOKEN:-}
VAULT_FORMAT="json"

export VAULT_FORMAT

vault secrets enable  -path=$VAULT_MOUNT transit

# Create our customer key
vault write  -f $VAULT_MOUNT/keys/$TRANIT_KEY

# Create our archive key to demonstrate multiple keys
vault write -f $VAULT_MOUNT/keys/$TRANIT_KEY



DATA="4111 1111 1111 1111"
CIPHERTEXT=$(vault write -format=json \
    $VAULT_MOUNT/encrypt/$TRANIT_KEY \
    plaintext=$(base64 <<< "${DATA}")\
     | jq -r '.data | .ciphertext')

DECODED=$(vault write $VAULT_MOUNT/decrypt/$TRANIT_KEY -format=json \
              ciphertext=$CIPHERTEXT \
              | jq -r '.data | .plaintext' | base64 --decode)
            

cat <<EOF
DATA: $DATA
CIPHERTEXT: $CIPHERTEXT
DECODED: $DECODED
EOF


POLICY=""
if [ -n "$(vault policy list | grep nomad-dynamic-app)" ]; then
  POLICY=$(vault policy read nomad-dynamic-app | jq -r '.policy')
fi 

echo "
$POLICY

path \"$VAULT_MOUNT/encrypt/app\" { 
  capabilities = [ \"create\", \"update\" ] 
}
path \"$VAULT_MOUNT/decrypt/app\" { 
  capabilities = [ \"create\", \"update\" ] 
}
" | vault policy write nomad-dynamic-app - 
