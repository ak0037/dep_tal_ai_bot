using System.ServiceProcess;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace PsiBot.Services
{
    public class PsiBotWindowsService : ServiceBase
    {
        private IWebHost _webHost;

        public PsiBotWindowsService()
        {
            ServiceName = "PsiBot Service";
        }

        protected override void OnStart(string[] args)
        {
            _webHost = Program.CreateWebHostBuilder(args).Build();
            _webHost.Start();
        }

        protected override void OnStop()
        {
            _webHost.StopAsync().GetAwaiter().GetResult();
            _webHost.Dispose();
        }
    }
}