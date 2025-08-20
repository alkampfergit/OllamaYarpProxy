using OllamaYarpProject;
using OllamaYarpProject.Models;
using OllamaYarpProject.Interfaces;
using OllamaYarpProject.Configuration;
using OllamaYarpProject.Services;
using OllamaYarpProject.Interceptors;
using OllamaYarpProject.Processors;
using Transform = OllamaYarpProject.Transform;
using System.Reflection;
using Serilog;
using Yarp.ReverseProxy.Configuration;

// Set current directory to executable location
Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

var builder = WebApplication.CreateBuilder(args);

// Add custom configuration file if found (before configuring Serilog)
var configFile = ConfigurationUtilities.FindYarpOllamaConfig(Environment.CurrentDirectory);
if (!string.IsNullOrEmpty(configFile))
{
    builder.Configuration.AddJsonFile(configFile, optional: true, reloadOnChange: true);
}

// Configure Serilog from configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Log configuration file discovery
var logger = Log.ForContext("SourceContext", "Startup");
if (!string.IsNullOrEmpty(configFile))
{
    logger.Information("Found configuration override file: {ConfigFile} in directory: {Directory}",
        configFile, Path.GetDirectoryName(configFile));
}
else
{
    logger.Debug("No configuration override file found, using default appsettings.json");
}

// Configure O3ProConfig and InterceptorConfiguration from configuration
builder.Services.Configure<O3ProConfig>(builder.Configuration.GetSection("O3ProConfig"));
builder.Services.Configure<InterceptorConfiguration>(builder.Configuration.GetSection("InterceptorConfiguration"));

// Remove explicit logging configuration to allow appsettings.json to control logging
// builder.Logging.ClearProviders();
// builder.Logging.AddConsole();

// Register abstraction services
builder.Services.AddSingleton<IDateTime, DateTimeWrapper>();
builder.Services.AddSingleton<IModelRouter, ModelRouter>();

// Register transform services
builder.Services.AddSingleton<StandardTransform>();
builder.Services.AddTransient<Transform.IPathTransformer, Transform.PathTransformer>();
builder.Services.AddTransient<Transform.IRequestTransformer, Transform.RequestTransformer>();
builder.Services.AddTransient<Transform.IResponseTransformer, Transform.ResponseTransformer>();
builder.Services.AddTransient<Transform.IStreamingResponseHandler, Transform.StreamingResponseHandler>();
builder.Services.AddTransient<Transform.IRequestResponseLogger, Transform.RequestResponseLogger>();

builder.Services.AddSingleton<O3ProClient>();

// Register new streaming response processing services
builder.Services.AddSingleton<ISseParser, SseParser>();
builder.Services.AddTransient<CitationStreamingProcessor>();
builder.Services.AddSingleton<IStreamingResponseProcessorFactory, StreamingResponseProcessorFactory>();

// Keep old services for backward compatibility during transition
builder.Services.AddTransient<ChunkManipulator>();
builder.Services.AddSingleton<IChunkManipulatorFactory, ChunkManipulatorFactory>();
builder.Services.AddTransient<CitationResponseInterceptor>();
builder.Services.AddSingleton<IResponseInterceptorFactory, ResponseInterceptorFactory>();

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms<StandardTransform>();

var app = builder.Build();

// Log YARP configuration at startup
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
var config = app.Services.GetRequiredService<IConfiguration>();

startupLogger.LogInformation("=== OllamaYarpProject Configuration ===");

// Log Kestrel endpoints
var kestrelSection = config.GetSection("Kestrel:Endpoints");
foreach (var endpoint in kestrelSection.GetChildren())
{
    var url = endpoint.GetValue<string>("Url");
    startupLogger.LogInformation("Listening on: {EndpointName} = {Url}", endpoint.Key, url);
}

// Log YARP configuration
var reverseProxySection = config.GetSection("ReverseProxy");
var clustersSection = reverseProxySection.GetSection("Clusters");
foreach (var cluster in clustersSection.GetChildren())
{
    startupLogger.LogInformation("YARP Cluster: {ClusterName}", cluster.Key);
    var destinations = cluster.GetSection("Destinations");
    foreach (var dest in destinations.GetChildren())
    {
        var address = dest.GetValue<string>("Address");
        startupLogger.LogInformation("  -> Destination {DestName}: {Address}", dest.Key, address);
    }
}

startupLogger.LogInformation("========================================");

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
