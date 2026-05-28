package io.infralovers.nomadvaultmysql.config;

public class AppProperties {

    private int port = 8080;
    private String logLevel = "INFO";
    private DatabaseConfig database = new DatabaseConfig();
    private VaultConfig vault = new VaultConfig();

    public int getPort() { return port; }
    public void setPort(int port) { this.port = port; }

    public String getLogLevel() { return logLevel; }
    public void setLogLevel(String logLevel) { this.logLevel = logLevel; }

    public DatabaseConfig getDatabase() { return database; }
    public void setDatabase(DatabaseConfig database) { this.database = database; }

    public VaultConfig getVault() { return vault; }
    public void setVault(VaultConfig vault) { this.vault = vault; }

    public static class DatabaseConfig {
        private String address = "localhost";
        private int port = 3306;
        private String database = "customers";
        private String user = "root";
        private String password = "root";

        public String getAddress() { return address; }
        public void setAddress(String address) { this.address = address; }

        public int getPort() { return port; }
        public void setPort(int port) { this.port = port; }

        public String getDatabase() { return database; }
        public void setDatabase(String database) { this.database = database; }

        public String getUser() { return user; }
        public void setUser(String user) { this.user = user; }

        public String getPassword() { return password; }
        public void setPassword(String password) { this.password = password; }
    }

    public static class VaultConfig {
        private boolean enabled = false;
        private boolean injectToken = false;
        private String namespace = "";
        private String address = "";
        private String token = "";
        private String keyPath = "";
        private String keyName = "";
        private String databaseAuthPath = "";
        private boolean transform = false;

        public boolean isEnabled() { return enabled; }
        public void setEnabled(boolean enabled) { this.enabled = enabled; }

        public boolean isInjectToken() { return injectToken; }
        public void setInjectToken(boolean injectToken) { this.injectToken = injectToken; }

        public String getNamespace() { return namespace; }
        public void setNamespace(String namespace) { this.namespace = namespace; }

        public String getAddress() { return address; }
        public void setAddress(String address) { this.address = address; }

        public String getToken() { return token; }
        public void setToken(String token) { this.token = token; }

        public String getKeyPath() { return keyPath; }
        public void setKeyPath(String keyPath) { this.keyPath = keyPath; }

        public String getKeyName() { return keyName; }
        public void setKeyName(String keyName) { this.keyName = keyName; }

        public String getDatabaseAuthPath() { return databaseAuthPath; }
        public void setDatabaseAuthPath(String databaseAuthPath) { this.databaseAuthPath = databaseAuthPath; }

        public boolean isTransform() { return transform; }
        public void setTransform(boolean transform) { this.transform = transform; }
    }
}
