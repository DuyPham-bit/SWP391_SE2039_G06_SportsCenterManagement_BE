using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SportsCenterManagement.API.Models;
using SportsCenterManagement.API.Services;
using SportsCenterManagement.DAL.Authorization;

namespace SportsCenterManagement.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = RoleNames.Admin)]
    [EnableRateLimiting("mail-send")]
    public class MailController : ControllerBase
    {
        private readonly ISendMailService _sendMailService;

        public MailController(ISendMailService sendMailService)
        {
            _sendMailService = sendMailService;
        }

        [HttpPost("send")]
        public async Task<IActionResult> TestSendMail([FromBody] MailContent request)
        {
            string result = await _sendMailService.SendMailAsync(request);
            return Ok(new { message = result });
        }
    }
}
