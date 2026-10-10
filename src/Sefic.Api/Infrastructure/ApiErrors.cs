using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Sefic.Application.Common;

namespace Sefic.Api.Infrastructure;

/// <summary>Formato único de erro (ProblemDetails) sem stack traces.</summary>
public static class ApiErrors
{
    public static ProblemDetails Create(int status, string title, string detail,
        IDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        if (errors is not null) problem.Extensions["errors"] = errors;
        return problem;
    }
}

public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var problem = exception switch
        {
            ValidationException v => ApiErrors.Create(400, "Pedido inválido", v.Message,
                v.Errors.ToDictionary(e => e.Key, e => e.Value)),
            NotFoundException n => ApiErrors.Create(404, "Não encontrado", n.Message),
            ConflictException c => ApiErrors.Create(409, "Conflito", c.Message),
            BadHttpRequestException b => ApiErrors.Create(400, "Pedido inválido", "O pedido está malformado."),
            _ => null
        };

        if (problem is null)
        {
            logger.LogError(exception, "Erro não tratado");
            problem = ApiErrors.Create(500, "Erro interno", "Ocorreu um erro inesperado.");
        }

        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", ct);
        return true;
    }
}
