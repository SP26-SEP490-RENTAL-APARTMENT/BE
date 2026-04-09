using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Implements
{
     public class EmailService
    {
        private readonly string _smtpHost = "smtp.gmail.com";
        private readonly int _smtpPort = 587;
        private readonly string _smtpUser = "khadnse180586@fpt.edu.vn";
        private readonly string _smtpPass = "psov xigy wdpp ffgr";
        private readonly string _fromEmail = "noreply@resetemail.com";
        private readonly bool _enableSsl = true;
        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                using (var client = new System.Net.Mail.SmtpClient(_smtpHost, _smtpPort))
                {
                    client.Credentials = new System.Net.NetworkCredential(_smtpUser, _smtpPass);
                    client.EnableSsl = _enableSsl;

                    var mailMessage = new System.Net.Mail.MailMessage
                    {
                        From = new System.Net.Mail.MailAddress(_fromEmail),
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true,
                    };
                    mailMessage.To.Add(toEmail);

                    await client.SendMailAsync(mailMessage);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error sending email: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}
