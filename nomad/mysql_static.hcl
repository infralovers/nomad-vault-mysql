job "mysql-server" {
  datacenters = ["dc1"]
  type        = "service"

  group "mysql-server" {
    count = 1

    vault {
      policies      = ["nomad-dynamic-app", "nomad-mysql"]
      change_mode   = "signal"
      change_signal = "SIGINT"
    }

    service {
      name = "mysql-server"
      port = "db"
    }

    restart {
      attempts = 10
      interval = "5m"
      delay    = "25s"
      mode     = "delay"
    }

    task "mysql-server" {
      driver = "docker"

      config {
        image = "mysql:9"
        ports = ["db"]
        volumes = [
          "/srv/mysql/:/var/lib/mysql"
        ]
      }
      template {
        destination = "secrets/.envs"
        change_mode = "noop"
        env         = true
        data        = <<EOF
{{ with secret "dynamic-app/kv/database" }}
MYSQL_DATABASE = "{{ .Data.data.database }}"
MYSQL_USER = "{{ .Data.data.username }}"
MYSQL_PASSWORD = "{{ .Data.data.password }}"
{{ end }}

{{ with secret "dynamic-app/kv/database_root" }}
MYSQL_ROOT_PASSWORD = "{{ .Data.data.password }}"
{{ end }}
EOF
      }
      resources {
        cpu    = 500
        memory = 500
      }
    }
    network {
      mode = "bridge"
      port "db" {
        static = 3306
        to     = 3306
      }
    }
  }
}
