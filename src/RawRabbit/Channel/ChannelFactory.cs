﻿using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RawRabbit.Channel.Abstraction;
using RawRabbit.Configuration;
using RawRabbit.Exceptions;
using RawRabbit.Logging;

namespace RawRabbit.Channel
{
	public class ChannelFactory : IChannelFactory
	{
		private readonly ILog _logger = LogProvider.For<ChannelFactory>();
		protected readonly IConnectionFactory ConnectionFactory;
		protected readonly RawRabbitConfiguration ClientConfig;
		protected readonly ConcurrentBag<IModel> Channels;
		protected IConnection Connection;

		public ChannelFactory(IConnectionFactory connectionFactory, RawRabbitConfiguration config)
		{
			ConnectionFactory = connectionFactory;
			ClientConfig = config;
			Channels = new ConcurrentBag<IModel>();
		}

		public virtual Task ConnectAsync(CancellationToken token = default(CancellationToken))
		{
			try
			{
				_logger.Debug("Creating a new connection for {hostNameCount} hosts.", ClientConfig.Hostnames.Count);
				Connection = ConnectionFactory.CreateConnection(ClientConfig.Hostnames, ClientConfig.ClientProvidedName);
				Connection.ConnectionShutdown += (sender, args) =>
					_logger.Warn("Connection was shutdown by {Initiator}. ReplyText {ReplyText}", args.Initiator, args.ReplyText);
			}
			catch (BrokerUnreachableException e)
			{
				_logger.Info("Unable to connect to broker", e);
				throw;
			}
			return Task.FromResult(true);
		}

		public virtual async Task<IModel> CreateChannelAsync(CancellationToken token = default(CancellationToken))
		{
			var connection = await GetConnectionAsync(token);
			token.ThrowIfCancellationRequested();
			var channel = connection.CreateModel();
			Channels.Add(channel);
			return channel;
		}

		protected virtual async Task<IConnection> GetConnectionAsync(CancellationToken token = default(CancellationToken))
		{
			token.ThrowIfCancellationRequested();
			if (Connection == null)
			{
				await ConnectAsync(token);
			}
			if (Connection.IsOpen)
			{
				_logger.Debug("Existing connection is open and will be used.");
				return Connection;
			}
			_logger.Info("The existing connection is not open.");

			if (Connection.CloseReason != null &&Connection.CloseReason.Initiator == ShutdownInitiator.Application)
			{
				_logger.Info("Connection is closed with Application as initiator. It will not be recovered.");
				Connection.Dispose();
				throw new ChannelAvailabilityException("Closed connection initiated by the Application. A new connection will not be created, and no channel can be created.");
			}

			if (!(Connection is IRecoverable recoverable))
			{
				_logger.Info("Connection is not recoverable");
				Connection.Dispose();
				throw new ChannelAvailabilityException("The non recoverable connection is closed. A channel can not be created.");
			}

			_logger.Debug("Connection is recoverable. Waiting for 'Recovery' event to be triggered. ");
			var recoverTcs = new TaskCompletionSource<IConnection>();
			token.Register(() => recoverTcs.TrySetCanceled());

			EventHandler<EventArgs> completeTask = null;
			completeTask = (sender, args) =>
			{
				if (recoverTcs.Task.IsCanceled)
				{
					return;
				}
				_logger.Info("Connection has been recovered!");
				recoverTcs.TrySetResult(recoverable as IConnection);
				recoverable.Recovery -= completeTask;
			};

			recoverable.Recovery += completeTask;
			return await recoverTcs.Task;
		}

		public void Dispose()
		{
			foreach (var channel in Channels)
			{
				channel?.Dispose();
			}
			Connection?.Dispose();
		}
	}
}
