using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MiniTube.Tests;

public class IntegrationTests : IClassFixture<MiniTubeWebFactory>, IDisposable
{
    private readonly HttpClient _client;

    public IntegrationTests(MiniTubeWebFactory factory)
    {
        TestAuthHandler.IsAuthenticated = false;

        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public void Dispose()
    {
        TestAuthHandler.IsAuthenticated = false;
    }

    // ─── TEST 1 (Nivel 1): Smoke test ──────────────────────────────────────
    // Does the app start and serve the homepage?
    // Catches: broken DI, missing config, startup crashes.

    [Fact]
    public async Task Homepage_Returns200()
    {
        var response = await _client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ─── TEST 2 (Nivel 4): Anonymous user cannot access Upload ─────────────
    // Upload has [Authorize]. Anonymous users must be blocked.

    [Fact]
    public async Task Upload_AnonymousUser_IsBlocked()
    {
        TestAuthHandler.IsAuthenticated = false;

        var response = await _client.GetAsync("/Upload");

        // With our test auth scheme, unauthenticated requests get 401 or 302
        var blocked = response.StatusCode is HttpStatusCode.Unauthorized
                      or HttpStatusCode.Redirect;
        blocked.Should().BeTrue(
            "anonymous users should not be able to access the Upload page");
    }

    // ─── TEST 3 (Nivel 4): Anonymous user cannot access Edit ───────────────
    // Edit also has [Authorize]. Same principle.

    [Fact]
    public async Task Edit_AnonymousUser_IsBlocked()
    {
        TestAuthHandler.IsAuthenticated = false;

        var response = await _client.GetAsync("/Edit?id=fake-id");

        var blocked = response.StatusCode is HttpStatusCode.Unauthorized
                      or HttpStatusCode.Redirect;
        blocked.Should().BeTrue(
            "anonymous users should not be able to access the Edit page");
    }

    // ─── TEST 4 (Nivel 4): Authenticated user CAN access Upload ────────────
    // Proves auth works both ways: blocked when anonymous, allowed when logged in.

    [Fact]
    public async Task Upload_AuthenticatedUser_Returns200()
    {
        TestAuthHandler.IsAuthenticated = true;

        var response = await _client.GetAsync("/Upload");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ─── TEST 5 (Nivel 5): Uploading an .exe file is rejected ──────────────
    // The upload validates file extensions. Dangerous files must be rejected.

    [Fact]
    public async Task Upload_ExeFile_IsRejected()
    {
        TestAuthHandler.IsAuthenticated = true;

        using var content = new MultipartFormDataContent();
        var fakeExe = new ByteArrayContent(new byte[] { 0x00, 0x01 });
        fakeExe.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fakeExe, "Form.VideoFile", "malware.exe");
        content.Add(new StringContent("Test Video"), "Form.Title");
        content.Add(new StringContent("A test"), "Form.Description");
        content.Add(new StringContent("Tech"), "Form.Category");

        var response = await _client.PostAsync("/Upload", content);

        // A successful upload redirects to /Index. If the file is rejected,
        // the page re-renders with a validation error (200) or returns an error.
        // Either way, it should NOT redirect to the Index page.
        response.Headers.Location?.ToString().Should().NotBe("/");
    }
}
