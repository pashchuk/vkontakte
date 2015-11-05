using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Pashchuk.Social.Vkontakte
{
    public static class OpenAPI
    {
        public static int RequestsPerSecond { get; } = 3;

        public static Task<string[]> GetGroupMembersAsync(string groupdId)
        {
            return Task.Run(async () =>
            {
                using (var client = new HttpClient())
                {
                    var maxRequestCount = 1000;
                    var count = 0;
                    var offset = 0;
                    string[] result;
                    var url = getMethodUrl("groups.getMembers", new Dictionary<string, string>()
                        {{"group_id", groupdId}});
                    dynamic requestResult = JsonConvert.DeserializeObject(client.GetAsync(url).Result.Content.ReadAsStringAsync().Result);
                    count = (int)requestResult.response.count;
                    var users = (requestResult.response.users as JArray).Select(x => (string)x).ToArray();
                    result = new string[count];
                    Array.Copy(users, result, users.Length);
                    if (count <= maxRequestCount) return result;
                    var tasks = new List<Task<string>>();
                    while (offset < count)
                    {
                        offset += maxRequestCount;
                        url = getMethodUrl("groups.getMembers", new Dictionary<string, string>()
                        {
                            {"group_id", groupdId},
                            {"offset", offset.ToString()}
                        });
                        tasks.Add(client.GetAsync(url).ContinueWith((t) =>
                            t.Result.Content.ReadAsStringAsync().Result));
                        await Task.Delay(1000/RequestsPerSecond);
                    }
                    await Task.WhenAll(tasks);
                    offset = maxRequestCount;
                    foreach (var task in tasks)
                    {
                        requestResult = JsonConvert.DeserializeObject(task.Result);
                        users = (requestResult.response.users as JArray).Select(x => (string)x).ToArray();
                        Array.Copy(users, 0, result, offset, users.Length);
                        offset += users.Length;
                    }
                    return result;
                }
            });
        }

        public static Task<string[]> GetUserFriendsAsync(string userId)
        {
            return Task.Run(async () =>
            {
                using (var client = new HttpClient())
                {
                    var maxRequestCount = 1000;
                    var count = 0;
                    var offset = 0;
                    string[] result;
                    var url = getMethodUrl("friends.get", new Dictionary<string, string>()
                        {{"user_id", userId}});
                    result =
                        JObject.Parse(await client.GetAsync(url).Result.Content.ReadAsStringAsync())
                            .SelectToken("response")
                            .ToObject<string[]>();

                    count = result.Length;
                    //if (count <= maxRequestCount) return result;
                    //var tasks = new List<Task<string>>();
                    //while (offset < count && count < maxRequestCount)
                    //{
                    //    offset += maxRequestCount;
                    //    url = getMethodUrl("friends.get", new Dictionary<string, string>()
                    //    {
                    //        {"user_id", userId},
                    //        {"offset", offset.ToString()}
                    //    });
                    //    tasks.Add(client.GetAsync(url).ContinueWith((t) =>
                    //        t.Result.Content.ReadAsStringAsync().Result));
                    //    await Task.Delay(1000/RequestsPerSecond);
                    //}
                    //await Task.WhenAll(tasks);
                    //offset = maxRequestCount;
                    //foreach (var task in tasks)
                    //{
                    //    requestResult = JsonConvert.DeserializeObject(task.Result);
                    //    users = (requestResult.response.items as JArray).Select(x => (string)x).ToArray();
                    //    Array.Copy(users, 0, result, offset, users.Length);
                    //    offset += users.Length;
                    //}
                    return result;
                }
            });
        }

        public static string[] GetUserIds(string[] screenName)
        {
            string[] result = null;
            using (var client = new HttpClient())
            {
                var url = getMethodUrl("users.get", new Dictionary<string, string>()
                {{"user_ids", string.Join(",", screenName)}});
                result =
                    JObject.Parse(client.GetAsync(url).Result.Content.ReadAsStringAsync().Result)
                        .SelectToken("response")
                        .Select(t => t.SelectToken("uid")
                            .ToObject<string>())
                        .ToArray();
            }
            return result;
        }

        //polls.getById
        public static PollInfo GetPollById(string pollId,string ownerId, string token)
        {
            PollInfo result = null;
            using (var client = new HttpClient())
            {
                var maxRequestCount = 1000;
                var count = 0;
                var offset = 0;

                var url = getMethodUrl("polls.getById", new Dictionary<string, string>()
                {
                    {"poll_id", pollId},
                    {"owner_id", ownerId},
                    {"access_token",token }
                });
                result =
                    JObject.Parse(client.GetAsync(url).Result.Content.ReadAsStringAsync().Result)
                        .SelectToken("response")
                        .ToObject<PollInfo>();
            }
            return result;
        }

        public static List<VotesInfo> GetPollVoters(string pollId, string ownerId, IEnumerable<string> answerIds, string token)
        {
            List<VotesInfo> result = null;
            using (var client = new HttpClient())
            {
                var maxRequestCount = 1000;
                var count = 0;
                var offset = 0;

                var url = getMethodUrl("polls.getVoters", new Dictionary<string, string>()
                {
                    {"poll_id", pollId},
                    {"owner_id", ownerId},
                    {"answer_ids", string.Join(",", answerIds)},
                    {"count", "1000"},
                    {"fields","first_name,last_name" },
                    {"access_token", token}
                });
                result = new List<VotesInfo>(4);
                var re = JObject.Parse(client.GetAsync(url).Result.Content.ReadAsStringAsync().Result)
                    .SelectToken("response");
                for (int i = 0; i < re.Count(); i++)
                {
                    result.Add(new VotesInfo());
                    result[i].AnswerId = re[i].SelectToken("answer_id").ToString();
                    result[i].Voters = re[i].SelectToken("users")
                        .Skip(1)
                        .Select(t => t.ToObject<User>())
                        .ToList();
                }
            }
            return result;
        }

        static string getMethodUrl(string methodName, Dictionary<string, string> parameters)
        {
            var builder = new StringBuilder();
            foreach (var parameter in parameters)
                builder.Append($"&{parameter.Key}={parameter.Value}");
            return $@"https://api.vk.com/method/{methodName}?{builder}";
        }
    }

    public class PollAnswer
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "text")]
        public string Text { get; set; }

        [JsonProperty(PropertyName = "votes")]
        public int Votes { get; set; }

        [JsonProperty(PropertyName = "rate")]
        public double Rate { get; set; }
    }

    public class PollInfo
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "owner_id")]
        public string OwnerId { get; set; }

        [JsonProperty(PropertyName = "question")]
        public string Question { get; set; }

        [JsonProperty(PropertyName = "votes")]
        public int Votes { get; set; }

        [JsonProperty(PropertyName = "answers")]
        public List<PollAnswer> Answers { get; set; }
    }


    public class VotesInfo
    {
        [JsonProperty(PropertyName = "answer_id")]
        public string AnswerId { get; set; }

        [JsonProperty(PropertyName = "users")]
        public List<User> Voters { get; set; }
    }
    public class User
    {
        [JsonProperty(PropertyName = "uid")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "first_name")]
        public string FirstName { get; set; }

        [JsonProperty(PropertyName = "last_name")]
        public string LastName { get; set; }        
    }
}
