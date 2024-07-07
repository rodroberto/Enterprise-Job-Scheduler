﻿using System;
using System.Threading;
using System.Threading.Tasks;
using RawRabbit.Operations.StateMachine.Core;
using RawRabbit.Pipe;
using RawRabbit.Pipe.Middleware;

namespace RawRabbit.Operations.StateMachine.Middleware
{
	public class RetrieveStateMachineOptions
	{
		public Func<IPipeContext, Guid> ModelIdFunc { get; set; }
		public Func<IPipeContext, Type> StateMachineTypeFunc { get; set; }
		public Func<IPipeContext, StateMachineBase> StateMachineFunc { get; set; }
		public Action<StateMachineBase, IPipeContext> PostExecuteAction { get; set; }
	}

	public class RetrieveStateMachineMiddleware : Pipe.Middleware.Middleware
	{
		private readonly IStateMachineActivator _stateMachineRepo;
		protected Func<IPipeContext, Guid> ModelIdFunc;
		protected Func<IPipeContext, Type> StateMachineTypeFunc;
		protected Action<StateMachineBase, IPipeContext> PostExecuteAction;
		protected Func<IPipeContext, StateMachineBase> StateMachineFunc;

		public RetrieveStateMachineMiddleware(IStateMachineActivator stateMachineRepo, RetrieveStateMachineOptions options = null)
		{
			_stateMachineRepo = stateMachineRepo;
			ModelIdFunc = options?.ModelIdFunc ?? (context => context.Get(StateMachineKey.ModelId, Guid.NewGuid()));
			StateMachineTypeFunc = options?.StateMachineTypeFunc ?? (context => context.Get<Type>(StateMachineKey.Type));
			StateMachineFunc = options?.StateMachineFunc;
			PostExecuteAction = options?.PostExecuteAction;
		}

		public override async Task InvokeAsync(IPipeContext context, CancellationToken token)
		{
			var id = GetModelId(context);
			var stateMachineType = GetStateMachineType(context);
			var stateMachine = await GetStateMachineAsync(context, id, stateMachineType);
			context.Properties.TryAdd(StateMachineKey.Machine, stateMachine);
			PostExecuteAction?.Invoke(stateMachine, context);
			await Next.InvokeAsync(context, token);
		}

		protected virtual Task<StateMachineBase> GetStateMachineAsync(IPipeContext context, Guid id, Type type)
		{
			var fromContext = StateMachineFunc?.Invoke(context);
			return fromContext != null
				? Task.FromResult(fromContext)
				: _stateMachineRepo.ActivateAsync(id, type);
		}

		protected virtual Type GetStateMachineType(IPipeContext context)
		{
			return StateMachineTypeFunc(context);
		}

		protected virtual Guid GetModelId(IPipeContext context)
		{
			return ModelIdFunc(context);
		}
	}
}
