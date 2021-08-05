using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Flurl.Http;
using Polly;
using Polly.Wrap;

namespace Pulumi.Dungeon.K8s
{
    public class ApiServer
    {
        public ApiServer(string baseUrl)
        {
            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
            Client = new FlurlClient(new HttpClient(handler)) { BaseUrl = baseUrl };

            HealthzPolicy = Policy.WrapAsync(
                Policy.TimeoutAsync<HttpStatusCode>(context => (TimeSpan)context["Timeout"]),
                Policy.HandleResult<HttpStatusCode>(status => status != HttpStatusCode.OK)
                    .WaitAndRetryForeverAsync(_ => TimeSpan.FromSeconds(5), (_, count, _) => Log.Debug($"Waiting for api server... ({count})", ephemeral: true)));
        }

        public async Task<HttpStatusCode> GetHealthzAsync()
        {
            try
            {
                return (await Client.Request("healthz").AllowAnyHttpStatus().GetAsync()).ResponseMessage.StatusCode;
            }
            catch (Exception ex)
            {
                Log.Debug($"ApiServer: {ex.GetBaseException().Message}");
                return HttpStatusCode.ServiceUnavailable;
            }
        }

        public Task<HttpStatusCode> WaitForHealthzAsync(TimeSpan timeout) =>
            HealthzPolicy.ExecuteAsync(context => GetHealthzAsync(), new Dictionary<string, object> { ["Timeout"] = timeout });

        protected IFlurlClient Client { get; }

        private AsyncPolicyWrap<HttpStatusCode> HealthzPolicy { get; }
    }
}
