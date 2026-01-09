using gRPCClient.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Grpc.Net.Client;
using Mirosnicenco_Eugenia_gRPCService;
using System.Threading;
using Grpc.Core;

namespace gRPCClient.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet("Home/Unary/{id}")]
        public async Task<IActionResult> Unary(int id)
        {
            var channel = GrpcChannel.ForAddress("https://localhost:7181");
            var client = new Greeter.GreeterClient(channel);
            var reply = await client.SendStatusAsync(new SRequest { No = id });
            return View("ShowStatus", (object)ChangetoDictionary(reply));
        }
        private Dictionary<string, string> ChangetoDictionary(SResponse response)
        {
            Dictionary<string, string> statusDict = new Dictionary<string, string>();
            foreach (StatusInfo status in response.StatusInfo)
                statusDict.Add(status.Author, status.Description);
            return statusDict;
        }

        [HttpGet("Home/ServerStreaming/{id}")]
        public async Task<IActionResult> ServerStreaming(int id)
        {
            var channel = GrpcChannel.ForAddress("https://localhost:7181");
            var client = new Greeter.GreeterClient(channel);
            Dictionary<string, string> statusDict = new Dictionary<string, string>();
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            using (var call = client.SendStatusSS(new SRequest { No = id}, cancellationToken:
           cts.Token))
            {
                try
                {
                    await foreach (var message in call.ResponseStream.ReadAllAsync())
                    {
                        statusDict[message.StatusInfo[0].Author] = message.StatusInfo[0].Description;
                    }
                }
                catch (RpcException ex) when (ex.StatusCode ==
               Grpc.Core.StatusCode.Cancelled)
                {
                    // Log Stream cancelled
                }
            }
            return View("ShowStatus", (object)statusDict);
        }

        [HttpGet("Home/ClientStreaming")]
        public async Task<IActionResult> ClientStreaming([FromQuery] string id)
        {
            var channel = GrpcChannel.ForAddress("https://localhost:7181");
            var client = new Greeter.GreeterClient(channel);
            Dictionary<string, string> statusDict = new Dictionary<string, string>();
            int[] statusNos = id?.Split(',').Select(int.Parse).ToArray() ?? new int[0];

            using var call = client.SendStatusCS();
            foreach (var no in statusNos)
                await call.RequestStream.WriteAsync(new SRequest { No = no });

            await call.RequestStream.CompleteAsync();
            var response = await call.ResponseAsync;

            foreach (var status in response.StatusInfo)
                statusDict[status.Author] = status.Description;

            return View("ShowStatus", (object)statusDict);
        }

        [HttpGet("Home/BiDirectionalStreaming")]
        public async Task<IActionResult> BiDirectionalStreaming([FromQuery] string id)
        {
            var channel = GrpcChannel.ForAddress("https://localhost:7181");
            var client = new Greeter.GreeterClient(channel);
            Dictionary<string, string> statusDict = new Dictionary<string, string>();

            int[] statusNos = id?.Split(',').Select(int.Parse).ToArray() ?? new int[0];

            using var call = client.SendStatusBD();

            var readTask = Task.Run(async () =>
            {
                await foreach (var msg in call.ResponseStream.ReadAllAsync())
                {
                    foreach (var status in msg.StatusInfo)
                        statusDict[status.Author] = status.Description;
                }
            });

            foreach (var no in statusNos)
                await call.RequestStream.WriteAsync(new SRequest { No = no });

            await call.RequestStream.CompleteAsync();
            await readTask;

            return View("ShowStatus", (object)statusDict);
        }

    }
}
