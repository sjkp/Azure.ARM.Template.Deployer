using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Azure.ARM.Template.Deployer
{
    class Program
    {
        const string ResourceGroupApiVersion = "2015-01-01";
        const string TemplateVersion = "2016-02-01";

        static string ClientId = ConfigurationManager.AppSettings["ClientId"];
        static string ServicePrincipalPassword = ConfigurationManager.AppSettings["ServicePrincipalPassword"];
        static string AzureTenantId = ConfigurationManager.AppSettings["AzureTenantId"];        
        static string SubscriptionId = ConfigurationManager.AppSettings["SubscriptionId"];
        static void Main(string[] args)
        {
            var httpClient = new HttpClient();
            //https://msdn.microsoft.com/en-us/library/azure/dn790546.aspx - Resource Group
            var token = GetAuthorizationToken();

            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + token);
            var rgName = "Sjkp.today.test";
            var location = "North Europe";

            CreateResourceGroup(httpClient, rgName, location);

            //https://msdn.microsoft.com/en-us/library/azure/dn790549.aspx
            if (ValidateDeployment(httpClient, rgName, location))
            {
                DeployResourceGroupTemplate(httpClient, rgName, location);
            }

            

            
        }

        private static void DeployResourceGroupTemplate(HttpClient client, string rgName, string location)
        {
            var deploymentName = DateTime.UtcNow.Ticks.ToString();
            StringContent body = CreateBody();
            var res = client.PutAsync($"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{rgName}/providers/microsoft.resources/deployments/{deploymentName}?api-version={TemplateVersion}", body).Result;
            var result = res.Content.ReadAsStringAsync().Result;
            Console.WriteLine(result);
        }

        private static bool ValidateDeployment(HttpClient client, string rgName, string location)
        {
            var deploymentName = DateTime.UtcNow.Ticks.ToString();
            StringContent body = CreateBody();
            var res = client.PostAsync($"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{rgName}/providers/microsoft.resources/deployments/{deploymentName}/validate?api-version={TemplateVersion}", body).Result;
            var result = res.Content.ReadAsStringAsync().Result;
            Console.WriteLine(result);
            return res.StatusCode == System.Net.HttpStatusCode.OK;
        }

        private static StringContent CreateBody()
        {
            var template = File.ReadAllText("WebSite.json");
            var parameters = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText("WebSite.parameters.json")).parameters;
            var requestBody = BuildBody(template, parameters.ToString());
            var body = new StringContent(requestBody, Encoding.UTF8, "application/json");
            return body;
        }

        private static string BuildBody(string template, string parameters)
        {
            return string.Format(@"{{
                ""properties"": {{
                    ""template"": {0},
    ""mode"": ""Incremental"",
    ""parameters"": {1}
                }}
            }}", template,parameters);
        }

        private static void CreateResourceGroup(HttpClient client, string name, string location)
        {
            var res = client.PutAsync($"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{name}?api-version={ResourceGroupApiVersion}",
                new StringContent(@"{""location"": 
"""+location+@""",  
        }", Encoding.UTF8, "application/json")).Result;

            Console.WriteLine(res.Content.ReadAsStringAsync().Result);
        }

        private static string GetAuthorizationToken()
        {
            ClientCredential creds = new ClientCredential(ClientId, ServicePrincipalPassword);
            var context = new AuthenticationContext("https://login.windows.net/" + AzureTenantId);
            var result = context.AcquireTokenAsync("https://management.azure.com/", creds);
            if (result == null)
            {
                throw new InvalidOperationException("Failed to obtain the JWT token");
            }

            return result.Result.AccessToken;
        }
    }
}
