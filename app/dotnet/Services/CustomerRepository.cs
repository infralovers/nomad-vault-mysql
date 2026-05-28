using MySqlConnector;
using NomadVaultMySqlDotnet.Configuration;
using NomadVaultMySqlDotnet.Models;

namespace NomadVaultMySqlDotnet.Services;

public sealed class CustomerRepository
{
    private const string CustomerTable = """
CREATE TABLE IF NOT EXISTS `customers` (
    `cust_no` int(11) NOT NULL AUTO_INCREMENT,
    `birth_date` varchar(255) NOT NULL,
    `first_name` varchar(255) NOT NULL,
    `last_name` varchar(255) NOT NULL,
    `create_date` varchar(255) NOT NULL,
    `social_security_number` varchar(255) NOT NULL,
    `credit_card_number` varchar(255) NOT NULL,
    `address` varchar(255) NOT NULL,
    `salary` varchar(255) NOT NULL,
    PRIMARY KEY (`cust_no`)
) ENGINE=InnoDB;
""";

    private const string SeedCustomers = """
INSERT IGNORE INTO customers VALUES
  (2, "3/14/69", "Larry", "Johnson", "2020-01-01T14:49:12.301977", "360-56-6750", "3600-5600-6750-0000", "Tyler, Texas", "7000000"),
  (40, "11/26/69", "Shawn", "Kemp", "2020-02-21T10:24:55.985726", "235-32-8091", "2350-3200-8091-0001", "Elkhart, Indiana", "15000000"),
  (34, "2/20/63", "Charles", "Barkley", "2019-04-09T01:10:20.548144", "531-72-1553", "5310-7200-1553-0002", "Leeds, Alabama", "9000000");
""";

    private readonly AppRuntimeConfig _config;
    private readonly VaultService _vaultService;
    private readonly ILogger<CustomerRepository> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private string _connectionString = string.Empty;

    public bool IsInitialized { get; private set; }

