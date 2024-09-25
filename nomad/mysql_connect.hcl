job "mysql-server" {
  datacenters = ["core"]
  type        = "service"
  namespace   = "demo"

  group "mysql-server" {
    count = 1

    service {
      name = "mysql-server"
      port = "db"

      connect {
        sidecar_service {}
      }


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
      }

      config {
        image = "mysql:9"

        ports = ["db"]
      }

      resources {
        cpu    = 500
        memory = 1024
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
