
using PsiBot.Model.Constants;
using PsiBot.Model.Models;
using PsiBot.Service.Settings;
using PsiBot.Services.Bot;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Communications.Common.Telemetry;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace PsiBot.Services.Controllers
{
    /// <summary>
    /// JoinCallController is a third-party controller (non-Bot Framework) that can be called in CVI scenario to trigger the bot to join a call.
    /// </summary>
    public class JoinCallController : ControllerBase
    {
        /// <summary>
        /// The logger
        /// </summary>
        private readonly IGraphLogger _logger;

        /// <summary>
        /// The bot service
        /// </summary>
        private readonly IBotService _botService;

        /// <summary>
        /// The bot configuration
        /// </summary>
        private readonly BotConfiguration botConfiguration;

        /// <summary>
        /// Initializes a new instance of the <see cref="JoinCallController" /> class.

        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="eventPublisher">The event publisher.</param>
        /// <param name="botService">The bot service.</param>
        /// <param name="settings">The bot configuration.</param>
        public JoinCallController(IBotService botService, IOptions<BotConfiguration> botConfiguration, IGraphLogger logger)
        {
            _logger = logger;
            _botService = botService;
            this.botConfiguration = botConfiguration.Value;
        }

        /// <summary>
        /// The join call async.
        /// </summary>
        /// <param name="joinCallBody">The join call body.</param>
        /// <returns>The <see cref="HttpResponseMessage" />.</returns>
        [HttpPost]
        [Route(HttpRouteConstants.JoinCall)]
        public async Task<IActionResult> JoinCallAsync([FromBody] JoinCallBody joinCallBody)
        {
            _logger.Info($"Attempting to join call with ID: {joinCallBody.DisplayName}");
            try
            {
                var call = await _botService.JoinCallAsync(joinCallBody).ConfigureAwait(false);
                var callPath = $"/{HttpRouteConstants.CallRoute.Replace("{callLegId}", call.Id)}";
                var callUri = $"{botConfiguration.ServiceCname}{callPath}";
                var values = new JoinURLResponse()
                {
                    Call = callUri,
                    CallId = call.Id,
                    ScenarioId = call.ScenarioId,
                    Logs = callUri.Replace("/calls/", "/logs/")
                };
                _logger.Info($"Successfully joined call with ID: {call.Id}");
                return Ok(values);
            }
            catch (ServiceException e)
            {
                _logger.Error(e, $"ServiceException while joining call: {e.Message}");
                return StatusCode((int)e.StatusCode, new { error = e.Message, details = e.ToString() });
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Unexpected error while joining call: {e.Message}");
                return StatusCode(500, new { error = "Internal server error", details = e.Message });
            }
        }
    }
}
