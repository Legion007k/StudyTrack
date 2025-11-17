using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

public class EmailSettings
{
    public string Host { get; set; }
    public int Port { get; set; }
    public bool EnableSSL { get; set; }
    public string User { get; set; }
    public string Password { get; set; }
}

public interface IEmailService
{
    Task SendEmail(string to, string subject, string body);
}

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendEmail(string to, string subject, string body)
    {
        using var smtp = new SmtpClient(_settings.Host)
        {
            Port = _settings.Port,
            EnableSsl = true, //  FORZADO (SIEMPRE TRUE)
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_settings.User, _settings.Password)
        };

        var mail = new MailMessage
        {
            From = new MailAddress(_settings.User),
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };

        mail.To.Add(to);
        await smtp.SendMailAsync(mail);
    }

}
