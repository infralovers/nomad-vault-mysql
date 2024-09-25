job "dynamic-app" {
  datacenters = ["core"]
  type        = "service"
  namespace   = "demo"

  group "dynamic-app" {
    count = 1

    vault {
      policies      = ["nomad-dynamic-app"]
      change_mode   = "signal"
      change_signal = "SIGINT"
    }

    service {
      name = "dynamic-app"
      port = "web"

      check {
        type     = "http"
        method   = "GET"
        interval = "10s"
        timeout  = "2s"
        path     = "/health"
      }

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

    task "dynamic-app" {
      driver = "docker"

      config {
        image = "docker.io/mabunixda/dynamic-vault-app"
        volumes = [
          "local/config.ini:/usr/src/app/config/config.ini"
        ]

        ports = ["web"]
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
    
    Database = my_app
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
    network {
      mode = "bridge"
      port "web" {
        to = 8080
      }
    }
  }
}
