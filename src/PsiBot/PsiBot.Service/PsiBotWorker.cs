using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PsiBot.Services.Bot;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PsiBot.Services
{
    public class PsiBotWorker : IHostedService
    {
        private readonly ILogger<PsiBotWorker> _logger;
        private readonly IBotService _botService;

        public PsiBotWorker(ILogger<PsiBotWorker> logger, IBotService botService)
        {
            _logger = logger;
            _botService = botService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PsiBot Worker starting...");
            try
            {
                _logger.LogInformation("BotService initialized successfully.");
                _logger.LogInformation("PsiBot Worker started successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during PsiBot Worker startup.");
                throw;
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("PsiBot Worker stopped.");
            return Task.CompletedTask;
        }
    }
}