﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NetRpc
{
    public sealed class RequestHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ChannelType _channelType;
        private readonly ILogger _logger;
        private readonly MiddlewareBuilder _middlewareBuilder;

        public RequestHandler(IServiceProvider serviceProvider, ChannelType channelType)
        {
            _serviceProvider = serviceProvider;
            _channelType = channelType;
            _logger = _serviceProvider.GetService<ILoggerFactory>().CreateLogger("NetRpc");
            var middlewareOptions = _serviceProvider.GetService<IOptions<MiddlewareOptions>>().Value;
            _middlewareBuilder = new MiddlewareBuilder(middlewareOptions, serviceProvider);
        }

        public async Task HandleAsync(IServiceConnection connection)
        {
            await HandleAsync(new BufferServiceOnceApiConvert(connection, _logger));
        }

        public async Task HandleAsync(IServiceOnceApiConvert convert)
        {
            try
            {
                var contractOptions = _serviceProvider.GetRequiredService<IOptions<ContractOptions>>();
                var rpcContextAccessor = _serviceProvider.GetRequiredService<IActionExecutingContextAccessor>();

                using var scope = _serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
                GlobalServiceProvider.Provider = _serviceProvider;
                GlobalServiceProvider.ScopeProvider = scope.ServiceProvider;
                var instances = scope.ServiceProvider.GetContractInstances(contractOptions.Value);
                var onceTransfer = new ServiceOnceTransfer(instances, scope.ServiceProvider, convert, _middlewareBuilder, rpcContextAccessor, _channelType, _logger);
                await onceTransfer.StartAsync();
                await onceTransfer.HandleRequestAsync();
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, null);
                throw;
            }
        }
    }
}