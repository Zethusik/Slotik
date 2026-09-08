using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;



namespace Slotik.Services
{
    public class EmailService
    {
        private readonly SmtpSettings _settings;

        public EmailService(IOptions<SmtpSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendConfirmationEmailAsync(string email, string confLink) 
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));

            message.To.Add(MailboxAddress.Parse(email));

            message.Subject = "Email Confirmation - Slotik";

            message.Body = new TextPart("html")
            {
                Text = $"""
                    <h2>Welcome to Slotik!</h2>

                    <p>
                        Confirm your Email to continue registration
                    </p>

                    <p>
                        <a href="{confLink}">
                            Confirm email
                        </a>
                    </p>

                    <p>
                        Link Expires after 30 minutes
                    </p>

                    <p>
                        if you dont know why did you got this message - just ignore it.
                    </p>
                    """
            };

            using var client = new SmtpClient();

            client.CheckCertificateRevocation = false;

            await client.ConnectAsync(
                _settings.Host,
                _settings.Port,
                SecureSocketOptions.StartTls);

            await client.AuthenticateAsync(_settings.Username, _settings.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task SendConfirmationCodeAsync(string email, string codeLink)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));

            message.To.Add(MailboxAddress.Parse(email));

            message.Subject = "Password Reset - Slotik";

            message.Body = new TextPart("html")
            {
                Text = $"""
                    <h2>Welcome to Slotik!</h2>

                    <p>
                        Reset your password by clicking on the link.
                    </p>

                    <p>
                        <a href="{codeLink}">
                            Reset Password
                        </a>
                    </p>

                    <p>
                        Link Expires after 30 minutes
                    </p>

                    <p>
                        if you dont know why did you got this message - just ignore it.
                    </p>
                    """
            };

            using var client = new SmtpClient();

            client.CheckCertificateRevocation = false;

            await client.ConnectAsync(
                _settings.Host,
                _settings.Port,
                SecureSocketOptions.StartTls);

            await client.AuthenticateAsync(_settings.Username, _settings.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public string HashToken(string token)
        {
            var bytes = SHA256.HashData(
                Encoding.UTF8.GetBytes(token));

            return Convert.ToHexString(bytes);
        }

        public string GenerateEmailToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);

            return WebEncoders.Base64UrlEncode(bytes);
        }


    }
}
