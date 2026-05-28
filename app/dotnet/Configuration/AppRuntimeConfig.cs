using Microsoft.Extensions.Logging;

namespace NomadVaultMySqlDotnet.Configuration;

public sealed class AppRuntimeConfig
{
    public DefaultSettings Default { get; init; } = new();
    public DatabaseSettings Database { get; init; } = new();
    public VaultSettings Vault { get; init; } = new();

    public static AppRuntimeConfig FromConfiguration(IConfiguration configuration)
    {
        var defaultSection = configuration.GetSection("DEFAULT");
        var databaseSection = configuration.GetSection("DATABASE");
        var vaultSection = configuration.GetSection("VAULT");

        return new AppRuntimeConfig
        {
            Default = new DefaultSettings
            {
                Port = ParseInt(defaultSection["Port"], 8080),
                LogLevel = ParseLogLevel(defaultSection["LogLevel"])
            },
            Database = new DatabaseSettings
            {
                Address = databaseSection["Address"] ?? "mysql",
                Port = ParseInt(databaseSection["Port"], 3306),
                Database = databaseSection["Database"] ?? "my_app",
                User = databaseSection["User"] ?? "root",
                Password = databaseSection["Password"] ?? "root"
            },
            Vault = new VaultSettings
            {
                Enabled = ParseBool(vaultSection["Enabled"], false),
                InjectToken = ParseBool(vaultSection["InjectToken"], false),
                Namespace = vaultSection["Namespace"] ?? string.Empty,
                Address = vaultSection["Address"] ?? string.Empty,
                Token = vaultSection["Token"] ?? string.Empty,
                KeyPath = vaultSection["KeyPath"] ?? string.Empty,
                KeyName = vaultSection["KeyName"] ?? string.Empty,
                Transform = ParseBool(vaultSection["Transform"], false),
                TransformPath = vaultSection["TransformPath"] ?? string.Empty,
                TransformMaskingPath = vaultSection["TransformMaskingPath"] ?? string.Empty,
                SsnRole = vaultSection["SSNRole"] ?? string.Empty,
                CcnRole = vaultSection["CCNRole"] ?? string.Empty,
                DatabaseAuthPath = vaultSection["database_auth"] ?? string.Empty
            }
        };
    }

    private static bool ParseBool(string? value, bool fallback)
    {
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static LogLevel ParseLogLevel(string? value)
    {
        return Enum.TryParse<LogLevel>(value, true, out var level) ? level : LogLevel.Information;
    }
}

public sealed class DefaultSettings
{
    public int Port { get; init; }
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
}

public sealed class DatabaseSettings
{
    public string Address { get; init; } = string.Empty;
    public int Port { get; init; }
    public string Database { get; init; } = string.Empty;
    public string User { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

public sealed class VaultSettings
{
    public bool Enabled { get; init; }
    public bool InjectToken { get; init; }
    public string Namespace { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public string KeyPath { get; init; } = string.Empty;
    public string KeyName { get; init; } = string.Empty;
    public bool Transform { get; init; }
    public string TransformPath { get; init; } = string.Empty;
    public string TransformMaskingPath { get; init; } = string.Empty;
    public string SsnRole { get; init; } = string.Empty;
    public string CcnRole { get; init; } = string.Empty;
    public string DatabaseAuthPath { get; init; } = string.Empty;
}
