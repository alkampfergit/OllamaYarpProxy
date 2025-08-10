# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

OllamaYarpProxy is an ASP.NET Core reverse proxy that emulates Ollama's API endpoints while forwarding requests to a configurable backend server. It solves compatibility issues for tools like VS Code Copilot that expect Ollama's API format but need to work with different backend servers.

## Build and Run Commands

- **Build and run**: `dotnet run --project src/OllamaYarpProject`
- **Build only**: `dotnet build src/OllamaYarpProject`
- **Build from solution**: `dotnet build src/OllamaYarp.sln`
- **Run with specific profile**: `dotnet run --project src/OllamaYarpProject --launch-profile "http"`

The application listens on http://localhost:11434 by default and forwards requests to http://localhost:4000.

## Testing and Linting

- **No explicit test commands found** - this project doesn't appear to have unit tests configured yet
- **No linting commands found** - standard .NET analyzers are used via project configuration

## Architecture

### Core Components

- **Program.cs**: Main application entry point, configures YARP reverse proxy with logging middleware and request/response interceptors
- **StandardTransform.cs**: Custom YARP transform provider implementing ITransformProvider that handles:
  - Request path rewriting for API endpoint mapping
  - Response schema transformation between different API formats
  - Direct response generation for mock endpoints
- **ModelData.cs**: Data models for JSON serialization/deserialization between source API schema and Ollama schema

### Key Features

1. **API Endpoint Translation**: 
   - `/api/tags` → `/models` (with response schema transformation)
   - `/v1/chat/completions` → `/chat/completions` (with o3-pro model detection)
   - `/api/show` → returns mock model information directly
   - `/api/version` → returns static version info directly

2. **Response Schema Transformation**: 
   - Converts `/models` responses from source format (SourceRoot/ModelData) to Ollama format (OllamaRoot/OllamaModel)
   - Generates mock model details with fixed timestamps, sizes, and UUIDs

3. **Request Body Inspection**: 
   - Buffers and inspects request bodies for specific model names (e.g., "o3-pro")
   - Logs warnings for specific model requests

### Configuration

- **appsettings.json**: 
  - YARP configuration with single route and cluster
  - Kestrel server limits (MaxRequestBodySize: 2GB, extended timeouts)
  - Logging levels set to Debug for detailed request tracing
  - Backend target: `http://localhost:4000/`

- **launchSettings.json**: 
  - HTTP profile on port 11434
  - HTTPS profile on ports 7021/11434

### Data Flow

1. Client request arrives at proxy (port 11434)
2. Request middleware logs incoming request details
3. StandardTransform.Apply() configures request/response transforms
4. Request transform rewrites paths and inspects bodies
5. YARP forwards to destination server (port 4000)
6. Response transform modifies response content if needed
7. Response returned to client with proper schema

### Error Handling and Logging

- Comprehensive request/response logging with proxy destination tracking
- YARP error feature detection and logging
- JSON parsing error handling for malformed request bodies
- Debug-level logging for all proxy operations

## Dependencies

- **.NET 9.0**: Target framework with nullable reference types enabled
- **Yarp.ReverseProxy 2.3.0**: Core reverse proxy functionality  
- **Newtonsoft.Json 13.0.3**: JSON serialization/deserialization
- **Azure.AI.OpenAI 2.2.0-beta.5**: OpenAI integration (for O3ProClient)
- **OpenAI 2.3.0**: OpenAI API client library
- **Ollama 1.15.0**: Ollama client library

## Recent Changes and Current State

The codebase is currently on feature/custom-model branch with ongoing work on:
- Custom model integration (O3ProClient and related classes)
- Enhanced chat completion response building
- OpenAI model compatibility improvements

Modified files in current working state:
- `GenerateChatCompletionResponseBuilder.cs`: Chat completion response handling
- `OpenaiModel.cs`: OpenAI model definitions and mappings  
- `StandardTransform.cs`: Core transformation logic with null reference warnings