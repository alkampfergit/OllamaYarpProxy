# OllamaYarpProxy

OllamaYarpProxy is an ASP.NET Core reverse proxy that emulates Ollama's API endpoints and transparently forwards requests to a configurable backend (default: http://localhost:4000). It uses YARP (Yet Another Reverse Proxy) and custom transforms to rewrite paths and adapt responses, enabling compatibility with clients expecting Ollama's API.

## Features

- **Ollama API Compatibility:** Exposes endpoints like `/api/tags`, `/api/show`, `/api/version`, and `/v1/chat/completions`, rewriting and transforming requests/responses as needed.
- **Configurable Backend:** Forwards requests to a backend server, configurable via external configuration files.
- **Configuration Discovery:** Automatically finds configuration override files in current or parent directories.
- **Response Interceptors:** Configurable streaming response processors for model-specific transformations (e.g., citation extraction).
- **Custom Transforms:** Uses YARP's transform pipeline to rewrite paths and adapt JSON schemas for Ollama compatibility.
- **Logging:** Logs incoming requests, proxy destinations, and errors for easier debugging.
- **Solves [vscode-copilot-release#7518](https://github.com/microsoft/vscode-copilot-release/issues/7518#issuecomment-3051433965):** Enables Copilot and similar tools to interact with Ollama-compatible endpoints even when the backend differs.

## How to Run

1. **Build and run the project:**
   ```pwsh
   dotnet run --project src/OllamaYarpProject
   ```
2. **Access the proxy:**  
   By default, it listens on `http://localhost:11434` and forwards requests to `http://localhost:4000`.

3. **Configure (Optional):**  
   Create an `ollama-yarp-proxy.json` file in the same directory or a parent directory to customize settings:
   ```bash
   # Example: Change backend to localhost:8080
   echo '{
     "ReverseProxy": {
       "Clusters": {
         "ollamaCluster": {
           "Destinations": {
             "destination1": {
               "Address": "http://localhost:8080/"
             }
           }
         }
       }
     }
   }' > ollama-yarp-proxy.json
   ```

## Configuration

### Configuration Override Files

**⚠️ Important:** Do not modify the built-in `appsettings.json` file. Instead, create a configuration override file in the current directory or any parent directory. The proxy will automatically discover and use it:

**Benefits of external configuration:**
- ✅ Preserves original settings
- ✅ Easy to version control your specific setup
- ✅ Portable across different environments
- ✅ No conflicts when updating the proxy

**Preferred method** - Create `ollama-yarp-proxy.json`:
```json
{
  "ReverseProxy": {
    "Clusters": {
      "ollamaCluster": {
        "Destinations": {
          "destination1": {
            "Address": "http://localhost:4000/"
          }
        }
      }
    }
  },
  "InterceptorConfiguration": {
    "ModelInterceptorMappings": {
      "gpt-4": "CitationInterceptor",
      "gpt-3.5-turbo": "CitationInterceptor"
    }
  }
}
```

**Legacy method** - Create `yarpollama.json` with the same format.

### Configuration Discovery

The proxy searches for configuration files in this order:
1. Current working directory
2. Parent directories (recursively up the folder tree)
3. Files checked: `ollama-yarp-proxy.json` (preferred), then `yarpollama*.json` (legacy)

### Available Configuration Options

- **Backend URL:** Configure the target server in the `ReverseProxy.Clusters.ollamaCluster.Destinations.destination1.Address` field
- **Response Interceptors:** Map specific AI models to response interceptors via `InterceptorConfiguration.ModelInterceptorMappings`
- **Logging:** Override logging levels and outputs (same format as `appsettings.json`)

### Response Interceptors

The proxy supports configurable response interceptors for streaming responses:

- **CitationInterceptor:** Processes streaming responses and extracts/formats citations from AI models
- **Model Mapping:** Configure which models use which interceptors in the `InterceptorConfiguration` section

Example: Map the "gpt-4" model to use citation processing:
```json
{
  "InterceptorConfiguration": {
    "ModelInterceptorMappings": {
      "gpt-4": "CitationInterceptor"
    }
  }
}
```

## Endpoints

- `/api/tags` → `/models` (rewritten and response schema adapted)
- `/api/show` → Returns model info in Ollama format
- `/api/version` → Returns Ollama-compatible version info
- `/v1/chat/completions` → `/chat/completions` (rewritten)
- All other endpoints are proxied as-is

## Why?

This proxy allows tools (like Copilot) that expect Ollama's API to work with alternative backends, solving integration issues such as [vscode-copilot-release#7518](https://github.com/microsoft/vscode-copilot-release/issues/7518#issuecomment-3051433965).

---
