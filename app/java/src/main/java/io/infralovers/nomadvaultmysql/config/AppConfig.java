package io.infralovers.nomadvaultmysql.config;

import io.infralovers.nomadvaultmysql.service.CustomerRepository;
import io.infralovers.nomadvaultmysql.service.VaultService;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;

/**
 * Reads config/config.ini (same format as the Python and .NET implementations)
 * and wires up the service beans.
 *
 * Config file path resolution order:
 *   1. APP_CONFIG_PATH environment variable
 *   2. config/config.ini relative to working directory (default)
 */
@Configuration
public class AppConfig {

    private static final Logger log = LoggerFactory.getLogger(AppConfig.class);

    @Bean
    public AppProperties appProperties() {
        String configPath = System.getenv("APP_CONFIG_PATH");
        if (configPath == null || configPath.isBlank()) {
            configPath = "config/config.ini";
        }
        Path path = Paths.get(configPath);
        if (!Files.exists(path)) {
            log.warn("Config file not found at {}, using defaults.", path.toAbsolutePath());
            return new AppProperties();
        }
        try {
            AppProperties props = parseIniFile(path);
            log.info("Loaded config from {}", path.toAbsolutePath());
            return props;
        } catch (IOException e) {
            log.error("Failed to read config file {}: {}", path, e.getMessage());
            return new AppProperties();
        }
    }

    @Bean
    public VaultService vaultService(AppProperties props) {
        return new VaultService(props);
    }

    @Bean
    public CustomerRepository customerRepository(AppProperties props, VaultService vaultService) {
        return new CustomerRepository(props, vaultService);
    }

    // ── INI parser ────────────────────────────────────────────────────────────

    private static AppProperties parseIniFile(Path path) throws IOException {
        AppProperties props = new AppProperties();
        String currentSection = "DEFAULT";

        for (String rawLine : Files.readAllLines(path)) {
            String line = rawLine.trim();
            if (line.isEmpty() || line.startsWith("#") || line.startsWith(";")) {
                continue;
            }
            if (line.startsWith("[") && line.endsWith("]")) {
                currentSection = line.substring(1, line.length() - 1).toUpperCase();
                continue;
            }
            int eq = line.indexOf('=');
            if (eq < 0) continue;
            String key   = line.substring(0, eq).trim().toLowerCase();
            String value = line.substring(eq + 1).trim();

            switch (currentSection) {
                case "DATABASE" -> applyDatabase(props.getDatabase(), key, value);
                case "VAULT"    -> applyVault(props.getVault(), key, value);
                default         -> applyDefault(props, key, value);
            }
        }
        return props;
    }

    private static void applyDefault(AppProperties p, String key, String value) {
        switch (key) {
            case "port"     -> p.setPort(parseInt(value, 8080));
            case "loglevel" -> p.setLogLevel(value);
        }
    }

    private static void applyDatabase(AppProperties.DatabaseConfig db, String key, String value) {
        switch (key) {
            case "address"  -> db.setAddress(value);
            case "port"     -> db.setPort(parseInt(value, 3306));
            case "database" -> db.setDatabase(value);
            case "user"     -> db.setUser(value);
            case "password" -> db.setPassword(value);
        }
    }

    private static void applyVault(AppProperties.VaultConfig v, String key, String value) {
        switch (key) {
            case "enabled"         -> v.setEnabled(parseBool(value));
            case "injecttoken"     -> v.setInjectToken(parseBool(value));
            case "namespace"       -> v.setNamespace(value);
            case "address"         -> v.setAddress(value);
            case "token"           -> v.setToken(value);
            case "keypath"         -> v.setKeyPath(value);
            case "keyname"         -> v.setKeyName(value);
            case "databaseauthpath"-> v.setDatabaseAuthPath(value);
            case "transform"       -> v.setTransform(parseBool(value));
        }
    }

    private static int parseInt(String value, int defaultValue) {
        try { return Integer.parseInt(value); } catch (NumberFormatException e) { return defaultValue; }
    }

    private static boolean parseBool(String value) {
        return "true".equalsIgnoreCase(value) || "1".equals(value) || "yes".equalsIgnoreCase(value);
    }
}
