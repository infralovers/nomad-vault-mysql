job "mysql-server" {
  datacenters = ["dc1"]
  type        = "service"

  group "mysql-server" {
    count = 1

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

      env = {
        "MYSQL_ROOT_PASSWORD" = "super-duper-password"
        "MYSQL_DATABASE" = "app"
        "MYSQL_USER" = "app"
        "MYSQL_PASSWORD" = "my-app-super-password"
      }

      config {
        image = "mysql:9"
        ports = ["db"]
        volumes = [
          "/srv/mysql/:/var/lib/mysql"
        ]
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
