using System.Text.Json;

public static class CustomLogger
{
    public static void Log(string message, string logType, string level = "Information", string correlationId = null)
    {
        var logObject = new
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            Level = level,
            MessageTemplate = message,
            Properties = new
            {
                LogType = logType,
                CorrelationId = correlationId ?? Guid.NewGuid().ToString()
            }
        };

        var logJson = JsonSerializer.Serialize(logObject, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(logJson);
    }

    public static void LogSuccess(string message, string correlationId = null)
    {
        Log(message, "Success", "Information", correlationId);
    }

    public static void LogError(string message, string correlationId = null)
    {
        Log(message, "Error", "Error", correlationId);
    }
}
