using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _serviceId;
    private readonly IEnumerable<string> _headersToInclude;

    public RequestLoggingMiddleware(RequestDelegate next, string serviceId = "SeuServiceId", IEnumerable<string> headersToInclude = null)
    {
        _next = next;
        _serviceId = serviceId;
        _headersToInclude = headersToInclude ?? new List<string> { "Host", "User-Agent", "CorrelationId" }; // Headers padrão
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;

        // Obtém o timestamp atual em formato ISO 8601
        var timestamp = DateTime.UtcNow.ToString("o");

        // Obtém o CorrelationId do header ou gera um novo
        var correlationId = request.Headers.ContainsKey("CorrelationId")
            ? request.Headers["CorrelationId"].ToString()
            : Guid.NewGuid().ToString();

        // Lê os headers especificados da requisição
        var headers = new Dictionary<string, string>();
        foreach (var headerName in _headersToInclude)
        {
            if (request.Headers.TryGetValue(headerName, out var headerValue))
            {
                headers[headerName] = headerValue.ToString();
            }
        }

        // Lê o corpo da requisição, se existir
        string requestBody = string.Empty;
        if (request.ContentLength > 0 || request.Method == HttpMethods.Post || request.Method == HttpMethods.Put)
        {
            request.EnableBuffering();

            using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
            {
                requestBody = await reader.ReadToEndAsync();
                request.Body.Position = 0;
            }
        }

        // Monta o objeto de log da requisição
        var requestLog = new
        {
            ServiceId = _serviceId,
            LogType = "Request",
            HttpMethod = request.Method,
            Url = $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}",
            Headers = headers,
            Body = requestBody,
            Timestamp = timestamp,
            CorrelationId = correlationId
        };

        // Monta o objeto de log completo
        var logObject = new
        {
            Timestamp = timestamp,
            Level = "Information",
            MessageTemplate = "HTTP Request: {@RequestLog}",
            Properties = new
            {
                RequestLog = requestLog,
                CorrelationId = correlationId
            }
        };

        // Serializa o objeto de log em JSON
        var logJson = JsonSerializer.Serialize(logObject, new JsonSerializerOptions { WriteIndented = true });

        // Escreve o log no console
        Console.WriteLine(logJson);

        // Chama o próximo middleware no pipeline
        await _next(context);
    }
}
