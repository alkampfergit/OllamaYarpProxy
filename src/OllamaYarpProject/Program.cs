using OllamaYarpProject;
using System.Reflection;

// Set current directory to executable location
Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

var builder = WebApplication.CreateBuilder(args);

// Add custom configuration file if found
var configFile = ConfigurationUtilities.FindYarpOllamaConfig(Environment.CurrentDirectory);
if (!string.IsNullOrEmpty(configFile))
{
    builder.Configuration.AddJsonFile(configFile, optional: true, reloadOnChange: true);
    Console.WriteLine($"Using configuration file: {configFile}");
}

// Remove explicit logging configuration to allow appsettings.json to control logging
// builder.Logging.ClearProviders();
// builder.Logging.AddConsole();

builder.Services.AddSingleton<StandardTransform>();

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms<StandardTransform>();

var app = builder.Build();

// Optionally, keep a root endpoint
app.MapGet("/", () => "OllamaYarpProject Reverse Proxy is running.");

// Log all proxy requests and their redirections
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ProxyLogger");
    var originalPath = context.Request.Path + context.Request.QueryString;
    logger.LogInformation("Incoming request: {method} {path}", context.Request.Method, originalPath);

    // Capture the response before and after proxying
    await next();

    // If the request was proxied, YARP sets this feature
    var proxyFeature = context.Features.Get<Yarp.ReverseProxy.Forwarder.IForwarderErrorFeature>();
    if (proxyFeature != null)
    {
        logger.LogWarning("Proxy error: {error}", proxyFeature.Error);
    }
    else if (context.Items.TryGetValue("YarpDestination", out var destination))
    {
        logger.LogInformation("Request {path} was proxied to {destination}", originalPath, destination);
    }
    else
    {
        // Try to log the destination from YARP's context
        var dest = context.Request.Headers["X-Forwarded-Host"].ToString();
        if (!string.IsNullOrEmpty(dest))
        {
            logger.LogInformation("Request {path} was proxied to {destination}", originalPath, dest);
        }
    }
});

// Map the proxy endpoints with a callback to log the destination
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use(async (context, next) =>
    {
        // YARP will set the destination info in the cluster/destination features
        var destination = context.Request.Headers["X-Forwarded-Host"].ToString();
        if (!string.IsNullOrEmpty(destination))
        {
            context.Items["YarpDestination"] = destination;
        }
        await next();
    });
});

app.Run();
