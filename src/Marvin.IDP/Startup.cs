// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Mappers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Marvin.IDP
{
    public class Startup
    {
        public IWebHostEnvironment Environment { get; }

        public Startup(IWebHostEnvironment environment)
        {
            Environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var connectionString = "Host=localhost;Database=MarvinIDPDbContext;Username=postgres;Password=rootpw";

            // uncomment, if you want to add an MVC-based UI
            services.AddControllersWithViews();

            var builder = services.AddIdentityServer()
                .AddInMemoryIdentityResources(Config.Ids)
                .AddInMemoryApiResources(Config.Apis)
                .AddInMemoryClients(Config.Clients)
                .AddTestUsers(TestUsers.Users);

            // not recommended for production - you need to store your key material somewhere secure
            // builder.AddDeveloperSigningCredential();
            builder.AddSigningCredential(LoadCertificateFromStore());

            var migrationsAssembly = typeof(Startup).Assembly.GetName().Name;
            builder.AddConfigurationStore(options =>
                options.ConfigureDbContext = builder =>
                    builder.UseNpgsql(
                        connectionString,
                        options => options.MigrationsAssembly(migrationsAssembly)
                    )
            );
            builder.AddOperationalStore(options =>
                options.ConfigureDbContext = builder =>
                    builder.UseNpgsql(
                        connectionString,
                        options => options.MigrationsAssembly(migrationsAssembly)
                    )
            );
        }

        public void Configure(IApplicationBuilder app)
        {
            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // uncomment if you want to add MVC
            app.UseStaticFiles();
            app.UseRouting();

            app.UseIdentityServer();

            // uncomment, if you want to add MVC
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });
            SeedTestConfigurationData(app);
        }

        private X509Certificate2 LoadCertificateFromStore()
        {
            var thumbPrint = "74DDD656FB258CAA28817B70BAA5D7DE3A818A06";
            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly);
                var certCollection = store.Certificates.Find(X509FindType.FindByThumbprint, thumbPrint, true);
                if (certCollection.Count == 0)
                {
                    throw new Exception("The specified certificate not found.");
                }
                return certCollection[0];
            }
        }

        private static void SeedTestConfigurationData(IApplicationBuilder app)
        {
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var configurationDbContext =
                    scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
                configurationDbContext.Database.Migrate();
                var persistedGrantDbContext =
                    scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>();
                configurationDbContext.Database.Migrate();
                persistedGrantDbContext.Database.Migrate();
                if (!(configurationDbContext.Clients.Any()))
                {
                    configurationDbContext
                       .Clients
                       .AddRange(Config.Clients.Select(c => c.ToEntity()));
                }
                if (!(configurationDbContext.IdentityResources.Any()))
                {
                    configurationDbContext
                       .IdentityResources
                       .AddRange(Config.Ids.Select(i => i.ToEntity()));
                }
                if (!(configurationDbContext.ApiResources.Any()))
                {
                    configurationDbContext
                       .ApiResources
                       .AddRange(Config.Apis.Select(a => a.ToEntity()));
                }
                configurationDbContext.SaveChanges();
            }
        }
    }
}
