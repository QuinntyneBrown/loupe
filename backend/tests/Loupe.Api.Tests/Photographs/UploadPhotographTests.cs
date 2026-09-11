// Given a valid photograph, when uploaded, then it survives another API instance and its preview is readable only by its owner.
// L2-001.1/.3, L2-029 and L2-038.4.
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Loupe.Api.Tests.Security;
using Loupe.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class UploadPhotographTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task L2_001_1_3_Png_upload_persists_its_default_title_and_private_preview()
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<LibraryDbContext>().Database.MigrateAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var callback = await LocalSignInFlow.CompleteAsync(factory, client);
        using var session = await client.GetAsync("/api/session");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", session.Headers.GetValues("X-CSRF-Token").Single());
        client.DefaultRequestHeaders.Add("Origin", "https://localhost");
        using var image = Image.Black(8, 6, bands: 3);
        using var upload = new MultipartFormDataContent();
        var imagePart = new ByteArrayContent(image.PngsaveBuffer());
        imagePart.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        upload.Add(imagePart, "image", "Window light.png");
        using var response = await PhotographFixture.SubmitAsync(client, upload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var saved = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Window light", saved.RootElement.GetProperty("title").GetString());
        var previewUrl = saved.RootElement.GetProperty("previewUrl").GetString()!;
        using var preview = await client.GetAsync(previewUrl);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var decoded = Image.NewFromBuffer(await preview.Content.ReadAsByteArrayAsync());
        Assert.Equal(8, decoded.Width);
        Assert.Equal(6, decoded.Height);
        await using var second = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var later = second.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        using var login = await LocalSignInFlow.CompleteAsync(second, later);
        using var revisited = await later.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, revisited.StatusCode);
        using var retained = JsonDocument.Parse(await revisited.Content.ReadAsStringAsync());
        Assert.Equal(saved.RootElement.GetProperty("id").GetGuid(), retained.RootElement.GetProperty("id").GetGuid());
        using var retainedPreview = await later.GetAsync(previewUrl);
        Assert.Equal(await preview.Content.ReadAsByteArrayAsync(), await retainedPreview.Content.ReadAsByteArrayAsync());
        using var anonymous = second.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, BaseAddress = new Uri("https://localhost") });
        using var rejected = await anonymous.GetAsync(previewUrl);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.NotEqual("image/jpeg", rejected.Content.Headers.ContentType?.MediaType);
    }
}
