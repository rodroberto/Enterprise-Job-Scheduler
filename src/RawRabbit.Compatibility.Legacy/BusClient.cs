﻿using RawRabbit.Compatibility.Legacy.Configuration;
using RawRabbit.Enrichers.MessageContext.Context;

namespace RawRabbit.Compatibility.Legacy
{
	public class BusClient : BusClient<MessageContext>, IBusClient
	{
		public BusClient(RawRabbit.IBusClient client, IConfigurationEvaluator configEval) : base(client, configEval)
		{
		}
	}
}
