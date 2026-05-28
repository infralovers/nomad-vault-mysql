package io.infralovers.nomadvaultmysql.service;

import io.infralovers.nomadvaultmysql.config.AppProperties;
import io.infralovers.nomadvaultmysql.model.Customer;
import io.infralovers.nomadvaultmysql.model.CustomerInput;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.sql.*;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;

/**
 * JDBC-based customer repository.
 * Uses raw JDBC (no Spring Data) to mirror the style of the Python and .NET implementations.
 * Includes init-time retry loop, table creation, and seed data insertion.
 */
public class CustomerRepository {

    private static final Logger log = LoggerFactory.getLogger(CustomerRepository.class);

    private static final String CREATE_TABLE = """
            CREATE TABLE IF NOT EXISTS `customers` (
                `cust_no`                int(11) NOT NULL AUTO_INCREMENT,
                `birth_date`             varchar(255) NOT NULL,
                `first_name`             varchar(255) NOT NULL,
                `last_name`              varchar(255) NOT NULL,
                `create_date`            varchar(255) NOT NULL,
                `social_security_number` varchar(255) NOT NULL,
                `credit_card_number`     varchar(255) NOT NULL,
                `address`                varchar(255) NOT NULL,
                `salary`                 varchar(255) NOT NULL,
                PRIMARY KEY (`cust_no`)
            ) ENGINE=InnoDB;
            """;

    private static final String SEED = """
            INSERT IGNORE INTO customers VALUES
              (2,  '3/14/69',  'Larry',   'Johnson', '2020-01-01T14:49:12.301977', '360-56-6750',  '3600-5600-6750-0000', 'Tyler, Texas',       '7000000'),
              (40, '11/26/69', 'Shawn',   'Kemp',    '2020-02-21T10:24:55.985726', '235-32-8091',  '2350-3200-8091-0001', 'Elkhart, Indiana',   '15000000'),
              (34, '2/20/63',  'Charles', 'Barkley', '2019-04-09T01:10:20.548144', '531-72-1553',  '5310-7200-1553-0002', 'Leeds, Alabama',     '9000000');
            """;

    private final AppProperties props;
    private final VaultService  vault;

    private String  connectionString;
    private boolean initialized = false;

    public boolean isInitialized() { return initialized; }

