using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using System;
using System.Text;
using System.Globalization;
using Newtonsoft.Json;
using System.Net.Http;

namespace katsujim_SignalR_functionApp
{
    public static class Function1
    {
        [FunctionName("negotiate")]
        public static SignalRConnectionInfo GetSignalRInfo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [SignalRConnectionInfo(HubName = "chat")] SignalRConnectionInfo connectionInfo)
        {
            return connectionInfo;
        }

        [FunctionName("messages")]
        public static Task SendMessage(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] object message,
            [SignalR(HubName = "chat")] IAsyncCollector<SignalRMessage> signalRMessages)
        {

            //チャットメッセージの翻訳と保存を実施
            StoreChatMessage(message);

            //SignalRへメッセージの追加
            return signalRMessages.AddAsync(
                new SignalRMessage
                {
                    Target = "newMessage",
                    Arguments = new[] { message }
                });
        }


        public static async Task StoreChatMessage(object message)
        {
            //メッセージの取得
            dynamic data = JsonConvert.DeserializeObject(message.ToString());
            string srcMessage = data?.text;
            string sender = data?.sender;

            //メッセージの翻訳を実施
            object tranlatedMessageObject = await TranslateText(srcMessage);
            dynamic data2 = JsonConvert.DeserializeObject(tranlatedMessageObject.ToString().TrimStart('[').TrimEnd(']'));
            string message_en = data2?.translations[0].text;

            //翻訳したメッセージをSQLへ保存
            string tableName = "test";
            var culture = CultureInfo.CreateSpecificCulture("ja-JP");
            string timestamp = DateTime.UtcNow.AddHours(9.0).ToString("u", culture);

            try
            {
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
                builder.DataSource = "SERVERNAME";
                builder.UserID = "USERNAME";
                builder.Password = "PASS";
                builder.InitialCatalog = "DBNAME";

                using (SqlConnection connection = new SqlConnection(builder.ConnectionString))
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append($"INSERT INTO {tableName} VALUES('{timestamp}','N{sender}',N'{srcMessage}','{message_en}');");
                    String sql = sb.ToString();

                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        connection.Open();
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Console.WriteLine("{0} {1}", reader.GetString(0), reader.GetString(1));
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }
        }

        public static async Task<string> TranslateText(string message)
        {
            string subscriptionKey = "YOURKEY";
            string endpoint = "https://api.cognitive.microsofttranslator.com/";
            string location = "japaneast";
            // Input and output languages are defined as parameters.
            string route = "/translate?api-version=3.0&from=ja&to=en";
            object[] body = new object[] { new { Text = message } };
            var requestBody = JsonConvert.SerializeObject(body);


            HttpResponseMessage response;
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                // Build the request.
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(endpoint + route);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
                request.Headers.Add("Ocp-Apim-Subscription-Region", location);

                // Send the request and get response.
                response = await client.SendAsync(request).ConfigureAwait(false);
                // Read response as a string.
                string result = await response.Content.ReadAsStringAsync();
                Console.WriteLine(result);
            }
            return await response.Content.ReadAsStringAsync();
        }
    }
}
