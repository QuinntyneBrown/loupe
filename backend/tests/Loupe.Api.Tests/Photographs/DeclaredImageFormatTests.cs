// Given a supported still image with a generic MIME declaration or a valid HEIF
// file-type box, when uploaded, then its bytes determine support and its preview is readable.
using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NetVips;
using Xunit;

namespace Loupe.Api.Tests.Photographs;

public sealed class DeclaredImageFormatTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Theory]
    [InlineData("png", null)]
    [InlineData("jpeg", null)]
    [InlineData("webp", null)]
    [InlineData("heic", null)]
    [InlineData("png", "application/octet-stream")]
    [InlineData("jpeg", "application/octet-stream")]
    [InlineData("webp", "application/octet-stream")]
    [InlineData("heic", "application/octet-stream")]
    [InlineData("png", "image/png; charset=binary")]
    [InlineData("heic", "image/heic; codecs=\"hvc1.1.6.L93.B0\"")]
    public Task L2_001_1_Valid_images_accept_empty_generic_or_parameterized_declarations(string format, string? declaration) =>
        AssertAcceptedAsync(CreateImage(format), declaration);

    [Theory]
    [InlineData("image/heic")]
    [InlineData("image/heif")]
    public Task L2_001_1_Heic_compatible_brands_are_recognized_under_the_structural_heif_brand(string declaration)
    {
        var bytes = CreateImage("heic");
        var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes));
        var compatible = Enumerable.Range(0, (length - 16) / 4).Select(index => 16 + index * 4)
            .First(offset => bytes.AsSpan(offset, 4).SequenceEqual("mif1"u8));
        "heic"u8.CopyTo(bytes.AsSpan(compatible, 4));
        "mif1"u8.CopyTo(bytes.AsSpan(8, 4));
        return AssertAcceptedAsync(bytes, declaration);
    }

    [Fact]
    public Task L2_001_1_An_extended_size_heic_file_type_box_is_supported()
    {
        var bytes = CreateImage("heic");
        var length = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        Assert.True(length >= 24);
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 1);
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(8), length);
        "heic"u8.CopyTo(bytes.AsSpan(16, 4));
        bytes.AsSpan(20, 4).Clear();
        if (length >= 28) "mif1"u8.CopyTo(bytes.AsSpan(24, 4));
        return AssertAcceptedAsync(bytes, "image/heic", explicitHeif: true);
    }

    [Theory]
    [InlineData(false, "image/jpeg; charset=binary")]
    [InlineData(true, "application/octet-stream")]
    [InlineData(true, null)]
    public async Task L2_039_1_Generic_declarations_do_not_admit_unsupported_bytes_or_false_image_types(bool svg, string? declaration)
    {
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        var bytes = svg ? Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' width='8' height='6'/>") : CreateImage("png");
        using var upload = Upload(bytes, declaration);
        using var response = await PhotographFixture.SubmitAsync(client, upload);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/photographs");
        Assert.Empty(list.GetProperty("items").EnumerateArray());
    }

    private async Task AssertAcceptedAsync(byte[] bytes, string? declaration, bool explicitHeif = false)
    {
        using var fixture = explicitHeif ? Image.HeifloadBuffer(bytes) : Image.NewFromBuffer(bytes);
        Assert.Equal(8, fixture.Width);
        Assert.Equal(6, fixture.Height);
        await using var factory = new ApiFactory(database.ConnectionString, database.MediaRoot);
        using var client = await factory.CreateAuthenticatedClientAsync(Guid.NewGuid().ToString());
        using var upload = Upload(bytes, declaration);
        using var response = await PhotographFixture.SubmitAsync(client, upload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var photo = await response.Content.ReadFromJsonAsync<JsonElement>();
        using var preview = await client.GetAsync(photo.GetProperty("previewUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var decoded = Image.NewFromBuffer(await preview.Content.ReadAsByteArrayAsync());
        Assert.Equal(8, decoded.Width);
        Assert.Equal(6, decoded.Height);
    }

    private static MultipartFormDataContent Upload(byte[] bytes, string? declaration)
    {
        var upload = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        if (declaration is not null) part.Headers.ContentType = MediaTypeHeaderValue.Parse(declaration);
        upload.Add(part, "image", "Study");
        return upload;
    }

    private static byte[] CreateImage(string format)
    {
        using var image = Image.Black(8, 6, bands: 3);
        return format switch
        {
            "jpeg" => image.JpegsaveBuffer(),
            "webp" => image.WebpsaveBuffer(),
            "heic" => image.HeifsaveBuffer(compression: Enums.ForeignHeifCompression.Hevc),
            _ => image.PngsaveBuffer()
        };
    }
}
