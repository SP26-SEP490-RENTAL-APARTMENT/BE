using Common.Utils;

namespace Common.Tests;

public class EmailVerifierTests
{
    [Theory]
    [InlineData("user@example.com", true)]
    [InlineData("USER@example.com", true)]
    [InlineData("not-an-email", false)]
    [InlineData("user@", false)]
    public void IsValidFormat_returns_expected_result(string email, bool expected)
    {
        var result = EmailVerifier.IsValidFormat(email);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("user@mailinator.com", true)]
    [InlineData("user@sub.mailinator.com", true)]
    [InlineData("user@example.com", true)]
    [InlineData("user@company.com", false)]
    public void IsDisposableDomain_returns_expected_result(string email, bool expected)
    {
        var result = EmailVerifier.IsDisposableDomain(email);

        Assert.Equal(expected, result);
    }
}

public class PasswordHasherTests
{
    [Fact]
    public void HashPassword_produces_a_different_value_than_input()
    {
        const string password = "P@ssw0rd!";

        var hashed = PasswordHasher.HashPassword(password);

        Assert.False(string.IsNullOrWhiteSpace(hashed));
        Assert.NotEqual(password, hashed);
    }

    [Fact]
    public void VerifyPassword_returns_true_for_correct_password_and_false_for_wrong_password()
    {
        const string password = "P@ssw0rd!";

        var hashed = PasswordHasher.HashPassword(password);

        Assert.True(PasswordHasher.VerifyPassword(password, hashed));
        Assert.False(PasswordHasher.VerifyPassword("wrong", hashed));
    }
}
