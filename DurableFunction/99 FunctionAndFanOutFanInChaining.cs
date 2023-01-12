using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace DurableFunction
{
    public static class FunctionChainingAndFanOutFanIn
    {
        [FunctionName(nameof(FunctionChainingAndFanOutFanIn))]
        public static async Task<List<string>> FunctionChainingAndFanOutFanInRunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();
            var task = new List<Task<string>>();

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>(nameof(FunctionChainingAndFanOutFanInSayHello), new ActivityDto("Tokyo")));
            outputs.Add(await context.CallActivityAsync<string>(nameof(FunctionChainingAndFanOutFanInSayHello), new ActivityDto("Seattle")));

            task.Add(context.CallActivityAsync<string>(nameof(FunctionChainingAndFanOutFanInSayHello), new ActivityDto("Sacramento", true)));
            task.Add(context.CallActivityAsync<string>(nameof(FunctionChainingAndFanOutFanInSayHello), new ActivityDto("Moscow", true)));
            task.Add(context.CallActivityAsync<string>(nameof(FunctionChainingAndFanOutFanInSayHello), new ActivityDto("France", true)));

            outputs.AddRange(await Task.WhenAll(task));

            //await context.CallSubOrchestratorAsync("FanOutFanInRunOrchestrator", context.InstanceId);

            outputs.Add(await context.CallActivityAsync<string>(nameof(FunctionChainingAndFanOutFanInSayHello), new ActivityDto("London")));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        public record ActivityDto(string Name, bool Async = false);

        [FunctionName(nameof(FunctionChainingAndFanOutFanInSayHello))]
        public static async Task<string> FunctionChainingAndFanOutFanInSayHello([ActivityTrigger] ActivityDto dto, ILogger log)
        {
            if(dto.Async) await Task.Delay(Random.Shared.Next(5000, 10000));
            if (Random.Shared.Next(1, 11) > 7)
            {
                throw new TooManyRequestsException();
            }

            var fileName = $"{DateTime.UtcNow:dddd-MMMM-dd-yyyy--HH_mm_ss_fff}";
            log.LogInformation($"Saying hello to {dto.Name}.");
            File.Create($"C:\\_test\\DurableFunctions\\{dto.Name}-{fileName}.file");
            return $"Hello {dto.Name}!";
        }

        [FunctionName("RewindInstance")]
        public static Task Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] string instanceId,
            [DurableClient] IDurableOrchestrationClient client
            )
        {
            string reason = "Orchestrator failed and needs to be revived.";
            return client.RewindAsync(instanceId, reason);
        }

        //Entry Point Chaining Function
        [FunctionName(nameof(FunctionChainingAndFanOutFanInHttpStart))]
        public static async Task<HttpResponseMessage> FunctionChainingAndFanOutFanInHttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            var instanceId = await starter.StartNewAsync(nameof(FunctionChainingAndFanOutFanIn), null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        public static RetryOptions Options = new RetryOptions(
            firstRetryInterval: TimeSpan.FromSeconds(2),
            maxNumberOfAttempts: 5)
        {
            Handle = (exception) => exception.InnerException is TooManyRequestsException
        };
    }

    public class TooManyRequestsException : Exception
    {
    }
}