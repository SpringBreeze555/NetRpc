﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NetRpc
{
    internal sealed class ServiceOnceTransfer
    {
        private readonly IServiceOnceApiConvert _convert;
        private readonly List<Instance> _instances;
        private readonly IServiceProvider _serviceProvider;
        private readonly MiddlewareBuilder _middlewareBuilder;
        private readonly IGlobalServiceContextAccessor _globalServiceContextAccessor;
        private readonly CancellationTokenSource _serviceCts = new CancellationTokenSource();

        public ServiceOnceTransfer(List<Instance> instances, IServiceProvider serviceProvider, IServiceOnceApiConvert convert,
            MiddlewareBuilder middlewareBuilder, IGlobalServiceContextAccessor globalServiceContextAccessor)
        {
            _instances = instances;
            _serviceProvider = serviceProvider;
            _middlewareBuilder = middlewareBuilder;
            _globalServiceContextAccessor = globalServiceContextAccessor;
            _convert = convert;
        }

        public async Task StartAsync()
        {
            await _convert.StartAsync(_serviceCts);
        }

        public async Task HandleRequestAsync()
        {
            object ret;
            ServiceContext context = null;
            ServiceCallParam scp = null;

            try
            {
                scp = await GetServiceCallParamAsync();
                context = ApiWrapper.Convert(scp, _instances, _serviceProvider);

                //set Accessor
                _globalServiceContextAccessor.Context = context;

                ret = await _middlewareBuilder.InvokeAsync(context);

                //if Post, do not need send back to client.
                if (scp.Action.IsPost)
                    return;
            }
            catch (Exception e)
            {
                //if Post, do not need send back to client.
                if (scp != null && scp.Action.IsPost)
                    return;

                //send fault
                await _convert.SendFaultAsync(e, context);
                return;
            }

            var hasStream = ret.TryGetStream(out var retStream, out var retStreamName);

            //send result
            var sendStreamNext = await _convert.SendResultAsync(new CustomResult(ret, hasStream, retStream.GetLength()), retStream, retStreamName, context);
            if (!sendStreamNext)
                return;

            //send stream
            await SendStreamAsync(hasStream, retStream, scp);
        }

        private async Task SendStreamAsync(bool hasStream, Stream retStream, ServiceCallParam scp)
        {
            if (hasStream)
            {
                try
                {
                    using (retStream)
                    {
                        await Helper.SendStreamAsync(i => _convert.SendBufferAsync(i), () =>
                            _convert.SendBufferEndAsync(), retStream, scp.Token);
                    }
                }
                catch (TaskCanceledException)
                {
                    await _convert.SendBufferCancelAsync();
                }
                catch (Exception)
                {
                    await _convert.SendBufferFaultAsync();
                }
            }
        }

        private async Task<ServiceCallParam> GetServiceCallParamAsync()
        {
            //onceCallParam
            var onceCallParam = await _convert.GetOnceCallParamAsync();

            //stream
            Stream stream;
            if (onceCallParam.Action.IsPost)
                stream = BytesToStream(onceCallParam.PostStream);
            else
                stream = _convert.GetRequestStream(onceCallParam.StreamLength);

            //serviceCallParam
            return new ServiceCallParam(onceCallParam,
                async i => await _convert.SendCallbackAsync(i),
                _serviceCts.Token, stream);
        }

        private static Stream BytesToStream(byte[] bytes)
        {
            if (bytes == null)
                return null;
            Stream stream = new MemoryStream(bytes);
            return stream;
        }
    }
}