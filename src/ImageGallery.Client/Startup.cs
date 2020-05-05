using IdentityModel;
using ImageGallery.Client.HttpHandlers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using System;
using System.IdentityModel.Tokens.Jwt;

namespace ImageGallery.Client
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();         // Stop transforming claims from the token.
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews()
                 .AddJsonOptions(opts => opts.JsonSerializerOptions.PropertyNamingPolicy = null);

            services.AddHttpContextAccessor();
            services.AddTransient<BearerTokenHandler>();

            // create an HttpClient used for accessing the API
            services.AddHttpClient("APIClient", client =>
            {
                client.BaseAddress = new Uri("https://localhost:44366/");
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add(HeaderNames.Accept, "application/json");
            }).AddHttpMessageHandler<BearerTokenHandler>();                                     // Attach access token as Bearer token to all request send by this Http Client.

            // create an HttpClient used for accessing the IDP
            services.AddHttpClient("IDPClient", client =>
            {
                client.BaseAddress = new Uri("https://localhost:44318/");
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add(HeaderNames.Accept, "application/json");
            });

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddOpenIdConnect(
                OpenIdConnectDefaults.AuthenticationScheme,
                options =>
                {
                    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.Authority = "https://localhost:44318/";             // Our IDP. Middleware uses this to know where to find public keys and endpoints.
                    options.ClientId = "imagegalleryclient";
                    options.ClientSecret = "secret";
                    options.ResponseType = "code";
                    // options.UsePkce = false;                                 // Defaults to true.
                    //options.CallbackPath = new PathString("...");             // "/signin-oidc" default value set up by OpenIdConnect middleware. Uncomment to set some other URI.
                    //options.SignedOutCallbackPath = new PathString("...");    // "/signout-callback-oidc" default value set up by OpenIdConnect middleware. Uncomment to set some other URI.
                    // options.Scope.Add("openid");                             // Requested by OIDC middleware by default, so no need for it.
                    // options.Scope.Add("profile");                            // Requested by OIDC middleware by default, so no need for it.
                    options.Scope.Add("address");
                    options.Scope.Add("roles");
                    options.Scope.Add("subscription_level");
                    options.Scope.Add("country");
                    options.Scope.Add("imagegalleryapi");                       // Allow access to API via the access token.
                    options.Scope.Add("offline_access");                        // Ask for refresh token.
                    options.SaveTokens = true;                                  // Allows the middleware to save tokens received from OIDC provider to be used afterwards.
                    options.GetClaimsFromUserInfoEndpoint = true;               // Call `/userinfo` to fetch additional identity claims, such as `given_name`, ˙last_name`, `address` (if `address` scope was requested.)

                    // options.ClaimActions.Remove("nbf");                      // Stop filtering "nbf" claim so it is fed into claim collection. Note: this is only for demo purposes, we have no need for "nbf", therefore this line is not needed.
                    options.ClaimActions.DeleteClaim("sid");                    // Remove "sid" claim from claim collection.
                    options.ClaimActions.DeleteClaim("idp");
                    options.ClaimActions.DeleteClaim("auth_time");
                    options.ClaimActions.DeleteClaim("s_hash");
                    options.ClaimActions.MapUniqueJsonKey("role", "role");      // Explictly map a custom claim to feed it into the Claims Identity.
                    options.ClaimActions.MapUniqueJsonKey("subscription_level", "subscription_level");      // Explictly map a custom claim to feed it into the Claims Identity.
                    options.ClaimActions.MapUniqueJsonKey("country", "country");// Explictly map a custom claim to feed it into the Claims Identity.

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = JwtClaimTypes.Name,     // Allow for calling into ClaimsIdentity.IsUserInRole(...)
                        RoleClaimType = JwtClaimTypes.Role      // and [Authorize(Roles = ...)]
                    };
                });
            services.AddAuthorization(options =>
                {
                    options.AddPolicy(
                        "CanOrderFrame",
                        authorizationPolicy =>
                        {
                            authorizationPolicy.RequireAuthenticatedUser();
                            authorizationPolicy.RequireClaim("subscription_level", "PayingUser");
                            authorizationPolicy.RequireClaim("country", "be");
                        });
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseStaticFiles();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Shared/Error");
                // The default HSTS value is 30 days. You may want to change this for
                // production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Gallery}/{action=Index}/{id?}");
            });
        }
    }
}
