using Azure.Identity;
using Microsoft.Graph;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace AzDoToSpoSample
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            // AzDOよりWorkItemを取得
            var workItem = await GetAzDoWorkItemAsync();
            // SPOにファイルをアップロード(HttpClient)
            await UploadSpoFileHttpClientAsync(workItem);
            // SPOにファイルをアップロード(GraphServiceClient)
            await UploadSpoFileGraphServiceClientAsync(workItem);
        }

        public static async Task<string> GetAzDoWorkItemAsync()
        {
            var organization = "your-azdo-orgnization-name";
            var project = "your-azdo-project-name";
            var pat = "your-azdo-pat";
            var workItemId = "your-azdo-target-workitem-id";

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

            var url = $"https://dev.azure.com/{organization}/{project}/_apis/wit/workitems/{workItemId}?api-version=6.0";
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var workItem = await response.Content.ReadAsStringAsync();
            Console.WriteLine(workItem);

            return workItem;
        }

        public static async Task UploadSpoFileHttpClientAsync(string fileContent)
        {
            var accessToken = await GetSpoAccessTokenAsync();

            var fileName = "workitem.json";

            var siteId = "your-spo-site-id";
            var driveId = "your-spo-drive-id";

            var uploadUrl = $"https://graph.microsoft.com/v1.0/sites/{siteId}/drives/{driveId}/root:/test/{fileName}:/content";

            byte[] byteArray = Encoding.UTF8.GetBytes(fileContent);

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var content = new ByteArrayContent(byteArray);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                var response = await client.PutAsync(uploadUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("File uploaded successfully.");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to upload file. Status Code: {response.StatusCode}");
                    Console.WriteLine($"Error: {error}");
                }
            }
        }

        private static async Task<string> GetSpoAccessTokenAsync()
        {
            var clientId = "your-entra-spo-client-id";
            var clientSecret = "your-entra-spo-client-secret";
            var tenantId = "your-entra-tenant-id";

            var scope = "https://graph.microsoft.com/.default";
            var tokenEndpoint = $"https://login.windows.net/{tenantId}/oauth2/v2.0/token";

            using (var client = new HttpClient())
            {
                var content = new StringContent(
                    $"client_id={clientId}&client_secret={clientSecret}&grant_type=client_credentials&scope={scope}",
                    Encoding.UTF8,
                    "application/x-www-form-urlencoded"
                );

                var response = await client.PostAsync(tokenEndpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(responseBody);
                    return json["access_token"]?.ToString();
                }
                else
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to get token. Status Code: {response.StatusCode}, Details: {errorResponse}");
                }
            }
        }

        public static async Task UploadSpoFileGraphServiceClientAsync(string fileContent)
        {
            var clientId = "your-entra-spo-client-id";
            var clientSecret = "your-entra-spo-client-secret";
            var tenantId = "your-entra-tenant-id";

            var driveId = "your-spo-drive-id";

            var fileName = "workitem.json";

            var clientSecretCredential = new ClientSecretCredential(tenantId, clientId, clientSecret);

            var graphClient = new GraphServiceClient(clientSecretCredential);

            byte[] byteArray = Encoding.UTF8.GetBytes(fileContent);

            using (Stream stream = new MemoryStream(byteArray))
            {
                var createItem = await graphClient.Drives[driveId].Root.ItemWithPath($"/test/{fileName}").Content.PutAsync(stream);
                Console.WriteLine("Stream created successfully.");
            }
        }

    }
}
