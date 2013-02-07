using ServerSentEvent4Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.SelfHost;

namespace SelfHostDemo
{
    public class SSEController : ApiController
    {
        private static readonly ServerSentEvent SSE = new ServerSentEvent(10, true); 

        public HttpResponseMessage Get(HttpRequestMessage request)
        {
            return SSE.AddSubscriber(request);
        }

        internal static void Send(string s)
        {
            SSE.Send(s);
        }
    }

    class Program
    {
        static readonly Uri _baseAddress = new Uri("http://localhost:4222/");
        
        static void Main(string[] args)
        {
            HttpSelfHostServer server = null;
            try
            {
                // Set up server configuration
                HttpSelfHostConfiguration config = new HttpSelfHostConfiguration(_baseAddress);
                config.TransferMode = System.ServiceModel.TransferMode.Streamed;
                config.Routes.MapHttpRoute(
                    name: "DefaultApi",
                    routeTemplate: "api/{controller}/{id}",
                    defaults: new { id = RouteParameter.Optional }
                );

                // Create server
                server = new HttpSelfHostServer(config);

                // Start listening
                server.OpenAsync().Wait();
                Console.WriteLine("Listening on " + _baseAddress);

                Console.WriteLine("Type exit to quit.");
                while (true)
                {
                    var text = Console.ReadLine();
                    if (text.ToLower() == "exit") break;
                    SSEController.Send(text);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not start server: {0}", e.GetBaseException().Message);
                Console.WriteLine("Hit ENTER to exit...");
                Console.ReadLine();
            }
            finally
            {
                if (server != null)
                {
                    // Stop listening
                    server.CloseAsync().Wait();
                }
            }
        }
    }
}
