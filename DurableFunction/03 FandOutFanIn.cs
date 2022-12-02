using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace DurableFunction
{
    public static class FanOutFanIn
    {
        [FunctionName(nameof(FanOutFanIn))]
        public static async Task<List<string>> FanOutFanInRunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var task = new List<Task<string>>();

            // Replace "hello" with the name of your Durable Activity Function.
            task.Add(context.CallActivityAsync<string>(nameof(FanOutFanInSayHello), "Tokyo"));
            task.Add(context.CallActivityAsync<string>(nameof(FanOutFanInSayHello), "Seattle"));
            task.Add(context.CallActivityAsync<string>(nameof(FanOutFanInSayHello), "London"));

            var outputs = await Task.WhenAll(task);
            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs.ToList();
        }

        [FunctionName(nameof(FanOutFanInSayHello))]
        public static async Task<string> FanOutFanInSayHello([ActivityTrigger] string name, ILogger log)
        {
            await Task.Delay(Random.Shared.Next(5000, 10000));
            log.LogInformation($"Saying hello to {name}.");
            return $"Hello {name}!";
        }

        //Entry Point Chaining Function
        [FunctionName(nameof(FanOutFanInHttpStart))]
        public static async Task<HttpResponseMessage> FanOutFanInHttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync(nameof(FanOutFanIn), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}