    public CustomerRepository(AppRuntimeConfig config, VaultService vaultService, ILogger<CustomerRepository> logger)
    {
        _config = config;
        _vaultService = vaultService;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (IsInitialized)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (IsInitialized)
            {
                return;
            }

            await _vaultService.InitializeAsync(_config, cancellationToken);

            var dbUser = _config.Database.User;
            var dbPassword = _config.Database.Password;

            if (_vaultService.IsEnabled && !string.IsNullOrWhiteSpace(_config.Vault.DatabaseAuthPath))
            {
                var creds = await _vaultService.ReadDatabaseCredentialsAsync(_config.Vault.DatabaseAuthPath, cancellationToken);
                if (creds.HasValue)
                {
                    dbUser = creds.Value.User;
                    dbPassword = creds.Value.Password;
                }
            }

            await ConnectAndPrepareAsync(dbUser, dbPassword, cancellationToken);
            IsInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<IReadOnlyList<CustomerRecord>> GetCustomerRecordsAsync(int limit = 50, bool raw = false, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var results = new List<CustomerRecord>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand("SELECT * FROM `customers` LIMIT @limit", connection);
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            try
            {
                results.Add(await ProcessCustomerAsync(reader, raw, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an error retrieving a customer record.");
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<CustomerRecord>> GetCustomerRecordAsync(int customerId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var results = new List<CustomerRecord>();
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new MySqlCommand("SELECT * FROM `customers` WHERE cust_no = @customerId", connection);
        command.Parameters.AddWithValue("@customerId", customerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            try
            {
                results.Add(await ProcessCustomerAsync(reader, raw: false, cancellationToken));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an error retrieving the customer record for id {CustomerId}.", customerId);
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<CustomerRecord>> InsertCustomerRecordAsync(CustomerInput record, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var normalized = await NormalizeCustomerInputAsync(record, includeCreateDate: true, cancellationToken);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
INSERT INTO `customers` (`birth_date`, `first_name`, `last_name`, `create_date`, `social_security_number`, `credit_card_number`, `address`, `salary`)
VALUES (@birthDate, @firstName, @lastName, @createDate, @ssn, @ccn, @address, @salary);
""";

        await using var command = new MySqlCommand(sql, connection);
        BindCommonParameters(command, normalized);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return await GetCustomerRecordsAsync(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<CustomerRecord>> UpdateCustomerRecordAsync(CustomerInput record, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (!record.CustNo.HasValue)
        {
            throw new ArgumentException("cust_no is required for update operations");
        }

        var normalized = await NormalizeCustomerInputAsync(record, includeCreateDate: false, cancellationToken);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
UPDATE `customers`
SET birth_date = @birthDate,
    first_name = @firstName,
    last_name = @lastName,
    social_security_number = @ssn,
    credit_card_number = @ccn,
    address = @address,
    salary = @salary
WHERE cust_no = @custNo;
""";

        await using var command = new MySqlCommand(sql, connection);
        BindCommonParameters(command, normalized);
        command.Parameters.AddWithValue("@custNo", record.CustNo.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return await GetCustomerRecordsAsync(cancellationToken: cancellationToken);
    }

    private async Task ConnectAndPrepareAsync(string username, string password, CancellationToken cancellationToken)
    {
        var serverBuilder = new MySqlConnectionStringBuilder
        {
            Server = _config.Database.Address,
            Port = (uint)_config.Database.Port,
            UserID = username,
            Password = password,
            AllowUserVariables = true
        };

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                await using var connection = new MySqlConnection(serverBuilder.ConnectionString);
                await connection.OpenAsync(cancellationToken);

                await using (var createDb = new MySqlCommand($"CREATE DATABASE IF NOT EXISTS `{_config.Database.Database}`", connection))
                {
                    await createDb.ExecuteNonQueryAsync(cancellationToken);
                }

                connection.ChangeDatabase(_config.Database.Database);

                await using (var createTable = new MySqlCommand(CustomerTable, connection))
                {
                    await createTable.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var seed = new MySqlCommand(SeedCustomers, connection))
                {
                    await seed.ExecuteNonQueryAsync(cancellationToken);
                }

                _connectionString = new MySqlConnectionStringBuilder(serverBuilder.ConnectionString)
                {
                    Database = _config.Database.Database
                }.ConnectionString;

                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Attempt {Attempt}/10 failed while connecting to MySQL at {Address}:{Port}", attempt, _config.Database.Address, _config.Database.Port);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }

        throw new ConnectionException($"Could not connect to {_config.Database.Address}:{_config.Database.Port} with user {username}");
    }

    private async Task<CustomerRecord> ProcessCustomerAsync(MySqlDataReader row, bool raw, CancellationToken cancellationToken)
    {
        var result = new CustomerRecord
        {
            CustomerNumber = row.GetInt32(0),
            BirthDate = row.GetString(1),
            FirstName = row.GetString(2),
            LastName = row.GetString(3),
            CreateDate = row.GetString(4),
            Ssn = row.GetString(5),
            Ccn = row.GetString(6),
            Address = row.GetString(7),
            Salary = row.GetString(8)
        };

        if (_vaultService.IsEnabled && !raw)
        {
            result.BirthDate = await _vaultService.DecryptAsync(result.BirthDate, cancellationToken);
            result.Address = await _vaultService.DecryptAsync(result.Address, cancellationToken);
            result.Salary = await _vaultService.DecryptAsync(result.Salary, cancellationToken);

            if (_vaultService.IsTransformEnabled)
            {
                result.Ssn = await _vaultService.DecodeSsnAsync(result.Ssn, cancellationToken);
            }
            else
            {
                result.Ssn = await _vaultService.DecryptAsync(result.Ssn, cancellationToken);
                result.Ccn = await _vaultService.DecryptAsync(result.Ccn, cancellationToken);
            }
        }

        return result;
    }

    private static void BindCommonParameters(MySqlCommand command, CustomerInput input)
    {
        command.Parameters.AddWithValue("@birthDate", input.BirthDate);
        command.Parameters.AddWithValue("@firstName", input.FirstName);
        command.Parameters.AddWithValue("@lastName", input.LastName);
        command.Parameters.AddWithValue("@createDate", input.CreateDate);
        command.Parameters.AddWithValue("@ssn", input.Ssn);
        command.Parameters.AddWithValue("@ccn", input.Ccn);
        command.Parameters.AddWithValue("@address", input.Address);
        command.Parameters.AddWithValue("@salary", input.Salary);
    }

    private async Task<CustomerInput> NormalizeCustomerInputAsync(CustomerInput input, bool includeCreateDate, CancellationToken cancellationToken)
    {
        var createDate = input.CreateDate;
        if (includeCreateDate && string.IsNullOrWhiteSpace(createDate))
        {
            createDate = DateTime.UtcNow.ToString("O");
        }

        var normalized = new CustomerInput
        {
            CustNo = input.CustNo,
            BirthDate = input.BirthDate,
            FirstName = input.FirstName,
            LastName = input.LastName,
            CreateDate = createDate,
            Ssn = input.Ssn,
            Ccn = input.Ccn,
            Address = input.Address,
            Salary = input.Salary
        };

        if (!_vaultService.IsEnabled)
        {
            return normalized;
        }

        normalized.BirthDate = await _vaultService.EncryptAsync(normalized.BirthDate, cancellationToken);
        normalized.Address = await _vaultService.EncryptAsync(normalized.Address, cancellationToken);
        normalized.Salary = await _vaultService.EncryptAsync(normalized.Salary, cancellationToken);

        if (_vaultService.IsTransformEnabled)
        {
            normalized.Ssn = await _vaultService.EncodeSsnAsync(normalized.Ssn, cancellationToken);
            normalized.Ccn = await _vaultService.EncodeCcnAsync(normalized.Ccn, cancellationToken);
        }
        else
        {
            normalized.Ssn = await _vaultService.EncryptAsync(normalized.Ssn, cancellationToken);
            normalized.Ccn = await _vaultService.EncryptAsync(normalized.Ccn, cancellationToken);
        }

        return normalized;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!IsInitialized)
        {
            await InitializeAsync(cancellationToken);
        }
    }
}

public sealed class ConnectionException : Exception
{
    public ConnectionException(string message) : base(message)
    {
    }
}
