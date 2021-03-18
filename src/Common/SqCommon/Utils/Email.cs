using System.Net.Mail;
using System.Net;
using System;
using System.Threading.Tasks;

namespace SqCommon
{
    public class Email
    {
        public string ToAddresses = string.Empty;
        public string Subject = string.Empty;
        public string Body  = string.Empty;
        public bool IsBodyHtml;

        public static string SenderName  = string.Empty;
        public static string SenderPwd  = string.Empty;

        // 2017-06: there was a rumour for a couple of months that Mailkit replaces System.Net.Mail.SmtpClient https://www.infoq.com/news/2017/04/MailKit-MimeKit-Official
        // https://dotnetcoretutorials.com/2017/08/20/sending-email-net-core-2-0/#comments
        // Apparently so, someone made an update to the documentation to remove the obsolete tag : https://github.com/dotnet/docs/commit/7ef4e82ea518a756b1a0d1d1684dde15653845aa#diff-aac793a10e9e8d7daa604332765b29db
        // But "The deprecation was an accidental side-effect of auto-generated docs. The .NET class is no longer marked deprecated in the documentation.
        // https://github.com/dotnet/docs/issues/1876"
        // OK. It was always good. So, continue using System.Net.Mail.SmtpClient as this is the standard. People will fix the bugs in it in the future.
        // I can do my async version if it is needed, no problem. That is no reason to use the 3rd party MailKit
        public void Send()
        {
            // https://stackoverflow.com/questions/32260/sending-email-in-net-through-gmail
            using (var message = new MailMessage()
            {
                Subject = Subject,
                Body = Body,
                IsBodyHtml = IsBodyHtml
            })
            {
                message.From = new MailAddress(SenderName);
                var toAddresses = ToAddresses.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < toAddresses.Length; i++)
                {
                    message.To.Add(toAddresses[i]);
                }

                using (var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(SenderName, SenderPwd),
                    Timeout = 10000 // in msec, so we use 10 sec timeout
                })
                {
                    Utils.Logger.Info("Email.Send() START");
                    smtp.Send(message);
                    Utils.Logger.Info("Email.Send() ENDS");
                }
            }
        }

        // https://stackoverflow.com/questions/7276375/what-are-best-practices-for-using-smtpclient-sendasync-and-dispose-under-net-4
        // Warning	CS4014 can be supressed if it returns void instead of Task, however as it is a util function, it shouldn't be fire_forget. The caller should handle it.
        // https://stackoverflow.com/questions/22629951/suppressing-warning-cs4014-because-this-call-is-not-awaited-execution-of-the
        public async Task SendAsync()
        {
            // https://stackoverflow.com/questions/32260/sending-email-in-net-through-gmail
            using (var message = new MailMessage()
            {
                Subject = Subject,
                Body = Body,
                IsBodyHtml = IsBodyHtml
            })
            {
                message.From = new MailAddress(SenderName);
                var toAddresses = ToAddresses.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < toAddresses.Length; i++)
                {
                    message.To.Add(toAddresses[i]);
                }

                using (var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(SenderName, SenderPwd),
                    Timeout = 10000 // in msec, so we use 10 sec timeout
                })
                {
                    Utils.Logger.Info("Email.Send() START");    // this is running in the calling thread (maybe the Main Thread)
                    await smtp.SendMailAsync(message);          // Caller (Main) thread returns here
                    Utils.Logger.Info("Email.Send() ENDS");     // this is running a separate worker thread.
                }
            }
        }
    }
}
