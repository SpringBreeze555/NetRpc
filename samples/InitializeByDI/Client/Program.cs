﻿using System.Threading;
using System.Threading.Tasks;
using DataContract;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetRpc;
using NetRpc.Grpc;

namespace Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddHostedService<GrpcHostedService>();
                    services.AddNetRpcGrpcClient<IService>(i =>
                        i.Channel = new Channel("localhost", 50001, ChannelCredentials.Insecure));
                })
                .Build();

            await host.StartAsync();
        }
    }

    public class GrpcHostedService : IHostedService
    {
        private readonly ClientProxy<IService> _client;

        public GrpcHostedService(ClientProxy<IService> client) //DI client here.
        {
            _client = client;
        }

        public Task StartAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            _client.Proxy.Call("hello world.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return Task.CompletedTask;
        }
    }
}