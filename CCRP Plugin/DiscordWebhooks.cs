using CCRPPlugin;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
namespace CCRP_Plugin
{
    public class DiscordWebhooks
    {
		public static Main Instance;

		private static void QueueWebhook(Action sendWebhook, string eventName)
		{
			Task.Run(() =>
			{
				try
				{
					sendWebhook();
				}
				catch (Exception exception)
				{
					Rocket.Core.Logging.Logger.LogWarning($"Discord webhook '{eventName}' failed: {exception.Message}");
				}
			});
		}

		private static WebRequest CreateRequest(string webhookLink)
		{
			if (!Uri.TryCreate(webhookLink, UriKind.Absolute, out Uri uri) ||
				(uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
			{
				throw new InvalidOperationException("Webhook URL is missing or invalid.");
			}

			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
			request.ContentType = "application/json";
			request.Method = "POST";
			request.Timeout = 5000;
			request.ReadWriteTimeout = 5000;
			return request;
		}

		public static void DiscordKickWebhook(string callerName, string callerId, string kickName, string kickId, string reason)
        {
			string webhookLink = Main.Instance.Configuration.Instance.Webhooks.Kick;
			QueueWebhook(() => DiscordKickWebhookCore(webhookLink, callerName, callerId, kickName, kickId, reason), "kick");
		}

		private static void DiscordKickWebhookCore(string webhookLink, string callerName, string callerId, string kickName, string kickId, string reason)
		{
			WebRequest webRequest = CreateRequest(webhookLink);

			using(var streamWriter = new StreamWriter(webRequest.GetRequestStream()))
            {
				string json = JsonConvert.SerializeObject(new
				{
					username = "CCRP Kick Log",
					embeds = new[]
					{
					new
					{
						description = $"**Player**\n" +
						$"{kickName} ({kickId})\n" +
						$"**Punisher**\n" +
						$"{callerName} ({callerId})\n" +
						$"**Reason**\n" +
						$"{reason}\n" +
						$"{DateTime.Now}",
						title = "Player Kicked",
						color = "1752220"
					}

					}});
				streamWriter.Write(json);
            }

			using (webRequest.GetResponse()) { }
        }
		public static void DiscordBanWebhook(string callerName, string callerId, string banId, string reason, string unbanDate)
		{
			string webhookLink = Main.Instance.Configuration.Instance.Webhooks.Ban;
			QueueWebhook(() => DiscordBanWebhookCore(webhookLink, callerName, callerId, banId, reason, unbanDate), "ban");
		}

		private static void DiscordBanWebhookCore(string webhookLink, string callerName, string callerId, string banId, string reason, string unbanDate)
		{
			if(unbanDate == "perm")
            {
				unbanDate = "Permanently";
            }

			WebRequest webRequest = CreateRequest(webhookLink);

			using (var streamWriter = new StreamWriter(webRequest.GetRequestStream()))
			{
				string json = JsonConvert.SerializeObject(new
				{
					username = "CCRP Ban Log",
					embeds = new[]
					{
					new
					{
						description =
						$"**Moderator**\n" +
						$"{callerName} ({callerId})\n" +
						$"**Player**\n" +
						$"{banId}\n" +
						$"**Reason**\n" +
						$"{reason}\n" +
						$"**Ban date**\n" +
						$"{DateTime.Now}\n" +
						$"**Unban date**\n" +
						$"{unbanDate}",
						title = "Player Banned",
						color = "1752220"
					}

					}
				});
				streamWriter.Write(json);
			}

			using (webRequest.GetResponse()) { }
		}
		public static void DiscordUnbanWebhook(string callerName, string callerId, ulong unbanId, string reason)
		{
			string webhookLink = Main.Instance.Configuration.Instance.Webhooks.Ban;
			QueueWebhook(() => DiscordUnbanWebhookCore(webhookLink, callerName, callerId, unbanId, reason), "unban");
		}

		private static void DiscordUnbanWebhookCore(string webhookLink, string callerName, string callerId, ulong unbanId, string reason)
		{
			WebRequest webRequest = CreateRequest(webhookLink);

			using (var streamWriter = new StreamWriter(webRequest.GetRequestStream()))
			{
				string json = JsonConvert.SerializeObject(new
				{
					username = "CCRP Unban Log",
					embeds = new[]
					{
					new
					{
						description = 
						$"**Moderator**\n" +
						$"{callerName} ({callerId})\n" +
						$"**Player**\n" +
						$"{unbanId}\n" +
						$"**Reason**\n" +
						$"{reason}\n" +
						$"{DateTime.Now}",
						title = "Player Unbanned",
						color = "1752220"
					}

					}
				});
				streamWriter.Write(json);
			}

			using (webRequest.GetResponse()) { }
		}
		public static void DiscordWarnWebHook(string callerName, string callerId, string warnName, string warnId, string reason, int currentWarns, string action)
		{
			string webhookLink = Main.Instance.Configuration.Instance.Webhooks.Warn;
			QueueWebhook(() => DiscordWarnWebhookCore(webhookLink, callerName, callerId, warnName, warnId, reason, currentWarns, action), "warning");
		}

		private static void DiscordWarnWebhookCore(string webhookLink, string callerName, string callerId, string warnName, string warnId, string reason, int currentWarns, string action)
		{
			if (action == "add")
			{
				WebRequest webRequest = CreateRequest(webhookLink);

				using (var streamWriter = new StreamWriter(webRequest.GetRequestStream()))
				{
					string json = JsonConvert.SerializeObject(new
					{
						username = "CCRP Warn Log",
						embeds = new[]
						{
					new
					{
						description = $"**Player**\n" +
						$"{warnName} ({warnId})\n" +
						$"**Punisher**\n" +
						$"{callerName} ({callerId})\n" +
						$"**Reason**\n" +
						$"{reason}\n" +
						$"**Warnings**\n" +
						$"{currentWarns}\n" +
						$"{DateTime.Now}",
						title = "Player Warned",
						color = "1752220"
					}

					}
					});
					streamWriter.Write(json);
				}
				using (webRequest.GetResponse()) { }
			}
			if(action == "remove")
            {
				WebRequest webRequest = CreateRequest(webhookLink);

				using (var streamWriter = new StreamWriter(webRequest.GetRequestStream()))
				{
					string json = JsonConvert.SerializeObject(new
					{
						username = "CCRP Warn Log",
						embeds = new[]
						{
					new
					{
						description = $"**Player**\n" +
						$"{warnName} ({warnId})\n" +
						$"**Punisher**\n" +
						$"{callerName} ({callerId})\n" +
						$"**Reason**\n" +
						$"{reason}\n" +
						$"**Warnings**\n" +
						$"{currentWarns}\n" +
						$"{DateTime.Now}",
						title = "Player Warn Removed",
						color = "1752220"
					}

					}
					});
					streamWriter.Write(json);
				}
				using (webRequest.GetResponse()) { }
			}
		}
		public static void DiscordBuildermodeWebhook(string callerName, string callerId, string onOrOff)
		{
			string webhookLink = Main.Instance.Configuration.Instance.Webhooks.BuilderMode;
			QueueWebhook(() => DiscordBuildermodeWebhookCore(webhookLink, callerName, callerId, onOrOff), "builder mode");
		}

		private static void DiscordBuildermodeWebhookCore(string webhookLink, string callerName, string callerId, string onOrOff)
		{
			WebRequest webRequest = CreateRequest(webhookLink);

			using (var streamWriter = new StreamWriter(webRequest.GetRequestStream()))
			{
				string json = JsonConvert.SerializeObject(new
				{
					username = "CCRP Buildermode Log",
					embeds = new[]
					{
					new
					{
						description = 
						$"**Player**\n" +
						$"{callerName} ({callerId})\n" +
						$"**Action**\n" +
						$"{onOrOff}\n" +
						$"{DateTime.Now.ToString("MM/dd/yyyy h:mm")}",

						title = "Buildermode",
						color = "1752220"
					}

					}
				});
				streamWriter.Write(json);
			}

			using (webRequest.GetResponse()) { }
		}

		public static void DiscordWreckWebhook(string callerName, string callerId, string commandText, string result, string location)
		{
			string webhookLink = Main.Instance.Configuration.Instance.Webhooks.Wreck;
			QueueWebhook(() => DiscordWreckWebhookCore(webhookLink, callerName, callerId, commandText, result, location), "wreck");
		}

		private static void DiscordWreckWebhookCore(string webhookLink, string callerName, string callerId, string commandText, string result, string location)
		{
			WebRequest webRequest = CreateRequest(webhookLink);

			using (var streamWriter = new StreamWriter(webRequest.GetRequestStream()))
			{
				string json = JsonConvert.SerializeObject(new
				{
					username = "CCRP Wreck Log",
					embeds = new[]
					{
					new
					{
						description =
						$"**Player**\n" +
						$"{callerName} ({callerId})\n" +
						$"**Command**\n" +
						$"{commandText}\n" +
						$"**Result**\n" +
						$"{result}\n" +
						$"**Location**\n" +
						$"{location}\n" +
						$"{DateTime.Now}",
						title = "Wreck Command",
						color = "15158332"
					}

					}
				});
				streamWriter.Write(json);
			}

			using (webRequest.GetResponse()) { }
		}

		public static void DiscordWreckWebhook(string callerName, string callerId, string commandText, string result, string location, string reason, byte[] screenshot)
		{
			string webhookLink = Main.Instance.Configuration.Instance.Webhooks.Wreck;
			QueueWebhook(() => DiscordWreckConfirmWebhookCore(webhookLink, callerName, callerId, commandText, result, location, reason, screenshot), "wreck");
		}

		private static void DiscordWreckConfirmWebhookCore(string webhookLink, string callerName, string callerId, string commandText, string result, string location, string reason, byte[] screenshot)
		{
			bool hasScreenshot = screenshot != null && screenshot.Length > 0;
			object embed = new
			{
				description =
					$"**Player**\n" +
					$"{callerName} ({callerId})\n" +
					$"**Command**\n" +
					$"{commandText}\n" +
					$"**Reason**\n" +
					$"{reason}\n" +
					$"**Result**\n" +
					$"{result}\n" +
					$"**Location**\n" +
					$"{location}\n" +
					$"{DateTime.Now}",
				title = "Wreck Command",
				color = "15158332",
				image = hasScreenshot ? new { url = "attachment://wreck.jpg" } : null
			};

			string json = JsonConvert.SerializeObject(new
			{
				username = "CCRP Wreck Log",
				allowed_mentions = new { parse = new string[0] },
				embeds = new[] { embed }
			}, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

			WebRequest webRequest = CreateRequest(webhookLink);
			webRequest.Timeout = 15000;

			if (!hasScreenshot)
			{
				using (var streamWriter = new StreamWriter(webRequest.GetRequestStream()))
				{
					streamWriter.Write(json);
				}
			}
			else
			{
				string boundary = "----CCRP" + Guid.NewGuid().ToString("N");
				webRequest.ContentType = "multipart/form-data; boundary=" + boundary;
				using (Stream body = webRequest.GetRequestStream())
				{
					byte[] head = Encoding.UTF8.GetBytes(
						$"--{boundary}\r\nContent-Disposition: form-data; name=\"payload_json\"\r\nContent-Type: application/json\r\n\r\n{json}\r\n" +
						$"--{boundary}\r\nContent-Disposition: form-data; name=\"files[0]\"; filename=\"wreck.jpg\"\r\nContent-Type: image/jpeg\r\n\r\n");
					byte[] tail = Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");
					body.Write(head, 0, head.Length);
					body.Write(screenshot, 0, screenshot.Length);
					body.Write(tail, 0, tail.Length);
				}
			}

			using (webRequest.GetResponse()) { }
		}

		public static void DiscordBypassWebhook(string callerName, string callerId, string commandText, string result)
		{
			string webhookLink = Main.Instance.Configuration.Instance.Webhooks.Bypass;
			QueueWebhook(() => DiscordBypassWebhookCore(webhookLink, callerName, callerId, commandText, result), "bypass");
		}

		private static void DiscordBypassWebhookCore(string webhookLink, string callerName, string callerId, string commandText, string result)
		{
			WebRequest webRequest = CreateRequest(webhookLink);

			using (var streamWriter = new StreamWriter(webRequest.GetRequestStream()))
			{
				string json = JsonConvert.SerializeObject(new
				{
					username = "CCRP Bypass Log",
					embeds = new[]
					{
						new
						{
							description =
								$"**Player**\n{callerName} ({callerId})\n" +
								$"**Command**\n{commandText}\n" +
								$"**Result**\n{result}\n" +
								$"{DateTime.Now}",
							title = "Donation Bypass Command",
							color = "1752220"
						}
					}
				});
				streamWriter.Write(json);
			}

			using (webRequest.GetResponse()) { }
		}

		public static void DiscordTeleportWebhook(string callerName, string callerId, string teleportAction, string teleportToName = null,
			string teleportToId = null, string x = null, string y = null, string z = null)
		{
			string webhookLink = Main.Instance.Configuration.Instance.Webhooks.Teleport;
			QueueWebhook(() => DiscordTeleportWebhookCore(webhookLink, callerName, callerId, teleportAction, teleportToName, teleportToId, x, y, z), "teleport");
		}

		private static void DiscordTeleportWebhookCore(string webhookLink, string callerName, string callerId, string teleportAction, string teleportToName,
			string teleportToId, string x, string y, string z)
		{
			if(teleportAction == "player")
            {
				WebRequest webRequest = CreateRequest(webhookLink);

				using (var streamWriter = new StreamWriter(webRequest.GetRequestStream()))
				{
					string json = JsonConvert.SerializeObject(new
					{
						username = "CCRP Teleport Log",
						embeds = new[]
						{
					new
					{
						description =
						$"**Moderator**\n" +
						$"{callerName} ({callerId})\n" +
						$"**Player**\n" +
						$"{teleportToName} ({teleportToId})\n" +
						$"**Coordinates**\n" +
						$"X: {x}\n Y: {y}\n Z: {z}\n" +
						$"{DateTime.Now}",
						title = "Player Teleport to Player",
						color = "1752220"
					}

					}
					});
					streamWriter.Write(json);
				}

				using (webRequest.GetResponse()) { }
				return;

			} 
			if(teleportAction == "tphere")
            {
				WebRequest webRequest1 = CreateRequest(webhookLink);

				using (var streamWriter = new StreamWriter(webRequest1.GetRequestStream()))
				{
					string json = JsonConvert.SerializeObject(new
					{
						username = "CCRP Teleport Log",
						embeds = new[]
						{
					new
					{
						description = 
						$"**Moderator**\n" +
						$"{callerName} ({callerId})\n" +
						$"**Player**\n" +
						$"{teleportToName} ({teleportToId})\n" +
						$"**Coordinates**\n" +
						$"X: {x}\n Y: {y}\nZ: {z}\n" +
						$"{DateTime.Now}",
						title = "Tphere",
						color = "1752220"
					}

					}
					});
					streamWriter.Write(json);
				}

				using (webRequest1.GetResponse()) { }
				return;
			}
			if(teleportAction == "coords")
            {
				WebRequest webRequest2 = CreateRequest(webhookLink);

				using (var streamWriter = new StreamWriter(webRequest2.GetRequestStream()))
				{
					string json = JsonConvert.SerializeObject(new
					{
						username = "CCRP Teleport Log",
						embeds = new[]
						{
					new
					{
						description = 
						$"**Moderator**\n" +
						$"{callerName} ({callerId})\n" +
						$"**Coordinates**\n" +
						$"X: {x}\n Y: {y}\nZ: {z}\n" +
						$"{DateTime.Now}",
						title = "Player Teleport to Coordinates",
						color = "1752220"
					}

					}
					});
					streamWriter.Write(json);
				}

				using (webRequest2.GetResponse()) { }
			}
			if (teleportAction == "city")
			{
				WebRequest webRequest2 = CreateRequest(webhookLink);

				using (var streamWriter = new StreamWriter(webRequest2.GetRequestStream()))
				{
					string json = JsonConvert.SerializeObject(new
					{
						username = "CCRP Teleport Log",
						embeds = new[]
						{
					new
					{
						description =
						$"**Moderator**\n" +
						$"{callerName} ({callerId})\n" +
						$"**City**\n" +
						$"{teleportToName}\n" +
						$"{DateTime.Now}",
						title = "Player Teleport to City",
						color = "1752220"
					}

					}
					});
					streamWriter.Write(json);
				}

				using (webRequest2.GetResponse()) { }
			}

		}

	}
}
