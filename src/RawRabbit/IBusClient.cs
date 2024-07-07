﻿using System;
using System.Threading;
using System.Threading.Tasks;
using RawRabbit.Pipe;

namespace RawRabbit
{
	public interface IBusClient
	{
		Task<IPipeContext> InvokeAsync(Action<IPipeBuilder> pipeCfg, Action<IPipeContext> contextCfg = null, CancellationToken token = default(CancellationToken));
	}
}