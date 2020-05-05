// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityServer4;
using IdentityServer4.Models;
using System.Collections.Generic;

namespace Marvin.IDP
{
    public static class Config
    {
        public static IEnumerable<IdentityResource> Ids =>
            new IdentityResource[]                   // Supported scopes.
            {
                new IdentityResources.OpenId(),      // Mandatory since we are using OIDC.
                new IdentityResources.Profile(),
                new IdentityResources.Address(),
                new IdentityResource(
                    "roles",
                    "Roles",
                    new string[]{ "role" }),
                new IdentityResource(
                    "subscription_level",
                    "Subscription level",
                    new string[]{ "subscription_level" }),
                new IdentityResource(
                    "country",
                    "Country",
                    new string[]{ "country" }),

            };

        public static IEnumerable<ApiResource> Apis =>
            new ApiResource[]
            {
                new ApiResource(
                    "imagegalleryapi",
                    "Image Gallery API",
                    new List<string>{               // IDP will include `role` claim with the access token when `imagegalleryapi` scope is requested.
                        "role"
                    })
            };

        public static IEnumerable<Client> Clients =>
            new Client[]
            {
                new Client
                {
                    ClientName = "ImageGallery",
                    ClientId = "imagegalleryclient",
                    AllowedGrantTypes = GrantTypes.Code,
                    RequirePkce = true,
                    RedirectUris = { "https://localhost:44389/signin-oidc" },   // Default value used by OIDC middleware on client.
                    PostLogoutRedirectUris = { "https://localhost:44389/signout-callback-oidc" },
                    AllowOfflineAccess = true,                                  // Enable refresh tokens
                    AccessTokenLifetime = 120,                                  // Just for testing purposes.
                    AbsoluteRefreshTokenLifetime = 600,                         // Just for testing purposes.
                    AllowedScopes =
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        IdentityServerConstants.StandardScopes.Address,
                        "roles",
                        "imagegalleryapi",
                        "subscription_level",
                        "country"
                    },
                    ClientSecrets = { new Secret ("secret".Sha256()) },
                    // AlwaysIncludeUserClaimsInIdToken = true                    // Enable if you want to skip the client app calling `/userinfo`.
                }
            };

    }
}