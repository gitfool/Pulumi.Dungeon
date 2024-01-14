using Flurl.Http;
using Polly;
using Polly.Retry;

namespace Pulumi.Dungeon.K8s;

public class ApiServer
{
    public ApiServer(string baseUrl)
    {
        var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
        Client = new FlurlClient(new HttpClient(handler)) { BaseUrl = baseUrl };

        ResiliencePipeline = new ResiliencePipelineBuilder<HttpStatusCode>()
            .AddRetry(new RetryStrategyOptions<HttpStatusCode>
            {
                ShouldHandle = args => args.Outcome.Result switch
                {
                    not HttpStatusCode.OK => PredicateResult.True(),
                    _ => PredicateResult.False()
                },
                BackoffType = DelayBackoffType.Constant,
                Delay = TimeSpan.FromSeconds(5),
                MaxRetryAttempts = int.MaxValue,
                OnRetry = args =>
                {
                    Log.Debug($"Waiting for api server... ({args.AttemptNumber + 1})", ephemeral: true);
                    return default;
                }
            })
            .AddTimeout(TimeSpan.FromMinutes(5))
            .Build();
    }

    public async ValueTask<HttpStatusCode> GetHealthzAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await Client.Request("healthz").AllowAnyHttpStatus().GetAsync(cancellationToken: cancellationToken)).ResponseMessage.StatusCode;
        }
        catch (Exception ex)
        {
            Log.Debug($"ApiServer: {ex.GetBaseException().Message}");
            return HttpStatusCode.ServiceUnavailable;
        }
    }

    public Task<HttpStatusCode> WaitForHealthzAsync() => ResiliencePipeline.ExecuteAsync(GetHealthzAsync).AsTask();

    protected IFlurlClient Client { get; }

    private ResiliencePipeline<HttpStatusCode> ResiliencePipeline { get; }
}
