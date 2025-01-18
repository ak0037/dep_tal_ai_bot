using Microsoft.Graph.Communications.Client.Authentication;
using Microsoft.Graph.Communications.Common;
using Microsoft.Graph.Communications.Common.Telemetry;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PsiBot.Model.Constants;
using System;
using System.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace PsiBot.Services.Authentication
{
    public class AuthenticationProvider : ObjectRoot, IRequestAuthenticationProvider
    {
        private readonly string appName;
        private readonly string appId;
        private readonly string appSecret;
        private readonly TimeSpan openIdConfigRefreshInterval = TimeSpan.FromHours(2);
        private DateTime prevOpenIdConfigUpdateTimestamp = DateTime.MinValue;
        private OpenIdConnectConfiguration openIdConfiguration;

        public AuthenticationProvider(string appName, string appId, string appSecret, IGraphLogger logger)
            : base(logger.NotNull(nameof(logger)).CreateShim(nameof(AuthenticationProvider)))
        {
            this.appName = appName.NotNullOrWhitespace(nameof(appName));
            this.appId = appId.NotNullOrWhitespace(nameof(appId));
            this.appSecret = appSecret.NotNullOrWhitespace(nameof(appSecret));

            Console.WriteLine($"[AUTH] Provider initialized with appName: {appName}, appId: {appId}");
        }

        public async Task AuthenticateOutboundRequestAsync(HttpRequestMessage request, string tenant)
        {
            Console.WriteLine($"[AUTH] Starting outbound authentication for tenant: {tenant}");

            const string schema = "Bearer";
            const string replaceString = "{tenant}";
            const string oauthV2TokenLink = "https://login.microsoftonline.com/{tenant}";
            string[] scopes = new[] { "https://graph.microsoft.com/.default" };

            tenant = string.IsNullOrWhiteSpace(tenant) ? "common" : tenant;
            var tokenLink = oauthV2TokenLink.Replace(replaceString, tenant);

            Console.WriteLine($"[AUTH] Attempting to acquire token from: {tokenLink}");

            var app = ConfidentialClientApplicationBuilder
                .Create(this.appId)
                .WithClientSecret(this.appSecret)
                .WithAuthority(new Uri(tokenLink))
                .Build();

            try
            {
                var result = await app.AcquireTokenForClient(scopes)
                    .ExecuteAsync();

                var expiresIn = result.ExpiresOn.Subtract(DateTimeOffset.UtcNow).TotalMinutes;
                Console.WriteLine($"[AUTH] Successfully acquired token. Expires in {expiresIn:F1} minutes");
                Console.WriteLine($"[AUTH] Token type: {result.TokenType}");
                Console.WriteLine($"[AUTH] Scopes acquired: {string.Join(", ", result.Scopes)}");

                request.Headers.Authorization = new AuthenticationHeaderValue(schema, result.AccessToken);
            }
            catch (MsalServiceException ex)
            {
                Console.WriteLine($"[AUTH ERROR] Failed to generate token. Error: {ex.Message}");
                Console.WriteLine($"[AUTH ERROR] Error Code: {ex.ErrorCode}");
                Console.WriteLine($"[AUTH ERROR] Correlation ID: {ex.CorrelationId}");
                throw;
            }
        }

        public async Task<RequestValidationResult> ValidateInboundRequestAsync(HttpRequestMessage request)
        {
            Console.WriteLine("[AUTH] Starting inbound request validation");

            var token = request?.Headers?.Authorization?.Parameter;
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("[AUTH ERROR] No authorization token found in request");
                return new RequestValidationResult { IsValid = false };
            }

            try
            {
                if (this.openIdConfiguration == null || DateTime.Now > this.prevOpenIdConfigUpdateTimestamp.Add(this.openIdConfigRefreshInterval))
                {
                    Console.WriteLine("[AUTH] Updating OpenID configuration");

                    IConfigurationManager<OpenIdConnectConfiguration> configurationManager =
                        new ConfigurationManager<OpenIdConnectConfiguration>(
                            AzureConstants.AuthDomain,
                            new OpenIdConnectConfigurationRetriever());

                    this.openIdConfiguration = await configurationManager.GetConfigurationAsync(CancellationToken.None).ConfigureAwait(false);
                    this.prevOpenIdConfigUpdateTimestamp = DateTime.Now;

                    Console.WriteLine("[AUTH] Successfully updated OpenID configuration");
                    Console.WriteLine($"[AUTH] Number of signing keys: {this.openIdConfiguration.SigningKeys.Count}");
                }

                var authIssuers = new[]
                {
                    "https://graph.microsoft.com",
                    "https://api.botframework.com",
                };

                var validationParameters = new TokenValidationParameters
                {
                    ValidIssuers = authIssuers,
                    ValidAudience = this.appId,
                    IssuerSigningKeys = this.openIdConfiguration.SigningKeys,
                };

                Console.WriteLine("[AUTH] Validating JWT token");
                JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                var claimsPrincipal = handler.ValidateToken(token, validationParameters, out var validatedToken);

                Console.WriteLine($"[AUTH] Token validated successfully. Token type: {validatedToken.GetType().Name}");

                const string ClaimType = "http://schemas.microsoft.com/identity/claims/tenantid";
                var tenantClaim = claimsPrincipal.FindFirst(claim => claim.Type.Equals(ClaimType, StringComparison.Ordinal));

                if (string.IsNullOrEmpty(tenantClaim?.Value))
                {
                    Console.WriteLine("[AUTH ERROR] No tenant claim found in token");
                    return new RequestValidationResult { IsValid = false };
                }

                Console.WriteLine($"[AUTH] Request validated successfully for tenant: {tenantClaim.Value}");
                request.Properties.Add(HttpConstants.HeaderNames.Tenant, tenantClaim.Value);
                return new RequestValidationResult { IsValid = true, TenantId = tenantClaim.Value };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUTH ERROR] Token validation failed: {ex.GetType().Name}");
                Console.WriteLine($"[AUTH ERROR] Error message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[AUTH ERROR] Inner error: {ex.InnerException.Message}");
                }
                return new RequestValidationResult() { IsValid = false };
            }
        }

        public async Task<AuthenticationResult> AcquireTokenWithRetryAsync(IConfidentialClientApplication app, string[] scopes, int attempts)
        {
            Console.WriteLine($"[AUTH] Starting token acquisition with {attempts} retry attempts");

            while (true)
            {
                attempts--;
                Console.WriteLine($"[AUTH] Attempting to acquire token. Attempts remaining: {attempts}");

                try
                {
                    var result = await app.AcquireTokenForClient(scopes)
                        .ExecuteAsync()
                        .ConfigureAwait(false);

                    Console.WriteLine("[AUTH] Successfully acquired token with retry");
                    return result;
                }
                catch (MsalServiceException ex) when (ex.ErrorCode == "temporarily_unavailable")
                {
                    if (attempts < 1)
                    {
                        Console.WriteLine("[AUTH ERROR] Exhausted all retry attempts");
                        throw new ApplicationException("Failed to acquire token after multiple retries.", ex);
                    }
                    Console.WriteLine($"[AUTH] Token acquisition temporarily failed. Will retry. Error: {ex.Message}");
                }
                catch (MsalException ex)
                {
                    Console.WriteLine($"[AUTH ERROR] MSAL exception during token acquisition: {ex.Message}");
                    throw new ApplicationException($"Failed to acquire token: {ex.Message}", ex);
                }

                Console.WriteLine("[AUTH] Waiting 1 second before retry...");
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }
    }
}