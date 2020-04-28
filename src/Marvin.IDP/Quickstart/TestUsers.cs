// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityModel;
using IdentityServer4.Test;
using System.Collections.Generic;
using System.Security.Claims;

namespace Marvin.IDP
{
    public class TestUsers
    {
        public static List<TestUser> Users = new List<TestUser>
        {
            new TestUser
            {
                SubjectId = "f368a2f6-3a56-441f-8b06-25272acc5ce7",
                Username = "Frank",
                Password = "password",
                Claims =
                {
                    new Claim("given_name", "Frank"),
                    new Claim("family_name", "Underwood"),
                    new Claim("address", "Main Street 1"),
                    new Claim("role", "FreeUser"),
                    new Claim("subscription_level", "FreeUser"),
                    new Claim("country", "be")
                }
            },
            new TestUser
            {
                SubjectId = "9cca3868-4c09-4104-bab5-14b06f9e61bd",
                Username = "Claire",
                Password = "password",
                Claims =
                {
                    new Claim("given_name", "Claire"),
                    new Claim("family_name", "Underwood"),
                    new Claim("address", "Big Road 13"),
                    new Claim("role", "PayingUser"),
                    new Claim("subscription_level", "PayingUser"),
                    new Claim("country", "be")
                }
            }
        };
    }
}