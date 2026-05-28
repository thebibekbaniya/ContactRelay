using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using ContactRelay.Options;

namespace ContactRelay.Graph;

public sealed class GraphClientFactory(IOptions<GraphOptions> options, ILogger<GraphClientFactory> logger) : IGraphClientFactory
{
    private readonly GraphOptions _options = options.Value;

    public GraphServiceClient CreateClient()
    {
        ValidateOptions();
        var credential = CreateCredential();
        return new GraphServiceClient(credential, _options.Scopes);
    }

    private TokenCredential CreateCredential()
    {
        if (!string.IsNullOrWhiteSpace(_options.CertificateThumbprint))
        {
            var certificate = FindCertificate(_options.CertificateThumbprint);
            logger.LogInformation("Using certificate authentication for Microsoft Graph.");
            return new ClientCertificateCredential(_options.TenantId, _options.ClientId, certificate);
        }

        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            logger.LogInformation("Using client secret authentication for Microsoft Graph.");
            return new ClientSecretCredential(_options.TenantId, _options.ClientId, _options.ClientSecret);
        }

        throw new InvalidOperationException("Graph authentication requires Graph:CertificateThumbprint or Graph:ClientSecret.");
    }

    private X509Certificate2 FindCertificate(string thumbprint)
    {
        var normalized = thumbprint.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        var storeLocation = string.Equals(_options.CertificateStoreLocation, "CurrentUser", StringComparison.OrdinalIgnoreCase)
            ? StoreLocation.CurrentUser
            : StoreLocation.LocalMachine;

        using var store = new X509Store(StoreName.My, storeLocation);
        store.Open(OpenFlags.ReadOnly);

        var matches = store.Certificates.Find(X509FindType.FindByThumbprint, normalized, validOnly: false);
        var certificate = matches
            .OfType<X509Certificate2>()
            .FirstOrDefault(cert => cert.HasPrivateKey && DateTimeOffset.UtcNow < cert.NotAfter);

        return certificate ?? throw new InvalidOperationException(
            $"Configured certificate was not found in {storeLocation}\\My, lacks a private key, or is expired.");
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_options.TenantId) || IsPlaceholder(_options.TenantId))
        {
            throw new InvalidOperationException("Graph:TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.ClientId) || IsPlaceholder(_options.ClientId))
        {
            throw new InvalidOperationException("Graph:ClientId is required.");
        }

        if (IsPlaceholder(_options.ClientSecret))
        {
            throw new InvalidOperationException("Graph:ClientSecret contains a placeholder value.");
        }

        if (IsPlaceholder(_options.CertificateThumbprint))
        {
            throw new InvalidOperationException("Graph:CertificateThumbprint contains a placeholder value.");
        }
    }

    private static bool IsPlaceholder(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && ConfigurationPlaceholder.IsPlaceholder(value);
    }
}
