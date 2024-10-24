job "dynamic-app" {
  datacenters = ["dc1"]
  type        = "service"

  group "dynamic-app" {
    count = 1

    restart {
      attempts = 10
      interval = "5m"
      delay    = "25s"
      mode     = "delay"
    }

    network {
      mode = "bridge"
    }

    vault {
      policies      = ["nomad-dynamic-app"]
      change_mode   = "signal"
      change_signal = "SIGINT"
    }

    service {
      name = "dynamic-app"
      port = "8080"
      tags = ["traefik.enable=true",
        "traefik.http.routers.dynamic-app.rule=Host(`dynamic-app.127.0.0.1.nip.io`)",
        "traefik.http.routers.dynamic-app.entrypoints=https",
        "traefik.http.routers.dynamic-app.tls=true",
        "traefik.connsulcatalog.connect=true"
      ]
      connect {
        sidecar_service {
          proxy {
            upstreams {
              destination_name = "mysql-server"
              local_bind_port  = 3306
            }
          }
        }
      }
      check {
        expose   = true
        type     = "http"
        name     = "heatlh"
        method   = "GET"
        interval = "10s"
        timeout  = "2s"
        path     = "/health"
      }
    }

    task "dynamic-app" {
      driver = "docker"

      config {
        image = "docker.io/mabunixda/dynamic-vault-app"
        volumes = [
          "local/config.ini:/usr/src/app/config/config.ini"
        ]
        ports = [8080]
      }

      template {
        destination = "local/config.ini"
        data        = <<EOF
    [DEFAULT]
    LogLevel = DEBUG
    Port = 8080
    [DATABASE]
    Address = 127.0.0.1
    Port = 3306
    
    {{ with secret "dynamic-app/kv/database" }}
    Database = {{ .Data.data.database }}
    {{ end }}
    {{ with secret "dynamic-app/db/creds/app" }}
    User = {{ .Data.username }}
    Password = {{ .Data.password }}
    {{ end }}

    [VAULT]
    Enabled = True
    InjectToken = True
    Namespace = 
    Address = {{ env "VAULT_ADDR" }}
    KeyPath = dynamic-app/transit
    KeyName = app
EOF
      }
      resources {
        cpu    = 256
        memory = 256
      }
    }
  }
}
