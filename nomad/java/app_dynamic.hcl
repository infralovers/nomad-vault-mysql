job "java-app" {
  datacenters = ["dc1"]
  type        = "service"

  group "java-app" {
    count = 1

    network {
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
      ]

      check {
        type     = "http"
        method   = "GET"
        interval = "10s"
        timeout  = "2s"
        path     = "/health"
      }
    }

    restart {
      attempts = 10
      interval = "5m"
      delay    = "25s"
      mode     = "delay"
    }

    task "java-app" {
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
{{ range service "mysql-server" }}
Address = {{ .Address }}
Port    = {{ .Port }}
{{ end }}
{{ with secret "dynamic-app/kv/database" }}
Database = {{ .Data.data.database }}
{{ end }}
{{ with secret "dynamic-app/db/creds/app" }}
User     = {{ .Data.username }}
Password = {{ .Data.password }}
{{ end }}

[VAULT]
Enabled = False
EOF
      }

      env {
        APP_CONFIG_PATH = "${NOMAD_TASK_DIR}/config/config.ini"
        SERVER_PORT     = "${NOMAD_PORT_web}"
      }

      resources {
        cpu    = 256
        memory = 256
      }
    }
  }
}
