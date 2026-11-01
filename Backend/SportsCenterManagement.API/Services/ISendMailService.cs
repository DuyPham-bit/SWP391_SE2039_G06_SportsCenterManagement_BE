using SportsCenterManagement.API.Models;

namespace SportsCenterManagement.API.Services
{
    public interface ISendMailService
    {
        Task<string> SendMailAsync(MailContent mailContent);
    }
}
