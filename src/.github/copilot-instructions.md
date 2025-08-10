This project is an ASP.NET Core reverse proxy that exposes the same endpoint as Ollama and redirects requests to another remote URL (default: localhost:4000).

The purpose is to simulate local ollama server but redirecting calls to litellm proxy that in turn redirect to other providers. 

Main usage is to help using all models inside GitHub Copilot in Visual Studio Code with zero friction.
