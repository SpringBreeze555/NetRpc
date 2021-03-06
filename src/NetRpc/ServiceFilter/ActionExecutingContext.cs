﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace NetRpc
{
    public class ActionExecutingContext : IActionExecutingContext
    {
        private object _result;

        public event EventHandler SendResultStreamStarted;

        public event EventHandler SendResultStreamFinished;

        public ChannelType ChannelType { get; }

        public DateTimeOffset StartTime { get; }

        public IServiceProvider ServiceProvider { get; }

        public Dictionary<string, object> Header { get; }

        public InstanceMethod InstanceMethod { get; }

        public ContractMethod ContractMethod { get; }

        public ContractInfo ContractInfo => Contract.ContractInfo;

        public Instance Instance { get; }

        public Contract Contract { get; }

        public Type CallbackType { get; }

        public Func<object, Task> Callback
        {
            get
            {
                var found = Args?.FirstOrDefault(i =>
                {
                    if (i == null)
                        return false;

                    return i.GetType().IsFuncT();
                });
                if (found == null)
                    return null;
                return FuncHelper.ConvertFunc(found);
            }
            set
            {
                if (Args == null)
                    return;

                for (var i = 0; i < Args.Length; i++)
                {
                    if (Args[i] == null)
                        continue;

                    var t = Args[i].GetType();
                    if (t.IsFuncT())
                    {
                        Args[i] = FuncHelper.ConvertFunc(value, CallbackType);
                        return;
                    }
                }
            }
        }

        public CancellationToken CancellationToken { get; }

        public ReadStream Stream { get; }

        public object[] Args { get; }

        /// <summary>
        /// Args of invoked action without stream and action.
        /// </summary>
        public object[] PureArgs { get; }

        public ActionInfo ActionInfo { get; }

        /// <summary>
        /// A central location for sharing state between components during the invoking process.
        /// </summary>
        public Dictionary<object, object> Properties { get; set; } = new Dictionary<object, object>();

        public ActionExecutingContext(IServiceProvider serviceProvider,
            Dictionary<string, object> header,
            Instance instance,
            MethodInfo instanceMethodInfo,
            ContractMethod contractMethod,
            object[] args,
            object[] pureArgs,
            ActionInfo actionInfo,
            ReadStream stream,
            Contract contract,
            ChannelType channelType,
            Func<object, Task> callback,
            CancellationToken token)
        {
            StartTime = DateTimeOffset.Now;
            ServiceProvider = serviceProvider;
            ChannelType = channelType;
            Header = header;
            InstanceMethod = instance.Methods.Find(i => i.MethodInfo == instanceMethodInfo);
            ContractMethod = contractMethod;
            Instance = instance;
            Args = args;
            PureArgs = pureArgs;
            CallbackType = GetFuncType(args);
            ActionInfo = actionInfo;
            Callback = callback;
            Callback = callback;
            Stream = stream;
            Contract = contract;
            CancellationToken = token;
            ResetProps();
        }

        /// <summary>
        /// Gets or sets value inside an action filter will short-circuit the action and any remaining action filters.
        /// </summary>
        public object Result
        {
            get => _result;
            set
            {
                if (value is Task)
                    throw new InvalidCastException("MiddlewareContext Result can not be a Task.");
                _result = value;
            }
        }

        private void ResetProps()
        {
            if (Args == null)
                return;

            for (var i = 0; i < Args.Length; i++)
            {
                if (Args[i] == null)
                    continue;

                if (Args[i].GetType().IsCancellationToken())
                    Args[i] = CancellationToken;

                if (Args[i].GetType().IsSubclassOf(typeof(Stream)))
                    Args[i] = Stream;
            }
        }

        public override string ToString()
        {
            return $"Header:{DicToStringForDisplay(Header)}, MethodName:{InstanceMethod.MethodInfo.Name}, Args:{Helper.ListToStringForDisplay(Args, ",")}";
        }

        private static Type GetFuncType(object[] args)
        {
            foreach (var i in args)
            {
                if (i == null)
                    continue;

                var t = i.GetType();
                if (t.IsFuncT())
                    return t.GetGenericArguments()[0];
            }

            return null;
        }

        public static string DicToStringForDisplay(Dictionary<string, object> header)
        {
            var s = "";
            foreach (var p in header)
                s += $"{p.Key}:{p.Value}, ";
            return s;
        }

        public virtual void OnSendResultStreamFinished()
        {
            SendResultStreamFinished?.Invoke(this, EventArgs.Empty);
        }

        public virtual void OnSendResultStreamStarted()
        {
            SendResultStreamStarted?.Invoke(this, EventArgs.Empty);
        }
    }
}