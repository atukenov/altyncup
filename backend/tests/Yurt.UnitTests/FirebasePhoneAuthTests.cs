using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.NSubstitute;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Yurt.Application.Common.Interfaces;
using Yurt.Application.Features.Auth;
using Yurt.Application.Features.Auth.DTOs;
using Yurt.Application.Features.Auth.Services;
using Yurt.Application.Features.Auth.Validators;
using Yurt.Domain.Entities;
using Yurt.Domain.Enums;
using Yurt.Infrastructure.Firebase;

namespace Yurt.UnitTests;

/// <summary>
/// Otp__Provider=Firebase: app builds that can do Firebase Phone Auth get a 6-digit SMS flow
/// and prove the number with a Firebase ID token; everyone else keeps the 4-digit WhatsApp code.
/// </summary>
public class FirebasePhoneAuthTests
{
    private const string Mobile = "+77011234567";

    private sealed class Harness
    {
        public IApplicationDbContext Db { get; } = Substitute.For<IApplicationDbContext>();
        public IOtpSender Sender { get; } = Substitute.For<IOtpSender>();
        public IFirebasePhoneVerifier Firebase { get; } = Substitute.For<IFirebasePhoneVerifier>();
        public IPasswordHasher Hasher { get; } = Substitute.For<IPasswordHasher>();
        public ITokenService Tokens { get; } = Substitute.For<ITokenService>();
        public List<OtpEvent> Events { get; } = new();
        public List<CustomerUser> Customers { get; } = new();
        public AuthService Service { get; }

        public Harness(string provider = OtpOptions.FirebaseProvider, params PhoneVerification[] pending)
        {
            var customers = Customers.BuildMockDbSet();
            customers.When(s => s.Add(Arg.Any<CustomerUser>())).Do(c => Customers.Add(c.Arg<CustomerUser>()));
            var verifications = pending.ToList().BuildMockDbSet();
            var events = Events.BuildMockDbSet();
            events.When(s => s.Add(Arg.Any<OtpEvent>())).Do(c => Events.Add(c.Arg<OtpEvent>()));
            var refreshTokens = new List<RefreshToken>().BuildMockDbSet();
            Db.CustomerUsers.Returns(customers);
            Db.PhoneVerifications.Returns(verifications);
            Db.OtpEvents.Returns(events);
            Db.RefreshTokens.Returns(refreshTokens);
            Hasher.Hash(Arg.Any<string>()).Returns("hash");
            Sender.SendAsync(default!, default!, default).ReturnsForAnyArgs("MSG1");

            Service = new AuthService(Db, Tokens, Hasher, Sender,
                new OtpOptions { Enabled = true, Provider = provider }, Substitute.For<IAdminAlertSender>(),
                Firebase, NullLogger<AuthService>.Instance);
        }
    }

    private static readonly CustomerRegisterDto Register = new(Mobile, "1234", "A", "B");

    private static PhoneVerification Pending(VerificationPurpose purpose = VerificationPurpose.Registration) => new()
    {
        MobileNumber = Mobile, Purpose = purpose, CodeHash = "hash", PinHash = "pin",
        DeliveryStatus = AuthService.FirebaseDeliveryStatus, ExpiresAt = DateTime.UtcNow.AddMinutes(5),
    };

    // ── start ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Start_FirebaseClient_SendsNothing_AndAsksForSixDigits()
    {
        var h = new Harness();

        var result = await h.Service.StartRegistrationAsync(Register, clientSupportsFirebase: true);

        Assert.True(result.Succeeded);
        Assert.Equal("firebase", result.Data!.Provider);
        Assert.Equal(6, result.Data.CodeLength);
        Assert.Null(result.Data.DevCode);
        await h.Sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
        await h.Sender.DidNotReceiveWithAnyArgs().HasWhatsAppAsync(default!, default);
        var e = Assert.Single(h.Events);
        Assert.Equal(OtpEventType.CodeSent, e.Type);
        Assert.Contains("Firebase", e.Detail);
    }

    [Fact]
    public async Task Start_OldClient_StillGetsWhatsAppFourDigits()
    {
        var h = new Harness();

        var result = await h.Service.StartRegistrationAsync(Register, clientSupportsFirebase: false);

        Assert.Equal("whatsapp", result.Data!.Provider);
        Assert.Equal(4, result.Data.CodeLength);
        await h.Sender.ReceivedWithAnyArgs(1).SendAsync(default!, default!, default);
    }

    [Fact]
    public async Task Start_ProviderWhatsApp_IgnoresFirebaseClient()
    {
        var h = new Harness(OtpOptions.WhatsAppProvider);

        var result = await h.Service.StartRegistrationAsync(Register, clientSupportsFirebase: true);

        Assert.Equal("whatsapp", result.Data!.Provider);
        Assert.Equal(4, result.Data.CodeLength);
        await h.Sender.ReceivedWithAnyArgs(1).SendAsync(default!, default!, default);
    }

    [Fact]
    public async Task StartPinReset_UnregisteredNumber_LooksTheSameAsRegistered()
    {
        var h = new Harness();

        var result = await h.Service.StartPinResetAsync(new PinResetStartDto(Mobile), clientSupportsFirebase: true);

        Assert.True(result.Succeeded);
        Assert.Equal("firebase", result.Data!.Provider);
        Assert.Equal(6, result.Data.CodeLength);
    }

