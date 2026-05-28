package io.infralovers.nomadvaultmysql.service;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import io.infralovers.nomadvaultmysql.config.AppProperties;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.security.SecureRandom;
import java.security.cert.X509Certificate;
import java.time.Duration;
import java.util.Base64;

import javax.net.ssl.SSLContext;
import javax.net.ssl.SSLParameters;
import javax.net.ssl.TrustManager;
import javax.net.ssl.X509TrustManager;

/**
 * Raw-HTTP Vault client — mirrors the approach used by the .NET implementation
 * so there is no Vault SDK dependency.
 *
 * Supported operations:
 *   - initialize()           verify reachability and store the token
 *   - encrypt(value)         Transit encrypt via /v1/{keyPath}/encrypt/{keyName}
 *   - decrypt(value)         Transit decrypt (passthrough if not vault:v*)
 *   - readDatabaseCredentials(path)  dynamic DB creds from /v1/{path}
 */
public class VaultService {

    private static final Logger log = LoggerFactory.getLogger(VaultService.class);

    private final AppProperties.VaultConfig config;
    private final HttpClient                http;
    private final ObjectMapper              json = new ObjectMapper();

    private String  token     = "";
    private boolean enabled   = false;

    public boolean isEnabled() { return enabled; }

    public VaultService(AppProperties props) {
        this.config = props.getVault();
        this.http   = createVaultHttpClient();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void initialize() {
        if (!config.isEnabled()) {
            log.info("Vault integration is disabled.");
            return;
        }

        token = config.isInjectToken()
                ? System.getenv().getOrDefault("VAULT_TOKEN", "").strip()
                : config.getToken().strip();

        if (config.getAddress().isBlank() || token.isBlank()) {
            log.warn("Vault address or token is missing — skipping Vault initialisation.");
            return;
        }

        try {
            HttpRequest req = buildGet("/v1/sys/health");
            HttpResponse<String> res = http.send(req, HttpResponse.BodyHandlers.ofString());
            // 200 = initialised+unsealed, 429 = standby — both mean reachable
            if (res.statusCode() >= 500) {
                log.warn("Vault health returned {}; continuing without Vault.", res.statusCode());
                return;
            }
            enabled = true;
            log.info("Vault initialised at {}", config.getAddress());
        } catch (Exception e) {
            log.warn("Vault unreachable ({}); continuing without Vault.", e.getMessage());
        }
    }

    private HttpClient createVaultHttpClient() {
        try {
            TrustManager[] trustAllCerts = new TrustManager[] {
                    new X509TrustManager() {
                        @Override
                        public void checkClientTrusted(X509Certificate[] chain, String authType) {
                        }

                        @Override
                        public void checkServerTrusted(X509Certificate[] chain, String authType) {
                        }

                        @Override
                        public X509Certificate[] getAcceptedIssuers() {
                            return new X509Certificate[0];
                        }
                    }
            };

            SSLContext sslContext = SSLContext.getInstance("TLS");
            sslContext.init(null, trustAllCerts, new SecureRandom());

            SSLParameters sslParameters = new SSLParameters();
            sslParameters.setEndpointIdentificationAlgorithm("");

            return HttpClient.newBuilder()
                    .connectTimeout(Duration.ofSeconds(5))
                    .sslContext(sslContext)
                    .sslParameters(sslParameters)
                    .build();
        } catch (Exception e) {
            log.warn("Falling back to default TLS validation for Vault HTTP client: {}", e.getMessage());
            return HttpClient.newBuilder()
                    .connectTimeout(Duration.ofSeconds(5))
                    .build();
        }
    }

    // ── Transit Encrypt ───────────────────────────────────────────────────────

    public String encrypt(String value) {
        if (!enabled || value == null || value.isBlank()) return value;
        try {
            String b64 = Base64.getEncoder().encodeToString(value.getBytes(StandardCharsets.UTF_8));
            String body = json.writeValueAsString(new PlainTextPayload(b64));
            String path = String.format("/v1/%s/encrypt/%s", config.getKeyPath(), config.getKeyName());
            HttpRequest req = buildPost(path, body);
            HttpResponse<String> res = http.send(req, HttpResponse.BodyHandlers.ofString());
            checkStatus(res, "encrypt");
            JsonNode root = json.readTree(res.body());
            return root.path("data").path("ciphertext").asText(value);
        } catch (Exception e) {
            log.error("Vault encrypt failed: {}", e.getMessage());
            throw new RuntimeException("Vault encrypt failed", e);
        }
    }

    // ── Transit Decrypt ───────────────────────────────────────────────────────

    public String decrypt(String value) {
        if (!enabled || value == null || !value.startsWith("vault:v")) return value;
        try {
            String body = json.writeValueAsString(new CiphertextPayload(value));
            String path = String.format("/v1/%s/decrypt/%s", config.getKeyPath(), config.getKeyName());
            HttpRequest req = buildPost(path, body);
            HttpResponse<String> res = http.send(req, HttpResponse.BodyHandlers.ofString());
            checkStatus(res, "decrypt");
            JsonNode root = json.readTree(res.body());
            String b64 = root.path("data").path("plaintext").asText("");
            return new String(Base64.getDecoder().decode(b64), StandardCharsets.UTF_8);
        } catch (Exception e) {
            log.error("Vault decrypt failed: {}", e.getMessage());
            throw new RuntimeException("Vault decrypt failed", e);
        }
    }

    // ── Database Credentials ──────────────────────────────────────────────────

    public record DbCreds(String user, String password) {}

    public DbCreds readDatabaseCredentials(String path) {
        if (!enabled || path == null || path.isBlank()) return null;
        try {
            HttpRequest req = buildGet("/v1/" + path);
            HttpResponse<String> res = http.send(req, HttpResponse.BodyHandlers.ofString());
            checkStatus(res, "db-creds");
            JsonNode data = json.readTree(res.body()).path("data");
            if (data.has("username")) {
                return new DbCreds(data.get("username").asText(), data.get("password").asText());
            }
            // nested data.data (KV v2)
            JsonNode nested = data.path("data");
            if (nested.has("username")) {
                return new DbCreds(nested.get("username").asText(), nested.get("password").asText());
            }
            return null;
        } catch (Exception e) {
            log.error("Failed to read DB creds from {}: {}", path, e.getMessage());
            return null;
        }
    }

    // ── HTTP helpers ──────────────────────────────────────────────────────────

    private HttpRequest buildGet(String vaultPath) {
        HttpRequest.Builder b = HttpRequest.newBuilder()
                .uri(URI.create(config.getAddress() + vaultPath))
                .GET()
                .header("X-Vault-Token", token)
                .timeout(Duration.ofSeconds(10));
        if (!config.getNamespace().isBlank()) {
            b.header("X-Vault-Namespace", config.getNamespace());
        }
        return b.build();
    }

    private HttpRequest buildPost(String vaultPath, String body) {
        HttpRequest.Builder b = HttpRequest.newBuilder()
                .uri(URI.create(config.getAddress() + vaultPath))
                .POST(HttpRequest.BodyPublishers.ofString(body, StandardCharsets.UTF_8))
                .header("Content-Type", "application/json")
                .header("X-Vault-Token", token)
                .timeout(Duration.ofSeconds(10));
        if (!config.getNamespace().isBlank()) {
            b.header("X-Vault-Namespace", config.getNamespace());
        }
        return b.build();
    }

    private static void checkStatus(HttpResponse<String> res, String op) {
        if (res.statusCode() < 200 || res.statusCode() >= 300) {
            throw new RuntimeException("Vault " + op + " returned HTTP " + res.statusCode() + ": " + res.body());
        }
    }

    // ── Internal payload records ───────────────────────────────────────────────

    record PlainTextPayload(String plaintext) {}
    record CiphertextPayload(String ciphertext) {}
}
