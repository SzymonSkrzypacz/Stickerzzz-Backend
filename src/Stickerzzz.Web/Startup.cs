﻿using Stickerzzz.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using System;
using System.Reflection;
using AutoMapper;
using FluentValidation.WebApi;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Stickerzzz.Infrastructure.Data;
using MediatR;
using Microsoft.Extensions.Logging;
using Stickerzzz.Infrastructure.Errors;
using Stickerzzz.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Linq;
using System.Net.Sockets;
using Newtonsoft.Json.Serialization;
using Stickerzzz.Web.Posts;

namespace Stickerzzz.Web
{
    public class Startup
    {
        static readonly string name = Dns.GetHostName(); // get container id
        static readonly IPAddress ip = Dns.GetHostEntry(name).AddressList.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
        //public string DEFAULT_DATABASE_CONNECTIONSTRING = "Host=db;Port=5432;Username=postgres;Password=NoFearNoMore12;Database=Stickerzzz;Command Timeout = 0";
        public const string DEFAULT_DATABASE_PROVIDER = "postgres";

        private readonly IConfiguration _config;

        public Startup(IConfiguration config)
        {
            _config = config;
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            // take the connection string from the environment variable or use hard-coded database name
            var connectionString = "Host=127.0.0.1;Port=5432;Username=postgres;Password=Paramiko12;Database=stickerzzz;sslmode=Prefer;Trust Server Certificate=true;";
            const string herokuConnectionString = @"
				Host=ec2-107-21-214-26.compute-1.amazonaws.com;
				Port=5432;
				Username=otsmskonphslqn;
				Password=2f0529e947bd0db1a2194de1f46a9c3bfc14918d3500c60d2a1dd64091b5ec2f;
				Database=dbd8p2d5gcap7;
				Pooling=true;
				SSL Mode=Require;
				TrustServerCertificate=True;
				";
            var connectionStringLocal = "Host=127.0.0.1;Port=5432;Username=postgres;Password=NoFearNoMore12;Database=stickerzzz;Command Timeout = 0;SSL Mode = Prefer;";
            // take the database provider from the environment variable or use hard-coded database provider
            var databaseProvider = _config.GetValue<string>("ASPNETCORE_Stickerzzz_DatabaseProvider");
            if (string.IsNullOrWhiteSpace(databaseProvider))
                databaseProvider = DEFAULT_DATABASE_PROVIDER;

            services.AddDbContext<AppDbContext>(options =>
            {
                if (databaseProvider.ToLower().Trim().Equals("sqlite"))
                    options.UseSqlite(connectionString);
                else if (databaseProvider.ToLower().Trim().Equals("postgres"))
                {
                    options.UseNpgsql(herokuConnectionString); // Local - connectionStringLocal // Production - connectionString
                }
                else
                    throw new Exception("Database provider unknown. Please check configuration");
            });

            var mappingConfig = new MapperConfiguration(mc =>
                        {
                            mc.AddProfile(new MappingPost());
                        });

            IMapper mapper = mappingConfig.CreateMapper();
            services.AddSingleton(mapper);


            services.AddCors();
            services.AddControllersWithViews().AddNewtonsoftJson().AddFluentValidation(fv =>
            {
                fv.ImplicitlyValidateChildProperties = true;
            });
            services.AddRazorPages();

            services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "Stickerzzz API", Version = "v1" }));
            services.ConfigureSwaggerGen(options =>
               {
                   options.CustomSchemaIds(x => x.FullName);
               });

            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_3_0)
                .AddNewtonsoftJson(opt =>
                {
                    opt.SerializerSettings.ContractResolver = new DefaultContractResolver();
                    opt.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                });

            services.AddAutoMapper(GetType().Assembly);
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
            services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddMediatR(Assembly.GetExecutingAssembly());
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(DBContextTransactionPipelineBehavior<,>));
            services.AddJwt();
            return ContainerSetup.InitializeWeb(Assembly.GetExecutingAssembly(), services);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory)
        {

            loggerFactory.AddSerilogLogging();

            if (env.EnvironmentName == "Development")
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseMiddleware<ErrorHandlingMiddleware>();

            app.UseCors(builder =>
                builder
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod());

            app.UseRouting();
            app.UseAuthorization();
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Stickerzzz API V1");
                c.RoutePrefix = string.Empty;
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
                endpoints.MapRazorPages();
            });
        }
    }
}
