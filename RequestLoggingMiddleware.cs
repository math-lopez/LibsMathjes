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
        _headersToInclude = headersToInclude ?? new List<string>(); // Nenhum header por padrão
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;

        // Obtém o CorrelationId do header ou gera um novo
        var correlationId = request.Headers.ContainsKey("CorrelationId")
            ? request.Headers["CorrelationId"].ToString()
            : Guid.NewGuid().ToString();

        // Adiciona o CorrelationId ao header da resposta
        context.Response.Headers["CorrelationId"] = correlationId;

        // Captura o timestamp antes de chamar o próximo middleware
        var timestamp = DateTime.UtcNow.ToString("o");

        // Cria um stream temporário para armazenar a resposta
        var originalBodyStream = context.Response.Body;
        using (var responseBody = new MemoryStream())
        {
            context.Response.Body = responseBody;

            try
            {
                // Chama o próximo middleware (pipeline)
                await _next(context);
            }
            catch (Exception)
            {
                // Define o status code como 500 em caso de erro
                context.Response.StatusCode = 500;
                // Opcional: Você pode adicionar lógica adicional aqui
            }

            // Agora que a resposta foi gerada, podemos acessar o status code
            var statusCode = context.Response.StatusCode;

            // Determina o LogType com base no status code
            string logType = statusCode >= 200 && statusCode < 400 ? "Success" : "Error";

            // Lê o corpo da resposta
            context.Response.Body.Seek(0, SeekOrigin.Begin);
            var responseBodyContent = await new StreamReader(context.Response.Body).ReadToEndAsync();
            context.Response.Body.Seek(0, SeekOrigin.Begin);

            // Lê os headers especificados da resposta
            var headers = new Dictionary<string, string>();
            foreach (var headerName in _headersToInclude)
            {
                if (context.Response.Headers.TryGetValue(headerName, out var headerValue))
                {
                    headers[headerName] = headerValue.ToString();
                }
            }

            // Monta o objeto de log da resposta
            var responseLog = new
            {
                ServiceId = _serviceId,
                LogType = logType,
                StatusCode = statusCode,
                Headers = headers,
                Body = responseBodyContent,
                Timestamp = DateTime.UtcNow.ToString("o"),
                CorrelationId = correlationId
            };

            // Monta o objeto de log completo
            var logObject = new
            {
                Timestamp = timestamp,
                Level = "Information",
                MessageTemplate = "HTTP Response: {@ResponseLog}",
                Properties = new
                {
                    ResponseLog = responseLog,
                    CorrelationId = correlationId
                }
            };

            // Serializa o objeto de log em JSON
            var logJson = JsonSerializer.Serialize(logObject, new JsonSerializerOptions { WriteIndented = true });

            // Escreve o log no console
            Console.WriteLine(logJson);

            // Copia o conteúdo da resposta de volta para o stream original
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }
}
