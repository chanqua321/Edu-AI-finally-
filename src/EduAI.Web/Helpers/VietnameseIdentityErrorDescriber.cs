using Microsoft.AspNetCore.Identity;

namespace EduAI.Web.Helpers;

public sealed class VietnameseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) =>
        new()
        {
            Code = nameof(PasswordTooShort),
            Description = $"Mật khẩu phải có ít nhất {length} ký tự."
        };

    public override IdentityError PasswordRequiresLower() =>
        new()
        {
            Code = nameof(PasswordRequiresLower),
            Description = "Mật khẩu phải có ít nhất 1 chữ thường (a-z)."
        };

    public override IdentityError PasswordRequiresUpper() =>
        new()
        {
            Code = nameof(PasswordRequiresUpper),
            Description = "Mật khẩu phải có ít nhất 1 chữ hoa (A-Z)."
        };

    public override IdentityError PasswordRequiresDigit() =>
        new()
        {
            Code = nameof(PasswordRequiresDigit),
            Description = "Mật khẩu phải có ít nhất 1 chữ số (0-9)."
        };

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new()
        {
            Code = nameof(PasswordRequiresNonAlphanumeric),
            Description = "Mật khẩu phải có ít nhất 1 ký tự đặc biệt."
        };

    public override IdentityError InvalidToken() =>
        new()
        {
            Code = nameof(InvalidToken),
            Description = "Token không hợp lệ hoặc đã hết hạn."
        };

    public override IdentityError DuplicateEmail(string email) =>
        new()
        {
            Code = nameof(DuplicateEmail),
            Description = $"Email '{email}' đã được sử dụng."
        };

    public override IdentityError DuplicateUserName(string userName) =>
        new()
        {
            Code = nameof(DuplicateUserName),
            Description = $"Tên đăng nhập '{userName}' đã được sử dụng."
        };
}

