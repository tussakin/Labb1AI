using System;
using System.Text;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace translate_text
{
    class Program
    {
        private static string translatorEndpoint;
        private static string cogSvcKey;
        private static string cogSvcRegion;
        private static string qnaEndpoint;
        private static string qnaSubscriptionKey;
        private static string qnaProjectName;
        private static string qnaDeploymentName;
        private static string OcpApimSubscriptionKey;

        static async Task Main(string[] args)
        {
            try
            {
                IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
                IConfigurationRoot configuration = builder.Build();
                translatorEndpoint = configuration["Azure:Translator:Endpoint"];
                cogSvcKey = configuration["Azure:Translator:SubscriptionKey"];
                cogSvcRegion = configuration["Azure:Translator:Region"];
                qnaEndpoint = configuration["Azure:QnA:Endpoint"];
                qnaSubscriptionKey = configuration["Azure:QnA:SubscriptionKey"];
                qnaProjectName = configuration["Azure:QnA:ProjectName"];
                qnaDeploymentName = configuration["Azure:QnA:DeploymentName"];
                OcpApimSubscriptionKey = configuration["Azure:QnA:OcpApimSubscriptionKey"];


                Console.InputEncoding = Encoding.UTF8;
                Console.OutputEncoding = Encoding.UTF8;

                while (true)
                {
                    Console.WriteLine("Enter the text you want to translate (or type 'exit' to quit):");
                    var inputText = Console.ReadLine();

                    if (inputText.ToLower() == "exit")
                    {
                        break;
                    }

                    string detectedLanguage = await GetLanguage(inputText);
                    Console.WriteLine("Detected Language: " + detectedLanguage);

                    if (detectedLanguage != "en")
                    {
                        string translatedText = await Translate(inputText, detectedLanguage);
                        Console.WriteLine("Translated Text:\n" + translatedText);

                        string qnaAnswer = await GetAnswerFromQnA(translatedText);
                        Console.WriteLine("QnA Answer:\n" + qnaAnswer);
                    }
                    else
                    {
                        Console.WriteLine("The text is already in English.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }


        static async Task<string> GetLanguage(string text)
        {
            string language = "en";

            object[] body = new object[] { new { Text = text } };
            var requestBody = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage())
                {
                    string path = "/detect?api-version=3.0";
                    request.Method = HttpMethod.Post;
                    request.RequestUri = new Uri(translatorEndpoint + path);
                    request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                    request.Headers.Add("Ocp-Apim-Subscription-Key", cogSvcKey);
                    request.Headers.Add("Ocp-Apim-Subscription-Region", cogSvcRegion);

                    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                    string responseContent = await response.Content.ReadAsStringAsync();

                    JArray jsonResponse = JArray.Parse(responseContent);
                    language = (string)jsonResponse[0]["language"];
                }
            }

            return language;
        }

        static async Task<string> Translate(string text, string sourceLanguage)
        {
            string translation = "";

            object[] body = new object[] { new { Text = text } };
            var requestBody = JsonConvert.SerializeObject(body);
            using (var client = new HttpClient())
            {
                using (var request = new HttpRequestMessage())
                {
                    string path = $"/translate?api-version=3.0&from={sourceLanguage}&to=en";
                    request.Method = HttpMethod.Post;
                    request.RequestUri = new Uri(translatorEndpoint + path);
                    request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                    request.Headers.Add("Ocp-Apim-Subscription-Key", cogSvcKey);
                    request.Headers.Add("Ocp-Apim-Subscription-Region", cogSvcRegion);

                    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                    string responseContent = await response.Content.ReadAsStringAsync();

                    JArray jsonResponse = JArray.Parse(responseContent);
                    translation = (string)jsonResponse[0]["translations"][0]["text"];
                }
            }

            return translation;
        }

        static async Task<string> GetAnswerFromQnA(string question)
{
    string answer = "No answer found.";

    var requestBody = new
    {
        top = 1,
        question = question, 
        includeUnstructuredSources = true, 
        confidenceScoreThreshold = 0.2, 
        answerSpanRequest = new
        {
            enable = true,
            topAnswersWithSpan = 1,
            confidenceScoreThreshold = 0.2
        },
    };

    string jsonRequestBody = JsonConvert.SerializeObject(requestBody);

    using (var client = new HttpClient())
    {
        using (var request = new HttpRequestMessage())
        {
            string endpointUrl = "https://tusslabb1languageqna.cognitiveservices.azure.com/language/:query-knowledgebases?projectName=TessLabb1&api-version=2021-10-01&deploymentName=production";
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri(endpointUrl);
            request.Content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");

            request.Headers.Add("Ocp-Apim-Subscription-Key", OcpApimSubscriptionKey);
            
            HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
            string responseContent = await response.Content.ReadAsStringAsync();

            JObject jsonResponse = JObject.Parse(responseContent);

            if (jsonResponse["answers"] != null && jsonResponse["answers"].HasValues)
            {
                answer = jsonResponse["answers"][0]["answer"].ToString();
            }
        }
    }

    return answer;
} 
    }
}
