using DentalLabOS.Application.Common;

namespace DentalLabOS.UnitTests;

public sealed class ValidatorTests
{
    [Fact]
    public void Collects_all_errors_then_throws_once()
    {
        var v = new Validator()
            .Required("Name", " ")
            .Email("Email", "not-an-email")
            .MaxLength("Code", new string('A', 20), 16)
            .Range("Padding", 20, 3, 10);

        var ex = Assert.Throws<ValidationException>(v.ThrowIfInvalid);
        Assert.Equal(["Validation.Required"], ex.Errors["Name"]);
        Assert.Equal(["Validation.Email"], ex.Errors["Email"]);
        Assert.Equal(["Validation.TooLong"], ex.Errors["Code"]);
        Assert.Equal(["Validation.Range"], ex.Errors["Padding"]);
    }

    [Fact]
    public void Valid_input_does_not_throw()
    {
        new Validator()
            .Required("Name", "معمل النور")
            .Email("Email", "owner@lab.test")
            .MaxLength("Code", "MAIN", 16)
            .Pattern("Code", "MAIN", "^[A-Z0-9-]+$", "Validation.Code")
            .ThrowIfInvalid();
    }

    [Fact]
    public void When_adds_custom_message()
    {
        var ex = Assert.Throws<ValidationException>(() => new Validator().When(true, "Code", "Validation.CodeTaken").ThrowIfInvalid());
        Assert.Equal("Validation.CodeTaken", ex.Errors["Code"].Single());
    }

    [Fact]
    public void Secure_tokens_are_random_and_hash_is_stable()
    {
        var a = SecureToken.Create();
        var b = SecureToken.Create();
        Assert.NotEqual(a, b);
        Assert.True(a.Length >= 40);
        Assert.Equal(SecureToken.Hash(a), SecureToken.Hash(a));
        Assert.NotEqual(a, SecureToken.Hash(a));
    }
}
