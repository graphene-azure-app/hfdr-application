using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing.Internal.PathSegments;
using Microsoft.Azure.Documents.Client;
using System.Reflection.Metadata;
using Microsoft.Azure.Documents;
using Microsoft.VisualBasic;
using HFDR_Schema;
using Microsoft.Azure.Documents.Linq;

namespace hfdr
{
    public static class hfdr
    {

        [FunctionName("hfdr-create")]
        public static void CreateRecruit(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "recruit")] HttpRequest req,
            [CosmosDB (
                databaseName:"recruit-db",
                collectionName:"recruits",
                ConnectionStringSetting = "CosmosDBConnection")] out dynamic document,
                ILogger log)
        {
            log.LogInformation("Crate a recruit entry.");

            string requestBody = new StreamReader(req.Body).ReadToEnd();
            var input = JsonConvert.DeserializeObject<Recruit>(requestBody);
            document = input;
        }

        [FunctionName("hfdr-count")]
        public static async Task<IActionResult> CountRecruit(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "count")] HttpRequest req,
            [CosmosDB(
                databaseName:"recruit-db",
                collectionName:"recruits",
                ConnectionStringSetting = "CosmosDBConnection"
                )] DocumentClient client,
            ILogger log)
        {
            log.LogInformation("Get the count of all the recruit entries");
            FeedOptions queryOptions = new FeedOptions { MaxItemCount = -1 };
            IQueryable<dynamic> countQuery = client.CreateDocumentQuery<dynamic>(
                UriFactory.CreateDocumentCollectionUri("recruit-db", "recruits"),
                "SELECT COUNT(1) AS TOTAL FROM recruits",
                queryOptions);
            dynamic count = null;
            foreach (var c in countQuery)
                count = c;
            return new OkObjectResult(count);
        }

        [FunctionName("hfdr-list")]
        public static async Task<IActionResult> ListRecruit(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recruit")] HttpRequest req,
            [CosmosDB(
                databaseName:"recruit-db",
                collectionName:"recruits",
                ConnectionStringSetting = "CosmosDBConnection"
                )] DocumentClient client, 
            ILogger log)
        {
            log.LogInformation("Get the all the recruit entries, with continual token");
            var queryParams = req.GetQueryParameterDictionary();
            var count = Int32.Parse(queryParams.FirstOrDefault(q => q.Key == "count").Value ?? "-1");

            string pToken = await new StreamReader(req.Body).ReadToEndAsync();
           
            var feedOptions = new FeedOptions()
            {
                MaxItemCount = count,
                RequestContinuation = pToken
            };

            var uri = UriFactory.CreateDocumentCollectionUri("recruit-db", "recruits");
            var query = client.CreateDocumentQuery(uri, feedOptions).AsDocumentQuery();
            var results = await query.ExecuteNextAsync();

            return new OkObjectResult(new
            {
                hasMoreResults = query.HasMoreResults,
                pagingToken = query.HasMoreResults ? results.ResponseContinuation : null,
                results = results.ToList()
            });
        }

        [FunctionName("hfdr-get")]
        public static async Task<IActionResult> GetRecruit(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "recruit/{id}")] HttpRequest req, 
            [CosmosDB(
                databaseName:"recruit-db",
                collectionName: "recruits",
                ConnectionStringSetting = "CosmosDBConnection",
                Id = "{id}",
                PartitionKey = "{id}")] Recruit recruit,
            ILogger log)
        {
            if (recruit == null)
                return new NotFoundResult();
            return new OkObjectResult(recruit);
        }

        [FunctionName("hfdr-update")]
        public static void UpdateRecruit(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "recruit/{id}")] HttpRequest req, 
            [CosmosDB(
                databaseName:"recruit-db",
                collectionName:"recruits",
                ConnectionStringSetting = "CosmosDBConnection",
                Id = "{id}",
                PartitionKey = "{id}")] out dynamic recruit,
            ILogger log)
        {
            string requestBody = new StreamReader(req.Body).ReadToEnd();
            var updateRecruit = JsonConvert.DeserializeObject<Recruit>(requestBody);
            recruit = updateRecruit;
        }

        [FunctionName("hfdr-delete")]

        public static async Task<IActionResult> DeleteRecruit(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "recruit/{id}")] HttpRequest req,
            ILogger log, string id)
        {

            DocumentClient client = new DocumentClient(new Uri(System.Environment.GetEnvironmentVariable($"CosmosDBEndPoint")), System.Environment.GetEnvironmentVariable($"CosmosDBAuthKey"));
            Microsoft.Azure.Documents.Document doc = client.CreateDocumentQuery
                (UriFactory.CreateDocumentCollectionUri("recruit-db", "recruits"))
                .Where(d => d.Id == id).AsEnumerable().FirstOrDefault();
            await client.DeleteDocumentAsync(doc.SelfLink, new RequestOptions { PartitionKey = new PartitionKey(doc.Id) });
            return new OkResult();
        }

    }
}