    public CustomerRepository(AppProperties props, VaultService vault) {
        this.props = props;
        this.vault = vault;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public synchronized void initialize() throws InterruptedException {
        if (initialized) return;

        vault.initialize();

        AppProperties.DatabaseConfig db = props.getDatabase();
        String user     = db.getUser();
        String password = db.getPassword();

        if (vault.isEnabled() && !props.getVault().getDatabaseAuthPath().isBlank()) {
            VaultService.DbCreds creds = vault.readDatabaseCredentials(props.getVault().getDatabaseAuthPath());
            if (creds != null) {
                user     = creds.user();
                password = creds.password();
            }
        }

        String baseUrl = String.format("jdbc:mysql://%s:%d/", db.getAddress(), db.getPort());
        String dbName  = db.getDatabase();

        // Retry loop (mirrors Python's 10-attempt loop)
        for (int attempt = 0; attempt < 10; attempt++) {
            try (Connection conn = DriverManager.getConnection(baseUrl + "?allowPublicKeyRetrieval=true&useSSL=false", user, password)) {
                log.info("Connected to MySQL at {}:{}", db.getAddress(), db.getPort());
                try (Statement st = conn.createStatement()) {
                    st.execute("CREATE DATABASE IF NOT EXISTS `" + dbName + "`");
                    st.execute("USE `" + dbName + "`");
                    st.execute(CREATE_TABLE);
                    st.execute(SEED);
                    conn.commit();
                } catch (SQLException e) {
                    if (!conn.getAutoCommit()) conn.rollback();
                    throw e;
                }
                break;
            } catch (SQLException e) {
                log.warn("DB connection attempt {}/10 failed: {}. Retrying in 3 s...", attempt + 1, e.getMessage());
                Thread.sleep(3_000);
                if (attempt == 9) throw new RuntimeException("Could not connect to database after 10 attempts", e);
            }
        }

        connectionString = String.format(
                "jdbc:mysql://%s:%d/%s?allowPublicKeyRetrieval=true&useSSL=false",
                db.getAddress(), db.getPort(), dbName);
        // Store effective credentials in a finalised field
        effectiveUser     = user;
        effectivePassword = password;
        initialized = true;
        log.info("Database initialised ({})", dbName);
    }

    // Effective credentials stored after initialization
    private String effectiveUser;
    private String effectivePassword;

    // ── Queries ───────────────────────────────────────────────────────────────

    public List<Customer> getCustomerRecords(int limit, boolean raw) throws SQLException {
        ensureInitialized();
        List<Customer> results = new ArrayList<>();
        try (Connection conn = getConnection();
             PreparedStatement ps = conn.prepareStatement("SELECT * FROM `customers` LIMIT ?")) {
            ps.setInt(1, limit);
            try (ResultSet rs = ps.executeQuery()) {
                while (rs.next()) {
                    results.add(mapRow(rs, raw));
                }
            }
        }
        return results;
    }

    public List<Customer> getCustomerRecord(int customerId) throws SQLException {
        ensureInitialized();
        List<Customer> results = new ArrayList<>();
        try (Connection conn = getConnection();
             PreparedStatement ps = conn.prepareStatement("SELECT * FROM `customers` WHERE cust_no = ?")) {
            ps.setInt(1, customerId);
            try (ResultSet rs = ps.executeQuery()) {
                while (rs.next()) {
                    results.add(mapRow(rs, false));
                }
            }
        }
        return results;
    }

    public List<Customer> insertCustomerRecord(CustomerInput input) throws SQLException {
        ensureInitialized();
        String birthDate  = vault.isEnabled() ? vault.encrypt(input.getBirthDate())  : input.getBirthDate();
        String ssn        = vault.isEnabled() ? vault.encrypt(input.getSsn())        : input.getSsn();
        String ccn        = vault.isEnabled() ? vault.encrypt(input.getCcn())        : input.getCcn();
        String address    = vault.isEnabled() ? vault.encrypt(input.getAddress())    : input.getAddress();
        String salary     = vault.isEnabled() ? vault.encrypt(input.getSalary())     : input.getSalary();
        String createDate = input.getCreateDate().isBlank() ? Instant.now().toString() : input.getCreateDate();

        try (Connection conn = getConnection();
             PreparedStatement ps = conn.prepareStatement(
                     "INSERT INTO `customers` (`birth_date`,`first_name`,`last_name`,`create_date`," +
                     "`social_security_number`,`credit_card_number`,`address`,`salary`) VALUES (?,?,?,?,?,?,?,?)",
                     Statement.RETURN_GENERATED_KEYS)) {
            ps.setString(1, birthDate);
            ps.setString(2, input.getFirstName());
            ps.setString(3, input.getLastName());
            ps.setString(4, createDate);
            ps.setString(5, ssn);
            ps.setString(6, ccn);
            ps.setString(7, address);
            ps.setString(8, salary);
            ps.executeUpdate();
        }
        return getCustomerRecords(50, false);
    }

    public List<Customer> updateCustomerRecord(CustomerInput input) throws SQLException {
        ensureInitialized();
        if (input.getCustNo() == null) {
            throw new IllegalArgumentException("cust_no is required for update");
        }
        String birthDate = vault.isEnabled() ? vault.encrypt(input.getBirthDate()) : input.getBirthDate();
        String ssn       = vault.isEnabled() ? vault.encrypt(input.getSsn())       : input.getSsn();
        String ccn       = vault.isEnabled() ? vault.encrypt(input.getCcn())       : input.getCcn();
        String address   = vault.isEnabled() ? vault.encrypt(input.getAddress())   : input.getAddress();
        String salary    = vault.isEnabled() ? vault.encrypt(input.getSalary())    : input.getSalary();

        try (Connection conn = getConnection();
             PreparedStatement ps = conn.prepareStatement(
                     "UPDATE `customers` SET `birth_date`=?,`first_name`=?,`last_name`=?," +
                     "`social_security_number`=?,`credit_card_number`=?,`address`=?,`salary`=? WHERE `cust_no`=?")) {
            ps.setString(1, birthDate);
            ps.setString(2, input.getFirstName());
            ps.setString(3, input.getLastName());
            ps.setString(4, ssn);
            ps.setString(5, ccn);
            ps.setString(6, address);
            ps.setString(7, salary);
            ps.setInt(8, input.getCustNo());
            ps.executeUpdate();
        }
        return getCustomerRecords(50, false);
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private Connection getConnection() throws SQLException {
        return DriverManager.getConnection(connectionString, effectiveUser, effectivePassword);
    }

    private Customer mapRow(ResultSet rs, boolean raw) throws SQLException {
        Customer c = new Customer();
        c.setCustomerNumber(rs.getInt("cust_no"));
        c.setBirthDate(raw ? rs.getString("birth_date")             : vault.decrypt(rs.getString("birth_date")));
        c.setFirstName(rs.getString("first_name"));
        c.setLastName(rs.getString("last_name"));
        c.setCreateDate(rs.getString("create_date"));
        c.setSsn(raw  ? rs.getString("social_security_number")      : vault.decrypt(rs.getString("social_security_number")));
        c.setCcn(raw  ? rs.getString("credit_card_number")          : vault.decrypt(rs.getString("credit_card_number")));
        c.setAddress(raw ? rs.getString("address")                  : vault.decrypt(rs.getString("address")));
        c.setSalary(raw  ? rs.getString("salary")                   : vault.decrypt(rs.getString("salary")));
        return c;
    }

    private void ensureInitialized() {
        if (!initialized) throw new IllegalStateException("Repository not initialized");
    }
}
