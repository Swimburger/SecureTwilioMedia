using System.Net;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Twilio.AspNet.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTwilioRequestValidation();

var app = builder.Build();

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/twilio", StringComparison.OrdinalIgnoreCase),
    app => app.UseTwilioRequestValidation()
);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "TwilioMedia")),
    RequestPath = "/twilio/media"
});

app.MapPost("/twilio/message", () => Results.Text("Ahoy!"));

app.Run();

public class ValidateTwilioRequestMiddleware
{
    private readonly RequestDelegate next;
    private readonly TwilioRequestValidationOptions options;

    public ValidateTwilioRequestMiddleware(
        RequestDelegate next, 
        IOptions<TwilioRequestValidationOptions> options
    )
    {
        this.next = next;
        this.options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        
        string? urlOverride = null;
        if (options.BaseUrlOverride != null)
        {
            urlOverride = $"{options.BaseUrlOverride.TrimEnd('/')}{request.Path}{request.QueryString}";
        }

        var validator = new RequestValidationHelper();
        var isValid = validator.IsValidRequest(context, options.AuthToken, urlOverride, options.AllowLocal ?? true);
        if (!isValid)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return;
        }
        
        await next(context);
    }
}

public static class ValidateTwilioRequestMiddlewareExtensions
{
    public static IApplicationBuilder UseTwilioRequestValidation(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ValidateTwilioRequestMiddleware>();
    }
}

