using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace PopulationApi
{
    public static class GetPopulation
    {
        [FunctionName("GetPopulation")]

        // Triggering pull request with a small test change

        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonConvert.DeserializeObject<PopulationRequest>(requestBody);

            if (input?.Years == null || input.Years.Count == 0)
            {
                return new BadRequestObjectResult("Please provide one or more years in the request body.");
            }

            var client = new HttpClient();
            var response = await client.GetAsync("https://datausa.io/api/data?drilldowns=Nation&measures=Population");
            var content = await response.Content.ReadAsStringAsync();

            var dataResponse = JsonConvert.DeserializeObject<PopulationResponse>(content);

            var result = new List<object>();
            long? previousPopulation = null;
            var changes = new List<long>();

            foreach (var year in input.Years.OrderBy(y => y))
            {
                var entry = dataResponse.Data.FirstOrDefault(d => d.Year == year.ToString());
                if (entry != null)
                {
                    long pop = entry.Population;
                    result.Add(new { Year = year, Population = pop });

                    if (previousPopulation != null)
                    {
                        changes.Add(pop - previousPopulation.Value);
                    }
                    previousPopulation = pop;
                }
            }

            var avgChange = changes.Count > 0 ? (long?)changes.Average() : null;
            var lastYear = input.Years.Max();
            var estimated2030 = (avgChange != null && previousPopulation != null && lastYear < 2030)
                ? previousPopulation + avgChange * (2030 - lastYear)
                : null;

            return new OkObjectResult(new
            {
                PopulationByYear = result,
                PopulationChanges = changes,
                Estimated2030 = estimated2030
            });
        }
    }

    public class PopulationRequest
    {
        public List<int> Years { get; set; }
    }

    public class PopulationResponse
    {
        [JsonProperty("data")]
        public List<PopulationDatum> Data { get; set; }
    }

    public class PopulationDatum
    {
        [JsonProperty("Year")]
        public string Year { get; set; }

        [JsonProperty("Population")]
        public long Population { get; set; }
    }
}





