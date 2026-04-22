namespace SoportesDescarga.Shared;

public static class ConfigHelper
{
    public static string GetIntranetBaseUrl(string loginUrl)
    {
        return new Uri(loginUrl).GetLeftPart(UriPartial.Authority);
    }
}
