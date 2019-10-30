﻿using System.Threading.Tasks;

namespace NetRpc
{
    internal class ClientMethodInvokeMiddleware
    {
        public ClientMethodInvokeMiddleware(ClientRequestDelegate next)
        {
        }

        public async Task InvokeAsync(ClientActionExecutingContext context)
        { 
            context.Result = await context.OnceCall.CallAsync(context.Header, new MethodContext(context.ContractMethod, context.InstanceMethod), context.Callback, context.Token, context.Stream, context.PureArgs);
        }
    }
}