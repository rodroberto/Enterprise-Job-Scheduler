﻿using System;
using System.Threading.Tasks;
using RawRabbit.Configuration;
using RawRabbit.Instantiation;
using Xunit;

namespace RawRabbit.IntegrationTests.DependencyInjection
{
	public class SimpleDependencyTests
	{
		[Fact]
		public async Task Should_Honor_Client_Config_From_Options()
		{
			var config = RawRabbitConfiguration.Local;
			const string nonExistingVhost = "/foo";
			config.VirtualHost = nonExistingVhost;
			await Assert.ThrowsAnyAsync<Exception>(async () =>
			{
				var factory = RawRabbitFactory.CreateTestInstanceFactory(new RawRabbitOptions {ClientConfiguration = config});
				var client = factory.Create();
				await client.CreateChannelAsync();
			});
		}
	}
}
