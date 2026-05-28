job "whoami" {
  datacenters = ["dc1"]
  type        = "service"

  group "whoami" {
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
    service {
      name = "whoami"
      port = "80"
      tags = ["traefik.enable=true",
        "traefik.http.routers.whoami.rule=Host(`whoami.127.0.0.1.nip.io`)",
        "traefik.http.routers.whoami.entrypoints=http",
        "traefik.http.routers.whoami.tls=false",
        "traefik.consulcatalog.connect=true"
      ]
      connect {
        sidecar_service {}
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
    task "whoami" {
      driver = "docker"

      config {
        image = "containous/whoami"
        ports = [80]

      }

      resources {
        cpu    = 100
        memory = 100
      }
    }
  }
}