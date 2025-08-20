using System;

namespace OllamaYarpProject.Interfaces;

public interface IDateTime
{
    DateTime UtcNow { get; }
    DateTime Now { get; }
}

public class DateTimeWrapper : IDateTime
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime Now => DateTime.Now;
}