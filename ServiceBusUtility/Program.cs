using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ServiceBusUtility
{
	class Program
	{
		private static readonly string _connection = "Endpoint=sb://ark-playground.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=fc3hUuRJJmx/IpQ+89QyYP8VVA6IkwQcToSEt/51+rU=";

		private static readonly string _topicName = "test_first";

		static void Main(string[] args)
		{
			while (true)
			{
				var suc = NamespaceManager.CreateFromConnectionString(_connection);

				if (suc.TopicExists(_topicName))
				{
					var subscription = suc.GetSubscriptions(_topicName).ToList();

					foreach (var s in subscription)
					{
						var name = s.Name;
						var active = s.MessageCountDetails.ActiveMessageCount;
						var dead = s.MessageCountDetails.DeadLetterMessageCount;

						Console.WriteLine($"{s.Name}____{s.Status}:::{active} ---- Morti:::{dead}");

						var infos = new Dictionary<string, SubsInfo>
							{
								{
									name,
									new SubsInfo()
									{
										ActiveMessage = (int) active,
										DeadMessage = (int) dead,
										SubName = name
									}
								}
							};


						_CleanMessagesInSub(name, infos.FirstOrDefault());
					}


					Console.WriteLine($"------------------------------------------------------------------");
					Thread.Sleep(3000);
				}
			}

		}


		static void _CleanMessagesInSub(string subscriptionName, KeyValuePair<string, SubsInfo> info)
		{
			var zuc = SubscriptionClient.CreateFromConnectionString(_connection, _topicName, subscriptionName);
			for (int msg = 0; msg < info.Value.ActiveMessage; msg++)
			{
				var santos = zuc.Receive();
				zuc.Complete(santos.LockToken);
				var vivi = info.Value.ActiveMessage - msg;
				var morti = info.Value.DeadMessage == 0 ? 0 : info.Value.DeadMessage - msg;
				Console.WriteLine($"{info.Key}____{info.Value.SubName}:::{vivi} ---- Morti:::{morti}");
				Console.WriteLine($"------------------------------------------------------------------");
			}
		}

		public class SubsInfo
		{
			public string? SubName { get; set; }
			public int ActiveMessage { get; set; }
			public int DeadMessage { get; set; }
		}
	}
}
