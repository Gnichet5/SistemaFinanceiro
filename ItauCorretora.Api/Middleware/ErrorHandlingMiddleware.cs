using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ItauCorretora.Api.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro não tratado na API.");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            var response = new 
            { 
                erro = "Ocorreu um erro interno no servidor.",
                codigo = "ERRO_INTERNO",
                detalhe = exception.Message 
            };

            var payload = JsonSerializer.Serialize(response);
            return context.Response.WriteAsync(payload);
        }
    }
}