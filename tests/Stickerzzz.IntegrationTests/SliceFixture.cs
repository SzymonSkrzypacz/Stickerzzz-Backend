﻿using System;
using System.IO;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Stickerzzz.Web;
using Stickerzzz.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Stickerzzz.IntegrationTests
{
    public class SliceFixture : IDisposable
    {
        static readonly IConfiguration Config;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ServiceProvider _provider;
        private readonly string DbName = Guid.NewGuid() + ".db";

        static SliceFixture()
        {
            Config = new ConfigurationBuilder()
               .AddEnvironmentVariables()
               .Build();
        }

        public SliceFixture()
        {
            var startup = new Startup(Config);
            var services = new ServiceCollection();

            DbContextOptionsBuilder builder = new DbContextOptionsBuilder()
                .UseInMemoryDatabase(DbName)
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            services.AddSingleton(new AppDbContext(builder.Options));

            startup.ConfigureServices(services);

            _provider = services.BuildServiceProvider();
            GetDbContext().Database.EnsureDeleted();

            GetDbContext().Database.EnsureCreated();
            _scopeFactory = _provider.GetService<IServiceScopeFactory>();
        }

        public AppDbContext GetDbContext()
        {
            return _provider.GetRequiredService<AppDbContext>();
        }

        public void Dispose()
        {
            File.Delete(DbName);
        }

        public async Task ExecuteScopeAsync(Func<IServiceProvider, Task> action)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                await action(scope.ServiceProvider);
            }
        }

        public async Task<T> ExecuteScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                return await action(scope.ServiceProvider);
            }
        }

        public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
        {
            return ExecuteScopeAsync(sp =>
            {
                var mediator = sp.GetService<IMediator>();

                return mediator.Send(request);
            });
        }

        public Task SendAsync(IRequest request)
        {
            return ExecuteScopeAsync(sp =>
            {
                var mediator = sp.GetService<IMediator>();

                return mediator.Send(request);
            });
        }

        public Task ExecuteDbContextAsync(Func<AppDbContext, Task> action)
        {
            return ExecuteScopeAsync(sp => action(sp.GetService<AppDbContext>()));
        }

        public Task<T> ExecuteDbContextAsync<T>(Func<AppDbContext, Task<T>> action)
        {
            return ExecuteScopeAsync(sp => action(sp.GetService<AppDbContext>()));
        }

        public Task InsertAsync(params object[] entities)
        {
            return ExecuteDbContextAsync(db =>
            {
                foreach (var entity in entities)
                {
                    db.Add(entity);
                }
                return db.SaveChangesAsync();
            });
        }
    }
}