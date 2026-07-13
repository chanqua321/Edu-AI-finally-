using EduAI.BusinessLogic.IService;
using EduAI.Model.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EduAI.BusinessLogic.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public Task<bool> SendStudentAccountEmailAsync(
        string toEmail,
        string fullName,
        string userName,
        string temporaryPassword,
        string loginUrl)
    {
        var subject = "EduAI — Tài khoản học viên";
        var body = $"""
            <p>Xin chào <strong>{fullName}</strong>,</p>
            <p>Admin đã tạo tài khoản học viên EduAI cho bạn.</p>
            <ul>
              <li><strong>Email đăng nhập:</strong> {userName}</li>
              <li><strong>Mật khẩu tạm thời:</strong> {temporaryPassword}</li>
            </ul>
            <p><strong>Bước 1 — Đăng nhập lần đầu:</strong> <a href="{loginUrl}">{loginUrl}</a></p>
            <p><strong>Bước 2 — Đổi mật khẩu ngay sau khi đăng nhập (bắt buộc).</strong></p>
            <p>Nếu không phải bạn yêu cầu, hãy bỏ qua email này.</p>
            """;

        return SendHtmlEmailAsync(toEmail, subject, body);
    }

    public Task<bool> SendTeacherAccountEmailAsync(
        string toEmail,
        string fullName,
        string userName,
        string temporaryPassword,
        string loginUrl,
        string confirmationUrl)
    {
        var subject = "EduAI — Tài khoản giáo viên & xác thực email";
        var body = $"""
            <p>Xin chào <strong>{fullName}</strong>,</p>
            <p>Admin đã tạo tài khoản giáo viên EduAI cho bạn.</p>
            <ul>
              <li><strong>Email đăng nhập:</strong> {userName}</li>
              <li><strong>Mật khẩu tạm thời:</strong> {temporaryPassword}</li>
            </ul>
            <p><strong>Bước 1 — Xác thực email (bắt buộc):</strong></p>
            <p><a href="{confirmationUrl}">Bấm vào đây để xác thực email</a></p>
            <p>Sau khi xác thực, bạn mới có thể đăng nhập.</p>
            <p><strong>Bước 2 — Đăng nhập:</strong> <a href="{loginUrl}">{loginUrl}</a></p>
            <p><strong>Bước 3 — Đổi mật khẩu ngay sau khi đăng nhập (bắt buộc).</strong></p>
            <p>Nếu không phải bạn yêu cầu, hãy bỏ qua email này.</p>
            """;

        return SendHtmlEmailAsync(toEmail, subject, body);
    }

    public Task<bool> SendTeacherCredentialsEmailAsync(
        string toEmail,
        string fullName,
        string userName,
        string temporaryPassword,
        string loginUrl)
    {
        var subject = "EduAI — Thông tin đăng nhập giáo viên";
        var body = $"""
            <p>Xin chào <strong>{fullName}</strong>,</p>
            <p>Đây là thông tin đăng nhập mới cho tài khoản giáo viên EduAI của bạn.</p>
            <ul>
              <li><strong>Email đăng nhập:</strong> {userName}</li>
              <li><strong>Mật khẩu tạm thời:</strong> {temporaryPassword}</li>
            </ul>
            <p><strong>Bước 1 — Đăng nhập:</strong> <a href="{loginUrl}">{loginUrl}</a></p>
            <p><strong>Bước 2 — Đổi mật khẩu ngay sau khi đăng nhập (bắt buộc).</strong></p>
            <p>Nếu không phải bạn yêu cầu, hãy bỏ qua email này.</p>
            """;

        return SendHtmlEmailAsync(toEmail, subject, body);
    }

    public Task<bool> SendTeacherEmailConfirmationAsync(
        string toEmail,
        string fullName,
        string confirmationUrl,
        string loginUrl,
        string temporaryPassword)
    {
        var subject = "EduAI — Xác thực email giáo viên";
        var body = $"""
            <p>Xin chào <strong>{fullName}</strong>,</p>
            <p>Đây là mật khẩu tạm thời của bạn để đăng nhập:</p>
            <ul>
              <li><strong>Mật khẩu tạm thời:</strong> {temporaryPassword}</li>
            </ul>
            <p>Vui lòng xác thực email để kích hoạt tài khoản giáo viên EduAI.</p>
            <p><a href="{confirmationUrl}">Bấm vào đây để xác thực email</a></p>
            <p>Sau khi xác thực, đăng nhập tại: <a href="{loginUrl}">{loginUrl}</a></p>
            """;

        return SendHtmlEmailAsync(toEmail, subject, body);
    }

    private async Task<bool> SendHtmlEmailAsync(string toEmail, string subject, string htmlBody)
    {
        if (!_emailSettings.Enabled)
        {
            _logger.LogWarning("Email sending is disabled. Message to {Email} was not sent.", toEmail);
            return false;
        }

        if (string.IsNullOrWhiteSpace(_emailSettings.SmtpHost) ||
            string.IsNullOrWhiteSpace(_emailSettings.SenderEmail) ||
            string.IsNullOrWhiteSpace(_emailSettings.Username) ||
            string.IsNullOrWhiteSpace(_emailSettings.Password))
        {
            _logger.LogError("EmailSettings is incomplete. Configure SmtpHost, SenderEmail, Username and Password.");
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(
                _emailSettings.SmtpHost,
                _emailSettings.SmtpPort,
                _emailSettings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);
            await client.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {Email}: {Subject}", toEmail, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            return false;
        }
    }
}
