using System.Net.Http.Headers;
using System.Linq;
using System.Text;
using System.Text.Json;
using NomadVaultMySqlDotnet.Configuration;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.SecretsEngines.Transit;

namespace NomadVaultMySqlDotnet.Services;

public sealed class VaultService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VaultService> _logger;
    private IVaultClient? _vaultClient;

    private VaultSettings _settings = new();
    private string _token = string.Empty;

    public bool IsEnabled { get; private set; }
    public bool IsTransformEnabled => IsEnabled && _settings.Transform;

    public VaultService(IHttpClientFactory httpClientFactory, ILogger<VaultService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(AppRuntimeConfig config, CancellationToken cancellationToken)
    {
        _settings = config.Vault;
        if (!_settings.Enabled)
        {
            IsEnabled = false;
            return;
        }

        _token = _settings.InjectToken
            ? (Environment.GetEnvironmentVariable("VAULT_TOKEN") ?? string.Empty).Trim()
            : _settings.Token.Trim();

        if (string.IsNullOrWhiteSpace(_settings.Address) || string.IsNullOrWhiteSpace(_token))
        {
            _logger.LogWarning("Skipping Vault initialization because address or token is missing.");
            IsEnabled = false;
            return;
        }

        var authMethod = new TokenAuthMethodInfo(_token);
        var vaultClientSettings = new VaultClientSettings(_settings.Address, authMethod)
        {
            Namespace = string.IsNullOrWhiteSpace(_settings.Namespace) ? null : _settings.Namespace,
            MyHttpClientProviderFunc = handler => new HttpClient(new HttpClientHandler
            {
                // PoC only: trust self-signed/internal certs for demo environments.
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            })
        };
        _vaultClient = new VaultClient(vaultClientSettings);

        var healthResponse = await SendVaultRequestAsync(HttpMethod.Get, "/v1/sys/health", null, cancellationToken);
        if (!healthResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Vault health endpoint returned status {StatusCode}; continuing with local DB creds.", (int)healthResponse.StatusCode);
            IsEnabled = false;
            return;
        }

        IsEnabled = true;
    }

    public async Task<(string User, string Password)?> ReadDatabaseCredentialsAsync(string path, CancellationToken cancellationToken)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(path) || _vaultClient is null)
        {
            return null;
        }

        // Vault dynamic DB creds path format: <mount>/creds/<role>
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var credsIndex = Array.IndexOf(parts, "creds");
        if (credsIndex > 0 && credsIndex < parts.Length - 1)
        {
            try
            {
                var mountPoint = string.Join('/', parts.Take(credsIndex));
                var roleName = parts[credsIndex + 1];
                var secret = await _vaultClient.V1.Secrets.Database.GetCredentialsAsync(roleName, mountPoint);
                return (secret.Data.Username ?? string.Empty, secret.Data.Password ?? string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read db creds via VaultSharp from {Path}; falling back to generic API.", path);
            }
        }

        var response = await SendVaultRequestAsync(HttpMethod.Get, $"/v1/{path}", null, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to read db creds from Vault path {Path}: {StatusCode}", path, (int)response.StatusCode);
            return null;
        }

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!doc.RootElement.TryGetProperty("data", out var data))
        {
            return null;
        }

        if (data.TryGetProperty("username", out var directUser) && data.TryGetProperty("password", out var directPassword))
        {
            return (directUser.GetString() ?? string.Empty, directPassword.GetString() ?? string.Empty);
        }

        if (data.TryGetProperty("data", out var nestedData) &&
            nestedData.TryGetProperty("username", out var nestedUser) &&
            nestedData.TryGetProperty("password", out var nestedPassword))
        {
            return (nestedUser.GetString() ?? string.Empty, nestedPassword.GetString() ?? string.Empty);
        }

        return null;
    }

    public async Task<string> EncryptAsync(string value, CancellationToken cancellationToken)
    {
        if (!IsEnabled || _vaultClient is null)
        {
            return value;
        }

        var plaintext = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        var request = new EncryptRequestOptions { Base64EncodedPlainText = plaintext };
        var response = await _vaultClient.V1.Secrets.Transit.EncryptAsync(_settings.KeyName, request, mountPoint: _settings.KeyPath);
        return response.Data.CipherText ?? value;
    }

    public async Task<string> DecryptAsync(string value, CancellationToken cancellationToken)
    {
        if (!IsEnabled || _vaultClient is null || !value.StartsWith("vault:v", StringComparison.Ordinal))
        {
            return value;
        }

        var request = new DecryptRequestOptions { CipherText = value };
        var response = await _vaultClient.V1.Secrets.Transit.DecryptAsync(_settings.KeyName, request, mountPoint: _settings.KeyPath);
        var base64PlainText = response.Data.Base64EncodedPlainText;
        if (string.IsNullOrWhiteSpace(base64PlainText))
        {
            return value;
        }

        var decoded = Convert.FromBase64String(base64PlainText);
        return Encoding.UTF8.GetString(decoded);
    }

    public async Task<string> EncodeSsnAsync(string value, CancellationToken cancellationToken)
    {
        if (!IsTransformEnabled)
        {
            return value;
        }

        return await EncodeValueAsync(_settings.TransformPath, _settings.SsnRole, value, cancellationToken);
    }

    public async Task<string> EncodeCcnAsync(string value, CancellationToken cancellationToken)
    {
        if (!IsTransformEnabled)
        {
            return value;
        }

        return await EncodeValueAsync(_settings.TransformMaskingPath, _settings.CcnRole, value, cancellationToken);
    }

    public async Task<string> DecodeSsnAsync(string value, CancellationToken cancellationToken)
    {
        if (!IsTransformEnabled)
        {
            return value;
        }

        var payload = JsonSerializer.Serialize(new { value, transformation = _settings.SsnRole });
        var response = await SendVaultRequestAsync(
            HttpMethod.Post,
            $"/v1/{_settings.TransformPath}/decode/{_settings.SsnRole}",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return doc.RootElement.GetProperty("data").GetProperty("decoded_value").GetString() ?? value;
    }

    private async Task<string> EncodeValueAsync(string mountPath, string role, string value, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { value, transformation = role });
        var response = await SendVaultRequestAsync(
            HttpMethod.Post,
            $"/v1/{mountPath}/encode/{role}",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return doc.RootElement.GetProperty("data").GetProperty("encoded_value").GetString() ?? value;
    }

    private async Task<HttpResponseMessage> SendVaultRequestAsync(HttpMethod method, string path, string? payload, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("vault");
        client.BaseAddress = new Uri(_settings.Address);

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Vault-Token", _token);

        if (!string.IsNullOrWhiteSpace(_settings.Namespace))
        {
            request.Headers.Add("X-Vault-Namespace", _settings.Namespace);
        }

        if (!string.IsNullOrWhiteSpace(payload))
        {
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        }

        return await client.SendAsync(request, cancellationToken);
    }
}
