
// using PsiBot.Service.Settings;
// using Microsoft.AspNetCore;
// using Microsoft.AspNetCore.Hosting;
// using Microsoft.Extensions.Configuration;
// using System.Security.Authentication;

// namespace PsiBot.Services
// {
//     public class Program
//     {
//         public static void Main(string[] args)
//         {
//             CreateWebHostBuilder(args).Build().Run();
//         }

//         public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
//             WebHost.CreateDefaultBuilder(args)
//                 .UseStartup<Startup>()
//                 .UseKestrel((ctx, opt) =>
//                 {
//                     var config = new BotConfiguration();
//                     ctx.Configuration.GetSection(nameof(BotConfiguration)).Bind(config);
//                     config.Initialize();
//                     opt.Configure()
//                         .Endpoint("HTTPS", listenOptions =>
//                         {
//                             listenOptions.HttpsOptions.SslProtocols = SslProtocols.Tls12;
//                         });
//                     opt.ListenAnyIP(config.CallSignalingPort, o => o.UseHttps());
//                     opt.ListenAnyIP(config.CallSignalingPort + 1);
//                 });
//     }
// }

// using System.ServiceProcess;
// using Microsoft.AspNetCore;
// using Microsoft.AspNetCore.Hosting;
// using Microsoft.Extensions.DependencyInjection;
// using PsiBot.Service.Settings;
// using System.Security.Authentication;
// using Microsoft.Extensions.Configuration;
// using System;

// namespace PsiBot.Services
// {
//     public class Program
//     {
//         public static void Main(string[] args)
//         {
//             if (Environment.UserInteractive)
//             {
//                 // Run as console application
//                 CreateWebHostBuilder(args).Build().Run();
//             }
//             else
//             {
//                 // Run as Windows Service
//                 using (var service = new PsiBotWindowsService())
//                 {
//                     ServiceBase.Run(service);
//                 }
//             }
//         }

//         public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
//             WebHost.CreateDefaultBuilder(args)
//                 .UseStartup<Startup>()
//                 .ConfigureServices((context, services) =>
//                 {
//                     services.AddHostedService<PsiBotWorker>();
//                 })
//                 .UseKestrel((ctx, opt) =>
//                 {
//                     var config = new BotConfiguration();
//                     ctx.Configuration.GetSection(nameof(BotConfiguration)).Bind(config);
//                     config.Initialize();
//                     opt.Configure()
//                         .Endpoint("HTTPS", listenOptions =>
//                         {
//                             listenOptions.HttpsOptions.SslProtocols = SslProtocols.Tls12;
//                         });
//                     opt.ListenAnyIP(config.CallSignalingPort, o => o.UseHttps());
//                     opt.ListenAnyIP(config.CallSignalingPort + 1);
//                 });
//     }
// }



using System;
using System.ServiceProcess;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using PsiBot.Service.Settings;
using System.Security.Authentication;
using Microsoft.Extensions.Configuration;

namespace PsiBot.Services
{   
    public class Program
    {
        public static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                // Run as console application
                CreateWebHostBuilder(args).Build().Run();
            }
            else
            {
                // Run as Windows Service
                using (var service = new PsiBotWindowsService())
                {
                    ServiceBase.Run(service);
                }
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .ConfigureServices((context, services) =>
                {
                    services.AddHostedService<PsiBotWorker>();
                })
                .UseKestrel((ctx, opt) =>
                {
                    var config = new BotConfiguration();
                    ctx.Configuration.GetSection(nameof(BotConfiguration)).Bind(config);
                    config.Initialize();
                    opt.ListenAnyIP(443, listenOptions =>
                    {
                        listenOptions.UseHttps(config.GetCertificateFromStore());
                    });
                    opt.ListenAnyIP(config.CallSignalingPort, o => o.UseHttps(config.GetCertificateFromStore()));
                    opt.ListenAnyIP(config.CallSignalingPort + 1);
                    opt.ListenAnyIP(config.InstancePublicPort, o => o.UseHttps(config.GetCertificateFromStore()));
                });
    }
}