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

- **Run all tests**: `dotnet test src/OllamaYarp.sln`
- **Run tests from specific project**: `dotnet test src/OllamaYarpProject.Tests`
- **Run single test**: `dotnet test --filter "FullyQualifiedName=Namespace.ClassName.TestMethodName"`
- **Run tests with coverage**: `dotnet test --collect:"XPlat Code Coverage"`
- **No linting commands found** - standard .NET analyzers are used via project configuration

## Architecture

### Core Components

- **Program.cs**: Main application entry point, configures YARP reverse proxy with logging middleware and request/response interceptors
- **StandardTransform.cs**: Custom YARP transform provider implementing ITransformProvider that handles:
  - Request path rewriting for API endpoint mapping
  - Response schema transformation between different API formats
  - Direct response generation for mock endpoints
- **ModelData.cs**: Data models for JSON serialization/deserialization between source API schema and Ollama schema
- **ChunkManipulator.cs**: Streaming response processor that handles:
  - SSE (Server-Sent Events) data chunk parsing and transformation
  - Citation extraction and processing from streaming responses
  - Content accumulation and formatting with carriage return detection
- **Models/O3ProClient.cs**: Azure OpenAI O3 Pro model client with support for both Azure and OpenAI endpoints
- **Models/OpenaiModel.cs**: Generic OpenAI model client wrapper using the OpenAI .NET SDK
- **Helpers/GenerateChatCompletionResponseBuilder.cs**: Builder pattern for Ollama chat completion responses

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

4. **Streaming Response Processing**:
   - Real-time SSE chunk manipulation with citation extraction
   - Content buffering with carriage return detection for proper formatting
   - Accumulated citation rendering with markdown link formatting

5. **Multi-Model Client Support**:
   - O3ProClient for Azure OpenAI O3 Pro reasoning model with dual endpoint support
   - OpenaiModel for generic OpenAI models via official SDK
   - IModel interface for extensible model client architecture

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
4. Request transform rewrites paths and inspects bodies for special model detection
5. YARP forwards to destination server (port 4000) or routes to direct model clients
6. For streaming responses: ChunkManipulator processes SSE chunks in real-time
7. Response transform modifies content schema and formats citations if needed
8. Response returned to client with proper Ollama-compatible schema

### Error Handling and Logging

- Comprehensive request/response logging with proxy destination tracking
- YARP error feature detection and logging
- JSON parsing error handling for malformed request bodies and streaming chunks
- ChunkManipulator graceful degradation for unparseable SSE data
- Debug-level logging for all proxy operations and chunk processing
- Model client error handling with detailed logging for authentication and API failures

## Dependencies

- **.NET 9.0**: Target framework with nullable reference types enabled
- **Yarp.ReverseProxy 2.3.0**: Core reverse proxy functionality  
- **Newtonsoft.Json 13.0.3**: JSON serialization/deserialization
- **Azure.AI.OpenAI 2.2.0-beta.5**: OpenAI integration (for O3ProClient)
- **OpenAI 2.3.0**: OpenAI API client library for generic model support
- **Ollama 1.15.0**: Ollama client library for response type definitions
- **Serilog.AspNetCore 8.0.3**: Structured logging framework
- **xUnit 2.9.2**: Testing framework (in test project)

## Recent Changes and Current State

The codebase is currently on feature/refactor-interception branch with major architectural improvements:
- **ChunkManipulator refactor**: Complete rewrite of streaming response processing with improved citation handling
- **Multi-model client architecture**: Added IModel interface with O3ProClient and OpenaiModel implementations
- **Enhanced streaming**: Real-time SSE chunk processing with citation extraction and formatting
- **Test infrastructure**: Added OllamaYarpProject.Tests project with xUnit framework

## Key Interfaces and Patterns

- **IModel interface**: Common interface for different AI model clients (O3ProClient, OpenaiModel)
- **IChunkManipulator interface**: Abstraction for streaming response processing with factory pattern
- **YARP ITransformProvider**: Custom transforms in StandardTransform.cs for request/response modification
- **Dependency Injection**: Heavy use of DI for service registration and configuration management
- **Builder Pattern**: GenerateChatCompletionResponseBuilder for structured response creation