    [Fact]
    public async Task AdminResend_FirebaseRequest_IsRefused()
    {
        var pending = Pending();
        var h = new Harness(OtpOptions.FirebaseProvider, pending);

        var result = await h.Service.ResendCodeAsync(pending.Id);

        Assert.Equal(409, result.StatusCode);
        await h.Sender.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default);
    }

    // ── verify ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyRegistration_ValidFirebaseToken_CreatesVerifiedCustomer()
    {
        var h = new Harness(OtpOptions.FirebaseProvider, Pending());
        h.Firebase.VerifyAsync("tok", Arg.Any<CancellationToken>()).Returns(FirebasePhoneCheck.Valid(Mobile));

        var result = await h.Service.VerifyRegistrationAsync(new RegisterVerifyDto(Mobile, FirebaseIdToken: "tok"));

        Assert.True(result.Succeeded);
        var user = Assert.Single(h.Customers);
        Assert.True(user.IsPhoneVerified);
        Assert.Equal("pin", user.PinHash);
        var e = Assert.Single(h.Events);
        Assert.Equal(OtpEventType.Verified, e.Type);
    }

    [Fact]
    public async Task VerifyRegistration_TokenForAnotherNumber_IsRejected()
    {
        var pending = Pending();
        var h = new Harness(OtpOptions.FirebaseProvider, pending);
        h.Firebase.VerifyAsync("tok", Arg.Any<CancellationToken>()).Returns(FirebasePhoneCheck.Valid("+77019999999"));

        var result = await h.Service.VerifyRegistrationAsync(new RegisterVerifyDto(Mobile, FirebaseIdToken: "tok"));

        Assert.Equal(400, result.StatusCode);
        Assert.Empty(h.Customers);
        Assert.Equal(1, pending.Attempts);
        var e = Assert.Single(h.Events);
        Assert.Equal(OtpEventType.WrongCode, e.Type);
        Assert.Contains("+77019999999", e.Detail);
    }

    [Fact]
    public async Task VerifyRegistration_InvalidToken_IsRejectedWithReason()
    {
        var h = new Harness(OtpOptions.FirebaseProvider, Pending());
        h.Firebase.VerifyAsync("bad", Arg.Any<CancellationToken>())
            .Returns(FirebasePhoneCheck.Invalid("Firebase token rejected: ExpiredIdToken"));

        var result = await h.Service.VerifyRegistrationAsync(new RegisterVerifyDto(Mobile, FirebaseIdToken: "bad"));

        Assert.False(result.Succeeded);
        Assert.Empty(h.Customers);
        Assert.Contains("ExpiredIdToken", Assert.Single(h.Events).Detail);
    }

    [Fact]
    public async Task VerifyRegistration_FirebaseRequest_CannotBePassedWithAGuessedCode()
    {
        var h = new Harness(OtpOptions.FirebaseProvider, Pending());

        var result = await h.Service.VerifyRegistrationAsync(new RegisterVerifyDto(Mobile, "1234"));

        Assert.False(result.Succeeded);
        Assert.Empty(h.Customers);
    }

    [Fact]
    public async Task VerifyPinReset_ValidFirebaseToken_SetsNewPin()
    {
        var user = new CustomerUser { MobileNumber = Mobile, PinHash = "old", IsActive = true };
        var pending = Pending(VerificationPurpose.PinReset);
        pending.CustomerUserId = user.Id;
        var h = new Harness(OtpOptions.FirebaseProvider, pending);
        h.Customers.Add(user);
        h.Hasher.Hash("5678").Returns("newpin");
        h.Firebase.VerifyAsync("tok", Arg.Any<CancellationToken>()).Returns(FirebasePhoneCheck.Valid(Mobile));

        var result = await h.Service.VerifyPinResetAsync(new PinResetVerifyDto(Mobile, null, "5678", "tok"));

        Assert.True(result.Succeeded);
        Assert.Equal("newpin", user.PinHash);
    }

    // ── validators / token claims ────────────────────────────────────────────

    [Fact]
    public void Validator_AcceptsTokenWithoutCode_ButNeedsCodeOtherwise()
    {
        var v = new RegisterVerifyValidator();
        Assert.True(v.Validate(new RegisterVerifyDto(Mobile, FirebaseIdToken: "tok")).IsValid);
        Assert.False(v.Validate(new RegisterVerifyDto(Mobile)).IsValid);
        Assert.True(v.Validate(new RegisterVerifyDto(Mobile, "1234")).IsValid);
    }

    [Fact]
    public void ReadPhone_PhoneSignIn_ReturnsNumber()
    {
        var claims = new Dictionary<string, object>
        {
            ["phone_number"] = Mobile,
            ["firebase"] = JObject.Parse("{\"sign_in_provider\":\"phone\"}"),
        };

        Assert.Equal(Mobile, FirebasePhoneVerifier.ReadPhone(claims).PhoneNumber);
    }

    [Fact]
    public void ReadPhone_OtherSignInOrNoPhone_IsRejected()
    {
        var google = new Dictionary<string, object>
        {
            ["phone_number"] = Mobile,
            ["firebase"] = JObject.Parse("{\"sign_in_provider\":\"google.com\"}"),
        };
        Assert.False(FirebasePhoneVerifier.ReadPhone(google).Ok);
        Assert.False(FirebasePhoneVerifier.ReadPhone(new Dictionary<string, object>()).Ok);
    }
}
