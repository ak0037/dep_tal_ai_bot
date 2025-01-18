using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Communications.Common.Telemetry;
using PsiBot.Service.Settings;
using PsiBot.Services.Bot;
using PsiBot.Services.Logging;

namespace PsiBot.Services
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();

            services.AddSingleton<IGraphLogger, GraphLogger>(_ => new GraphLogger("PsiBot", redirectToTrace: true));
            services.AddSingleton<InMemoryObserver, InMemoryObserver>();

            services.Configure<BotConfiguration>(Configuration.GetSection(nameof(BotConfiguration)));
            services.AddSingleton(sp =>
            {
                var botConfig = sp.GetRequiredService<IOptions<BotConfiguration>>().Value;
                botConfig.Initialize();
                return botConfig;
            });

            services.AddSingleton<IBotService, BotService>(provider =>
            {
                var logger = provider.GetRequiredService<IGraphLogger>();
                var botConfig = provider.GetRequiredService<BotConfiguration>();
                var bot = new BotService(logger, Options.Create(botConfig));
                bot.Initialize();
                return bot;
            });

            services.AddHostedService<PsiBotWorker>();
        }
        
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();
        }
    }
}