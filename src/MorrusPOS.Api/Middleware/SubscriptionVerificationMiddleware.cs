using System.Net;
using System.Text.Json;
using MorrusPOS.Application.Common.Interfaces;

namespace MorrusPOS.Api.Middleware;

public class SubscriptionVerificationMiddleware
{
    private readonly RequestDelegate _next;

    public SubscriptionVerificationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        if (currentUser.IsAuthenticated)
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";
            
            // Allow access to essential authentication, billing/management, or password reset routes
            var isBypass = path.StartsWith("/api/auth") || 
                           path.StartsWith("/api/billing") || 
                           path.Contains("change-password");

            if (!isBypass)
            {
                var subStatusClaim = context.User.FindFirst("subscription_status")?.Value;
                var trialEndDateClaim = context.User.FindFirst("trial_end_date")?.Value;

                var isTrialExpired = subStatusClaim == "Trial" && 
                                     DateTime.TryParse(trialEndDateClaim, out var trialEndDate) && 
                                     trialEndDate < DateTime.UtcNow;

                var isLocked = subStatusClaim == "Locked" || subStatusClaim == "Expired";

                if (isTrialExpired || isLocked)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.PaymentRequired; // 402 Payment Required
                    context.Response.ContentType = "application/json";

                    var errorPayload = new
                    {
                        code = "SUBSCRIPTION_EXPIRED",
                        message = "Masa trial/langganan bisnis Anda telah habis. Silakan lakukan pembayaran atau aktivasi ulang untuk melanjutkan.",
                        trialEndDate = DateTime.TryParse(trialEndDateClaim, out var ted) ? ted : (DateTime?)null,
                        subscriptionStatus = subStatusClaim
                    };

                    await context.Response.WriteAsync(JsonSerializer.Serialize(errorPayload));
                    return;
                }
            }
        }

        await _next(context);
    }
}
