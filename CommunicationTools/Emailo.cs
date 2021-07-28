using System;
using System.ComponentModel;
using System.Net;
using System.Net.Mail;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace CommunicationTools
{
	public class Emailo : IDisposable
	{
		private readonly NetworkCredential networkCredential;
		private readonly MailAddress senderAddress;
		private SmtpClient client;

		public Emailo(string senderSmtp, SecureString senderPassword, Action<object, AsyncCompletedEventArgs> sendCompletedHandler, string smtp, int port = 587)
		{
			this.networkCredential = new NetworkCredential(senderSmtp, senderPassword);
			this.senderAddress = new MailAddress(senderSmtp);
			this.client = new SmtpClient(smtp);
			this.client.Port = port;
			this.client.EnableSsl = true; // all major clients need ssl these days
			this.client.Credentials = this.networkCredential;
			this.client.SendCompleted += new SendCompletedEventHandler(sendCompletedHandler);
		}

		public async Task SendEmailAsync(string to, string subject, string body,
			MailPriority priority = MailPriority.Normal, bool isBodyHtml = false)
		{
			var receiverAddress = new MailAddress(to);
			var mail = new MailMessage(this.senderAddress, receiverAddress);
			mail.Subject = subject;
			mail.Body = body;

			mail.BodyEncoding = Encoding.UTF8;
			mail.IsBodyHtml = isBodyHtml;
			mail.Priority = priority;
			mail.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;  // send back Non-Delivery report

			await client.SendMailAsync(mail);
		}

		private static void sendCallback(object sender, AsyncCompletedEventArgs args)
		{
			if (args.Cancelled || args.Error != null)
			{
				Console.WriteLine($"Error sending message {args.Error?.Message}");
			}
		}

		public void Dispose() {
			if (this.client != null) {
				this.client.Dispose();
				this.client = null;
			}
		}
	}
}
