using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using MiniTube.Services;

namespace MiniTube.Tests;

public class AdminClaimsTransformationTests
{
    // ─── Helper ─────────────────────────────────────────────────────────────
    //
    // Builds a mocked IConfiguration that returns the given admin email
    // when asked for the "AdminEmail" key, and null for everything else.
    // This is what mocking buys you: a one-line fake IConfiguration without
    // having to load real appsettings.json files.
    private static IConfiguration BuildConfigMock(string? adminEmail)
    {
        var mock = new Mock<IConfiguration>();
        mock.Setup(c => c["AdminEmail"]).Returns(adminEmail);
        return mock.Object;
    }

    // Builds a ClaimsPrincipal for an authenticated user with the given email.
    private static ClaimsPrincipal BuildAuthenticatedUser(string email)
    {
        var claims = new[] { new Claim(ClaimTypes.Email, email) };
        var identity = new ClaimsIdentity(claims, authenticationType: "Test");
        return new ClaimsPrincipal(identity);
    }

    // ─── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddsIsAdminClaim_WhenEmailMatches()
    {
        // Arrange — config says admin is "admin@example.com", user has that email
        var config = BuildConfigMock("admin@example.com");
        var transformation = new AdminClaimsTransformation(config);
        var user = BuildAuthenticatedUser("admin@example.com");

        // Act
        var result = await transformation.TransformAsync(user);

        // Assert — user now has the IsAdmin claim
        result.HasClaim("IsAdmin", "true").Should().BeTrue();
    }

    [Fact]
    public async Task DoesNotAddClaim_WhenEmailDoesNotMatch()
    {
        // Arrange — config says admin is "admin@example.com" but user is someone else
        var config = BuildConfigMock("admin@example.com");
        var transformation = new AdminClaimsTransformation(config);
        var user = BuildAuthenticatedUser("random@example.com");

        // Act
        var result = await transformation.TransformAsync(user);

        // Assert
        result.HasClaim("IsAdmin", "true").Should().BeFalse();
    }

    [Fact]
    public async Task DoesNotAddClaim_WhenUserIsNotAuthenticated()
    {
        // Arrange — anonymous principal (no identity)
        var config = BuildConfigMock("admin@example.com");
        var transformation = new AdminClaimsTransformation(config);
        var anonymous = new ClaimsPrincipal();

        // Act
        var result = await transformation.TransformAsync(anonymous);

        // Assert — anonymous users are never admins
        result.HasClaim("IsAdmin", "true").Should().BeFalse();
    }

    [Fact]
    public async Task MatchesEmailCaseInsensitively()
    {
        // Arrange — config email is lowercase, user's is uppercase
        var config = BuildConfigMock("admin@example.com");
        var transformation = new AdminClaimsTransformation(config);
        var user = BuildAuthenticatedUser("ADMIN@EXAMPLE.COM");

        // Act
        var result = await transformation.TransformAsync(user);

        // Assert — the comparison uses OrdinalIgnoreCase, so this still matches
        result.HasClaim("IsAdmin", "true").Should().BeTrue();
    }

    [Fact]
    public async Task DoesNotAddClaim_WhenAdminEmailConfigIsMissing()
    {
        // Arrange — config has no AdminEmail set (returns null)
        var config = BuildConfigMock(null);
        var transformation = new AdminClaimsTransformation(config);
        var user = BuildAuthenticatedUser("anyone@example.com");

        // Act
        var result = await transformation.TransformAsync(user);

        // Assert — nobody is admin if there's no admin configured
        result.HasClaim("IsAdmin", "true").Should().BeFalse();
    }
}
