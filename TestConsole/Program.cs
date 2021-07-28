using System;
using System.ComponentModel;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using NetworkTools;
using CommunicationTools;

namespace TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            // TestTraceRoute();
            // await TestSendEmail();
        }

        static void TestTraceRoute()
        {
	        var traceroute = new TraceRoute();
	        var hops = traceroute.Tracert("www.google.com");
	        foreach (var hop in hops)
	        {
		        Console.WriteLine($"{hop.HopID} {hop.ReplyTime}ms {hop.ReplyStatus} {hop.Address} ({hop.Hostname})");
	        }
        }

        async static Task TestSendEmail()
        {
	        var sender = "";
	        var unsecurePassword = "";
	        var receiver = "";
	        var secure = new SecureString();
	        foreach(var c in unsecurePassword)
	        {
		        secure.AppendChar(c);
	        }

	        var subject = "Hello";
	        var body = "Test body";

	        using (var client = new Emailo(sender, secure, sendCallback, "smtp.gmail.com"))
	        {
		        await client.SendEmailAsync(receiver, subject, body);  // because this is async any operations after .Dispose() will be cancelled so use carefully
		        Console.WriteLine("Sleeping for 5 seconds");
		        Thread.Sleep(5000);
	        }
        }

        private static void sendCallback(object sender, AsyncCompletedEventArgs args)
        {
	        if (args.Cancelled || args.Error != null)
	        {
		        Console.WriteLine($"Error sending message {args.Error?.Message} | cancelled: {args.Cancelled}");
		        return;
	        }
	        Console.WriteLine("Email sent!");
        }
    }
}
