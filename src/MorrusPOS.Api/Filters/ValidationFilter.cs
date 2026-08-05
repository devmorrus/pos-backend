using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MorrusPOS.Api.Filters;

public class ValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            var errorMessage = string.Join(" ", errors);
            context.Result = new BadRequestObjectResult(new { error = errorMessage });
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
