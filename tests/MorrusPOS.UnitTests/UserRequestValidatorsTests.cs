using System;
using FluentValidation.TestHelper;
using MorrusPOS.Application.Features.Auth;
using MorrusPOS.Application.Features.Auth.Validators;
using MorrusPOS.Application.Features.Users;
using MorrusPOS.Application.Features.Users.Validators;
using Xunit;

namespace MorrusPOS.UnitTests;

public class UserRequestValidatorsTests
{
    private readonly LoginRequestValidator _loginValidator = new();
    private readonly CreateUserRequestValidator _createUserValidator = new();
    private readonly UpdateUserRequestValidator _updateUserValidator = new();
    private readonly ChangePasswordRequestValidator _changePasswordValidator = new();

    [Theory]
    [InlineData("test@morruspos.com", "validPassword", true)]
    [InlineData("invalid-email", "validPassword", false)]
    [InlineData("", "validPassword", false)]
    [InlineData("test@morruspos.com", "", false)]
    public void LoginRequest_Validation_Test(string email, string password, bool expectedValid)
    {
        var request = new LoginRequest(email, password);
        var result = _loginValidator.TestValidate(request);

        if (expectedValid)
            result.ShouldNotHaveAnyValidationErrors();
        else
            result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void CreateUserRequest_ValidInput_Should_Pass()
    {
        var request = new CreateUserRequest(
            OutletId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            Name: "Ahmad Dhani",
            Email: "ahmad@morruspos.com",
            Password: "P@ssw0rd123!"
        );

        var result = _createUserValidator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("A")] // too short
    [InlineData("Ahmad123")] // contains numbers
    [InlineData("Ahmad@!")] // contains invalid symbol
    public void CreateUserRequest_InvalidNames_Should_Fail(string name)
    {
        var request = new CreateUserRequest(
            OutletId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            Name: name,
            Email: "ahmad@morruspos.com",
            Password: "P@ssw0rd123!"
        );

        var result = _createUserValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData("12345678")] // no letters
    [InlineData("password")] // no uppercase, no numbers, no symbols
    [InlineData("Password")] // no numbers, no symbols
    [InlineData("Password123")] // no symbols
    [InlineData("P@ssword")] // no numbers
    public void CreateUserRequest_WeakPasswords_Should_Fail(string password)
    {
        var request = new CreateUserRequest(
            OutletId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            Name: "Ahmad Dhani",
            Email: "ahmad@morruspos.com",
            Password: password
        );

        var result = _createUserValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void ChangePasswordRequest_SamePassword_Should_Fail()
    {
        var request = new ChangePasswordRequest(
            CurrentPassword: "P@ssw0rd123!",
            NewPassword: "P@ssw0rd123!"
        );

        var result = _changePasswordValidator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }
}
