using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace ImageGallery.Client.HttpHandlers
{
    public class BearerTokenHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IHttpClientFactory httpClientFactory;

        public BearerTokenHandler(IHttpContextAccessor httpContextAccessor, IHttpClientFactory httpClientFactory)
        {
            this.httpContextAccessor = httpContextAccessor;
            this.httpClientFactory = httpClientFactory;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var accessToken = await httpContextAccessor.HttpContext.GetTokenAsync(OpenIdConnectParameterNames.AccessToken);
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.SetBearerToken(accessToken);
            }
            await GetAccessToken();
            return await base.SendAsync(request, cancellationToken);
        }

        private async Task<string> GetAccessToken()
        {
            // Is access token expired, as per `expires_at` claim?
            // If so, we have to extract the refresh token from HttpContext and
            // create a refresh token request. 
            // Once we have the new access token, we store all the tokens (id_token, access_token, refresh_token)
            // and sign in again, thus persisting all the tokens in the authentication cookie.
            var accessToken = await httpContextAccessor
                .HttpContext
                .GetTokenAsync(OpenIdConnectParameterNames.AccessToken);
            var expiresAtRaw = await httpContextAccessor
                .HttpContext
                .GetTokenAsync("expires_at");
            var expiresAt = DateTime.Parse(expiresAtRaw).ToUniversalTime();
            var timeBeforeExpiration = 600;     // If access token is nearing expiration, we will want to refresh it anyway. Expressed in seconds.
            // var timeBeforeExpiration = 30;     // Just for testing purposes.
            // Access token is not expired nor is nearing expiration.
            if ((expiresAt - DateTime.UtcNow).Seconds > timeBeforeExpiration)
            {
                return accessToken;
            }
            // Let's get the refresh token and talk to the token endpoint.
            var refreshToken = await httpContextAccessor
                .HttpContext
                .GetTokenAsync(OpenIdConnectParameterNames.RefreshToken);
            var idpClient = httpClientFactory.CreateClient("IDPClient");
            var discoveryDocumentResponse = await idpClient.GetDiscoveryDocumentAsync();
            var tokenResponse = await idpClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                RefreshToken = refreshToken,
                ClientId = "imagegalleryclient",
                ClientSecret = "secret",
                Address = discoveryDocumentResponse.TokenEndpoint
            });
            // Let's persist all the tokens: id_token, access_token and refresh_token.
            var authentication = await httpContextAccessor.HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            authentication.Properties.StoreTokens(
                new List<AuthenticationToken> {
                    new AuthenticationToken
                    {
                        Name = OpenIdConnectParameterNames.IdToken,
                        Value = tokenResponse.IdentityToken
                    },
                    new AuthenticationToken
                    {
                        Name = OpenIdConnectParameterNames.AccessToken,
                        Value = tokenResponse.AccessToken
                    },
                    new AuthenticationToken
                    {
                        Name = OpenIdConnectParameterNames.RefreshToken,
                        Value = tokenResponse.RefreshToken
                    },
                    new AuthenticationToken
                    {
                        Name = "expires_at",
                        Value = (DateTime.UtcNow + TimeSpan.FromSeconds(tokenResponse.ExpiresIn))
                            .ToString("o", CultureInfo.InvariantCulture)
                    }});
            await httpContextAccessor.HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                httpContextAccessor.HttpContext.User,
                authentication.Properties);
            return accessToken;
        }
    }
}