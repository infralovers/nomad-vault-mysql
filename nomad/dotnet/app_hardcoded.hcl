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

    service {
      name = "dynamic-app"
      port = "web"
      tags = [ "traefik.enable=true", 
                "traefik.http.routers.app.rule=Host(`app.127.0.0.1.nip.io`)" ]

      check {
        type     = "http"
        method   = "GET"
        interval = "10s"
        timeout  = "2s"
        path     = "/health"
      }
    }

    task "dynamic-app" {
      driver = "docker"

      config {
        image = "quay.io/infralovers/nomad-vault-mysql-dotnet"
        volumes = [
          "local/config.ini:/app/config/config.ini"
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
    {{ range service "mysql-server" }}
    Address = {{ .Address }}
    Port = {{ .Port }}
    {{end}}

    Database = app
    User = app
    Password = my-app-super-password
    [VAULT]
    Enabled = False
EOF
      }
      resources {
        cpu    = 256
        memory = 256
      }
    }
    network {
      port "web" {
        to = 8080
      }
    }
  }
}
