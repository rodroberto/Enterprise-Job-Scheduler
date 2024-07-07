﻿using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RabbitMQ.Client;
using RawRabbit.Channel;
using RawRabbit.Channel.Abstraction;
using RawRabbit.Common;
using RawRabbit.Configuration;
using RawRabbit.Configuration.BasicPublish;
using RawRabbit.Configuration.Consume;
using RawRabbit.Configuration.Consumer;
using RawRabbit.Configuration.Exchange;
using RawRabbit.Configuration.Publisher;
using RawRabbit.Configuration.Queue;
using RawRabbit.Consumer;
using RawRabbit.Instantiation;
using RawRabbit.Pipe;
using RawRabbit.Serialization;
using RawRabbit.Subscription;

namespace RawRabbit.DependencyInjection
{
	public static class RawRabbitDependencyRegisterExtension
	{
		public static IDependencyRegister AddRawRabbit(this IDependencyRegister register, RawRabbitOptions options = null)
		{
			register
				.AddSingleton(options?.ClientConfiguration ?? RawRabbitConfiguration.Local)
				.AddSingleton<IConnectionFactory, ConnectionFactory>(provider =>
				{
					var cfg = provider.GetService<RawRabbitConfiguration>();
					return new ConnectionFactory
					{
						VirtualHost = cfg.VirtualHost,
						UserName = cfg.Username,
						Password = cfg.Password,
						Port = cfg.Port,
						HostName = cfg.Hostnames.FirstOrDefault() ?? string.Empty,
						AutomaticRecoveryEnabled = cfg.AutomaticRecovery,
						TopologyRecoveryEnabled = cfg.TopologyRecovery,
						NetworkRecoveryInterval = cfg.RecoveryInterval,
						ClientProperties = provider.GetService<IClientPropertyProvider>().GetClientProperties(cfg),
						Ssl = cfg.Ssl
					};
				})
				.AddSingleton<IChannelPoolFactory, AutoScalingChannelPoolFactory>()
				.AddSingleton(resolver => AutoScalingOptions.Default)
				.AddSingleton<IClientPropertyProvider, ClientPropertyProvider>()
				.AddSingleton<ISerializer>(resolver => new Serialization.JsonSerializer(new Newtonsoft.Json.JsonSerializer
				{
					TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
					Formatting = Formatting.None,
					CheckAdditionalContent = true,
					ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
					ObjectCreationHandling = ObjectCreationHandling.Auto,
					DefaultValueHandling = DefaultValueHandling.Ignore,
					TypeNameHandling = TypeNameHandling.Auto,
					ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
					MissingMemberHandling = MissingMemberHandling.Ignore,
					PreserveReferencesHandling = PreserveReferencesHandling.Objects,
					NullValueHandling = NullValueHandling.Ignore
				}))
				.AddSingleton<IConsumerFactory, ConsumerFactory>()
				.AddSingleton<IChannelFactory>(resolver =>
				{
					var channelFactory = new ChannelFactory(resolver.GetService<IConnectionFactory>(), resolver.GetService<RawRabbitConfiguration>());
					channelFactory
						.ConnectAsync()
						.ConfigureAwait(false)
						.GetAwaiter()
						.GetResult();
					return channelFactory;
				})
				.AddSingleton<ISubscriptionRepository, SubscriptionRepository>()
				.AddSingleton<ITopologyProvider, TopologyProvider>()
				.AddTransient<IPublisherConfigurationFactory, PublisherConfigurationFactory>()
				.AddTransient<IBasicPublishConfigurationFactory, BasicPublishConfigurationFactory>()
				.AddTransient<IConsumerConfigurationFactory, ConsumerConfigurationFactory>()
				.AddTransient<IConsumeConfigurationFactory, ConsumeConfigurationFactory>()
				.AddTransient<IExchangeDeclarationFactory, ExchangeDeclarationFactory>()
				.AddTransient<IQueueConfigurationFactory, QueueDeclarationFactory>()
				.AddSingleton<INamingConventions, NamingConventions>()
				.AddSingleton<IExclusiveLock, ExclusiveLock>()
				.AddSingleton<IBusClient, BusClient>()
				.AddSingleton<IResourceDisposer, ResourceDisposer>()
				.AddTransient<IInstanceFactory>(resolver => new InstanceFactory(resolver))
				.AddSingleton<IPipeContextFactory, PipeContextFactory>()
				.AddTransient<IExtendedPipeBuilder, PipeBuilder>(resolver => new PipeBuilder(resolver))
				.AddSingleton<IPipeBuilderFactory>(provider => new PipeBuilderFactory(provider));

			var clientBuilder = new ClientBuilder();
			options?.Plugins?.Invoke(clientBuilder);
			clientBuilder.DependencyInjection?.Invoke(register);
			register.AddSingleton(clientBuilder.PipeBuilderAction);

			options?.DependencyInjection?.Invoke(register);
			return register;
		}
	}
}
