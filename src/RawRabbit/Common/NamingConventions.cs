﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace RawRabbit.Common
{
	public interface INamingConventions
	{
		Func<Type, string> ExchangeNamingConvention { get; set; }
		Func<Type, string> QueueNamingConvention { get; set; }
		Func<Type, string> RoutingKeyConvention { get; set; }
		Func<string> ErrorExchangeNamingConvention { get; set; }
		Func<TimeSpan,string> RetryLaterExchangeConvention { get; set; }
		Func<string, TimeSpan,string> RetryLaterQueueNameConvetion { get; set; }
		Func<Type, string> SubscriberQueueSuffix { get; set; }
	}

	public class NamingConventions : INamingConventions
	{
		private readonly ConcurrentDictionary<Type, int> _subscriberCounter;
		private readonly string _applicationName;
		private const string IisWorkerProcessName = "w3wp";
		private static readonly Regex DllRegex = new Regex(@"(?<ApplicationName>[^\\]*).dll", RegexOptions.Compiled);
		private static readonly Regex ConsoleOrServiceRegex = new Regex(@"(?<ApplicationName>[^\\]*).exe", RegexOptions.Compiled);
		private static readonly Regex IisHostedAppRegexVer1 = new Regex(@"-ap\s\\""(?<ApplicationName>[^\\]+)");
		private static readonly Regex IisHostedAppRegexVer2 = new Regex(@"\\\\apppools\\\\(?<ApplicationName>[^\\]+)");

		public virtual Func<Type, string> ExchangeNamingConvention { get; set; }
		public virtual Func<Type, string> QueueNamingConvention { get; set; }
		public virtual Func<Type, string> RoutingKeyConvention { get; set; }
		public virtual Func<string> ErrorExchangeNamingConvention { get; set; }
		public virtual Func<TimeSpan, string> RetryLaterExchangeConvention { get; set; }
		public virtual Func<string, TimeSpan, string> RetryLaterQueueNameConvetion { get; set; }
		public virtual Func<Type, string> SubscriberQueueSuffix { get; set; }

		public NamingConventions()
		{
			_subscriberCounter = new ConcurrentDictionary<Type,int>();
			_applicationName = GetApplicationName(string.Join(" ", Environment.GetCommandLineArgs()));

			ExchangeNamingConvention = type => type?.Namespace?.ToLower() ?? string.Empty;
			QueueNamingConvention = type => CreateShortAfqn(type);
			RoutingKeyConvention = type => CreateShortAfqn(type);
			ErrorExchangeNamingConvention = () => "default_error_exchange";
			SubscriberQueueSuffix = GetSubscriberQueueSuffix;
			RetryLaterExchangeConvention = span => "default_retry_later_exchange";
			RetryLaterQueueNameConvetion = (exchange, span) => $"retry_for_{exchange.Replace(".","_")}_in_{span.TotalMilliseconds}_ms";
		}

		private string GetSubscriberQueueSuffix(Type messageType)
		{
			var sb = new StringBuilder(_applicationName);

			_subscriberCounter.AddOrUpdate(
				key: messageType,
				addValueFactory: type =>
				{
					var next = 0;
					return next;
				},
				updateValueFactory:(type, i) =>
				{
					var next = i+1;
					sb.Append($"_{next}");
					return next;
				});

			return sb.ToString();
		}

		public static string GetApplicationName(params string[] commandLine)
		{
			var match = ConsoleOrServiceRegex.Match(commandLine.FirstOrDefault() ?? string.Empty);
			var applicationName = string.Empty;

			if (commandLine == null)
			{
				return string.Empty;
			}
			if (match.Success && match.Groups["ApplicationName"].Value != IisWorkerProcessName)
			{
				applicationName = match.Groups["ApplicationName"].Value;
				if (applicationName.EndsWith(".vshost"))
					applicationName = applicationName.Remove(applicationName.Length - ".vshost".Length);
			}
			else
			{
				match = IisHostedAppRegexVer1.Match(commandLine.FirstOrDefault() ?? string.Empty);
				if (match.Success)
				{
					applicationName = match.Groups["ApplicationName"].Value;
				}
				else
				{
					match = IisHostedAppRegexVer2.Match(commandLine.FirstOrDefault() ?? string.Empty);
					if (match.Success)
					{
						applicationName = match.Groups["ApplicationName"].Value;
					}
					else
					{
						var index = commandLine.Length > 1 ? 1 : 0;
						if (DllRegex.IsMatch(commandLine[index]))
						{
							applicationName = DllRegex.Match(commandLine[index]).Groups["ApplicationName"].Value;
						}
					}
				}
			}
			
			return applicationName.Replace(".","_").ToLower();
		}

		private static string CreateShortAfqn(Type type, string path = "", string delimeter = ".")
		{
			var t = $"{path}{(string.IsNullOrEmpty(path) ? string.Empty : delimeter)}{GetNonGenericTypeName(type)}";

			if (type.GetTypeInfo().IsGenericType)
			{
				t += "[";
				foreach (var argument in type.GenericTypeArguments)
				{
					t = CreateShortAfqn(argument, t, t.EndsWith("[") ? string.Empty : ",");
				}
				t += "]";
			}

			return (t.Length > 254
				? string.Concat("...", t.Substring(t.Length - 250))
				: t).ToLowerInvariant();
		}

		public static string GetNonGenericTypeName(Type type)
		{
			var name = !type.GetTypeInfo().IsGenericType
				? new[] { type.Name }
				: type.Name.Split('`');

			return name[0];
		}
	}
}