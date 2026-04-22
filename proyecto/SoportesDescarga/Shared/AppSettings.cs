namespace SoportesDescarga.Shared;

public class AppSettings
{
    public PathsSettings Paths { get; set; } = new();
    public LoginSettings Login { get; set; } = new();
    public ApiSettings Api { get; set; } = new();
    public ConcurrencySettings Concurrency { get; set; } = new();
}

public class PathsSettings
{
    public string Input { get; set; } = "input/entrada.txt";
    public string Output { get; set; } = "wwwroot/descargados";
}

public class LoginSettings
{
    public string Url { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class ApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string BearerToken { get; set; } = string.Empty;
}

public class ConcurrencySettings
{
    public int MaxParallelDownloads { get; set; } = 3;
}
