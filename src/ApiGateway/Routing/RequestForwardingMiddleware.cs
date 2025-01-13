using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.IO;

public class RequestForwardingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRouteConfigurationService _routeConfig;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RequestForwardingMiddleware> _logger;

    public RequestForwardingMiddleware(
        RequestDelegate next,
        IRouteConfigurationService routeConfig,
        IHttpClientFactory httpClientFactory,
        ILogger<RequestForwardingMiddleware> logger)
    {
        _next = next;
        _routeConfig = routeConfig;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        var method = context.Request.Method;

        var route = await _routeConfig.GetRouteForPathAsync(path, method);
        if (route == null)
        {
            await _next(context);
            return;
        }

        try
        {
            await ForwardRequestAsync(context, route);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to forward request to {Destination}", route.Destination);
            throw new RequestForwardingException("Failed to forward request", ex);
        }
    }

    private async Task ForwardRequestAsync(HttpContext context, RouteConfig route)
    {
        using var client = _httpClientFactory.CreateClient();
        var destinationUri = BuildDestinationUri(context.Request, route);
        
        using var requestMessage = CreateRequestMessage(context, destinationUri, route);
        using var responseMessage = await SendRequestWithPoliciesAsync(client, requestMessage, route);
        
        await CopyResponseToContextAsync(context, responseMessage);
    }

    private Uri BuildDestinationUri(HttpRequest request, RouteConfig route)
    {
        var destinationUrl = route.Destination.TrimEnd('/');
        var pathAndQuery = request.PathBase.Value +
                          request.Path.Value +
                          request.QueryString.Value;

        return new Uri(destinationUrl + pathAndQuery);
    }

    private HttpRequestMessage CreateRequestMessage(
        HttpContext context,
        Uri uri,
        RouteConfig route)
    {
        var requestMessage = new HttpRequestMessage();
        var requestMethod = context.Request.Method;
        
        // Copy the request method
        requestMessage.Method = new HttpMethod(requestMethod);
        requestMessage.RequestUri = uri;

        // Copy the request body
        if (requestMethod != HttpMethod.Get.Method && 
            requestMethod != HttpMethod.Head.Method && 
            context.Request.Body != null)
        {
            requestMessage.Content = new StreamContent(context.Request.Body);
        }

        // Copy headers
        foreach (var header in context.Request.Headers)
        {
            if (!header.Key.StartsWith("Host", StringComparison.OrdinalIgnoreCase))
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        // Add custom headers from route config
        foreach (var header in route.Headers)
        {
            requestMessage.Headers.Add(header.Key, header.Value);
        }

        return requestMessage;
    }

    private async Task<HttpResponseMessage> SendRequestWithPoliciesAsync(
        HttpClient client,
        HttpRequestMessage request,
        RouteConfig route)
    {
        client.Timeout = TimeSpan.FromMilliseconds(route.Timeout);

        // Apply retry policy if configured
        if (route.RetryPolicy != null)
        {
            return await ExecuteWithRetryAsync(client, request, route.RetryPolicy);
        }

        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }

    private async Task<HttpResponseMessage> ExecuteWithRetryAsync(
        HttpClient client,
        HttpRequestMessage request,
        RetryPolicy retryPolicy)
    {
        var retryCount = 0;
        var delay = retryPolicy.DelayMs;

        while (true)
        {
            try
            {
                var response = await client.SendAsync(
                    request, 
                    HttpCompletionOption.ResponseHeadersRead);

                if (!ShouldRetry(response.StatusCode, retryPolicy))
                    return response;

                if (retryCount >= retryPolicy.MaxRetries)
                    return response;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                if (retryCount >= retryPolicy.MaxRetries)
                    throw;
            }

            retryCount++;
            await Task.Delay(retryPolicy.ExponentialBackoff ? delay * retryCount : delay);
        }
    }

    private bool ShouldRetry(HttpStatusCode statusCode, RetryPolicy retryPolicy)
    {
        return retryPolicy.RetryableStatusCodes.Contains(((int)statusCode).ToString());
    }

    private async Task CopyResponseToContextAsync(
        HttpContext context,
        HttpResponseMessage responseMessage)
    {
        context.Response.StatusCode = (int)responseMessage.StatusCode;

        foreach (var header in responseMessage.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        foreach (var header in responseMessage.Content.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        await responseMessage.Content.CopyToAsync(context.Response.Body);
    }
} 