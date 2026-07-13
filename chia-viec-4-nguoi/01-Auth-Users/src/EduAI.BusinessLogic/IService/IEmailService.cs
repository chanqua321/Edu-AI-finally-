namespace EduAI.BusinessLogic.IService;

public interface IEmailService
{
    Task<bool> SendStudentAccountEmailAsync(
        string toEmail,
        string fullName,
        string userName,
        string temporaryPassword,
        string loginUrl);

    Task<bool> SendTeacherAccountEmailAsync(
        string toEmail,
        string fullName,
        string userName,
        string temporaryPassword,
        string loginUrl,
        string confirmationUrl);

    Task<bool> SendTeacherEmailConfirmationAsync(
        string toEmail,
        string fullName,
        string confirmationUrl,
        string loginUrl,
        string temporaryPassword);

    Task<bool> SendTeacherCredentialsEmailAsync(
        string toEmail,
        string fullName,
        string userName,
        string temporaryPassword,
        string loginUrl);
}
