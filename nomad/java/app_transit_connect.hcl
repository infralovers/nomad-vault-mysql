job "java-app" {
  datacenters = ["dc1"]
  type        = "service"

  group "java-app" {
    count = 1

    network {
      mode = "bridge"
      port "web" {}
    }

    vault {
      policies      = ["nomad-dynamic-app"]
      change_mode   = "signal"
      change_signal = "SIGINT"
    }

    service {
      name = "java-app"
      port = "web"
      tags = [
        "traefik.enable=true",
        "traefik.http.routers.java-app.rule=Host(`java-app.127.0.0.1.nip.io`)",
        "traefik.http.services.java-app.loadbalancer.server.port=${NOMAD_HOST_PORT_web}",
      ]

      check {
        type     = "http"
        method   = "GET"
        interval = "10s"
        timeout  = "2s"
        path     = "/health"
      }

      # Consul Connect sidecar routes MySQL traffic through the service mesh
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
    }

    restart {
      attempts = 10
      interval = "5m"
      delay    = "25s"
      mode     = "delay"
    }

    task "java-app" {
      # ── Nomad Java driver — runs the fat JAR directly, no Docker required ──
      driver = "java"

      config {
        jar_path    = "local/app.jar"
        jvm_options = ["-Xmx256m", "-Xms128m"]
      }

      artifact {
        source      = "https://github.com/infralovers/nomad-vault-mysql/releases/download/java-artifact-latest/nomad-vault-mysql-java.jar"
        destination = "local/"
      }

      template {
        destination = "local/config/config.ini"
        data        = <<EOF
[DEFAULT]
LogLevel = DEBUG
Port = {{ env "NOMAD_PORT_web" }}

[DATABASE]
Address  = 127.0.0.1
Port     = 3306

{{ with secret "dynamic-app/kv/database" }}
Database = {{ .Data.data.database }}
{{ end }}
{{ with secret "dynamic-app/db/creds/app" }}
User     = {{ .Data.username }}
Password = {{ .Data.password }}
{{ end }}

[VAULT]
Enabled     = True
InjectToken = True
Namespace   =
Address     = {{ env "VAULT_ADDR" }}
KeyPath     = dynamic-app/transit
KeyName     = app
EOF
      }

      env {
        APP_CONFIG_PATH = "${NOMAD_TASK_DIR}/config/config.ini"
        SERVER_PORT     = "${NOMAD_PORT_web}"
      }

      resources {
        cpu    = 500
        memory = 512
      }
    }
  }
}
