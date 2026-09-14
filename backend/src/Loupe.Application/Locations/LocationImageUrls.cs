namespace Loupe.Application.Locations;

public static class LocationImageUrls
{
    public static string Image(Guid locationId, Guid imageId) => $"/api/locations/{locationId}/images/{imageId}";
    public static string Preview(Guid locationId, Guid imageId) => $"/api/locations/{locationId}/images/{imageId}/preview";
}
