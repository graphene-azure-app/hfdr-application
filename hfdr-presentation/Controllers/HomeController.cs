using hfdr_presentation.Models;
using HFDR_Schema;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileSystemGlobbing.Internal.PathSegments;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace hfdr_presentation.Controllers
{
    public static class SessionExtensions
    {
        public static void Set<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonConvert.SerializeObject(value));
        }

        public static T Get<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default(T) : JsonConvert.DeserializeObject<T>(value);
        }
    }
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration Configuration;
        const int PAGE_SIZE = 5;
         
        public async Task<List<Recruit>> PaginatedResult(int currentPage, int totalPage)
        {
            @ViewBag.CurrentPage = currentPage;
            @ViewBag.TotalPage = totalPage;
            PagedRecruit pRecruit;
            var dict = HttpContext.Session.Get<Dictionary<int, string>>("currDict");
            if (dict == null) dict = new Dictionary<int, string>();
            int pageWithToken = currentPage;
            string cToken = null;

            while(pageWithToken > 1)
            {
                if(dict.ContainsKey(pageWithToken))
                {
                    cToken = dict[pageWithToken];
                    break;
                }
                pageWithToken--;

            }

            bool samePage = pageWithToken == currentPage;
            string queryString = Configuration["ConnectionStrings:ListPageResult"] + "&count=" + PAGE_SIZE;

            do
            {
                using (HttpClient client = new HttpClient())
                {
                    var httpReq = new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri(queryString),
                        Content = cToken == null ? null : new StringContent(cToken, Encoding.UTF8)
                    };

                    using (HttpResponseMessage res = await client.SendAsync(httpReq).ConfigureAwait(false))
                    {
                        using (HttpContent content = res.Content)
                        {
                            var jsonResult = await content.ReadAsStringAsync();
                            pRecruit = JsonConvert.DeserializeObject<PagedRecruit>(jsonResult);
                            if(pRecruit.HasMoreResults)
                            {
                                cToken = pRecruit.PagingToken;
                                if(!dict.ContainsKey(pageWithToken + 1))
                                    dict.Add(pageWithToken + 1, cToken);
                            }
                        }
                    }
                }
                pageWithToken++;
            } while (pageWithToken <= currentPage && (!samePage));

            HttpContext.Session.Set<Dictionary<int, string>>("currDict", dict);
            return pRecruit.Results;
        }
        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            Configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }
        public async Task<IActionResult> Recruit(int page = 1)
        {
            int pageCnt = HttpContext.Session.Get<int>("pgCount");
            if(pageCnt == 0)
            {
                pageCnt = await CountPage();
            }
            HttpContext.Session.Set<int>("pgCount", pageCnt);
            var paginatedRecruit = await PaginatedResult(page, pageCnt);
            return View(paginatedRecruit);
        }

        private async Task<int> CountPage()
        {
            string queryString = Configuration["ConnectionStrings:CountEntryAddr"];
            RecruitCnt rCount;
            using (HttpClient client = new HttpClient())
            {
                using (HttpResponseMessage res = await client.GetAsync(queryString))
                {
                    using (HttpContent content = res.Content)
                    {
                        var jsonRes = await content.ReadAsStringAsync();
                        rCount = JsonConvert.DeserializeObject<RecruitCnt>(jsonRes);
                    }
                }    
            }
            if (rCount.TOTAL % PAGE_SIZE == 0)
                return rCount.TOTAL / PAGE_SIZE;
            return rCount.TOTAL / PAGE_SIZE + 1;
        }

        private async Task<Recruit> GetRecruit(Recruit recruit)
        {
            string functionUrl = Configuration["ConnectionStrings:GetRecruitAddrPartUn"] + recruit.id + Configuration["ConnectionStrings:GetRecruitAddrPartDeux"];
            using (HttpClient client = new HttpClient())
            {
                using (HttpResponseMessage res = await client.GetAsync(functionUrl))
                {
                    using(HttpContent content = res.Content)
                    {
                        var jsonResult = await content.ReadAsStringAsync();
                        recruit = JsonConvert.DeserializeObject<Recruit>(jsonResult);
                    }
                }
            }
            return recruit;
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(Recruit recruit)
        {
            HttpContext.Session.Set<int>("pgCount", 0);
            HttpContext.Session.Set<Dictionary<int, string>>("currDict", null);
            recruit.id = Guid.NewGuid().ToString();
            recruit.APPLIED_DATE = DateTime.Now;
            string postMsg = System.Text.Json.JsonSerializer.Serialize(recruit);

            int status = await CreateNewRecruit(postMsg);

            if (status == 0)
                return RedirectToAction("Recruit");

            return RedirectToAction("Error");
        }

        private async Task<int> CreateNewRecruit(string postMsg)
        {
            var content = new StringContent(postMsg);   
            using (HttpClient client = new HttpClient())
            {
                await client.PostAsync(Configuration["ConnectionStrings:CreateNewRecruitAddr"], content);
            }
            return 0;

        }

        public async Task<IActionResult> Update(Recruit recruit)
        {
            Recruit answerRecruit = await GetRecruit(recruit);
            return View(answerRecruit);
        }
        
        [HttpPost]
        public async Task<IActionResult> Update(Recruit recruit, int placeHolder)
        {
            recruit.APPLIED_DATE = DateTime.Now;
            string putMsg = System.Text.Json.JsonSerializer.Serialize(recruit);

            int status = await UpdateRecruit(putMsg, recruit.id);

            if (status == 0)
                return RedirectToAction("Recruit");

            return RedirectToAction("Error");
        }

        private async Task<int> UpdateRecruit(string putMsg, string recruitID)
        {
            var content = new StringContent(putMsg);
            string requestUrl = Configuration["ConnectionStrings:PutRecruitAddrPartUn"] + recruitID + Configuration["ConnectionStrings:PutRecruitAddrPartDeux"];
            using (HttpClient client = new HttpClient())
            {
                await client.PutAsync(requestUrl, content);
            }
            return 0;

        }

        public async Task<IActionResult> Delete(Recruit recruit)
        {
            HttpContext.Session.Set<Dictionary<int, string>>("currDict", null);
            HttpContext.Session.Set<int>("pgCount", 0);
            string recuritID = recruit.id;

            int status = await DeleteRecruit(recuritID);

            if (status == 0)
                return RedirectToAction("Recruit");

            return RedirectToAction("Error");
        }

        private async Task<int> DeleteRecruit(string recruitID)
        {
            string requestUrl = Configuration["ConnectionStrings:DeleteRecuritAddrPartUn"] + recruitID + Configuration["ConnectionStrings:DeleteRecuritAddrPartDeux"];
            using (HttpClient client = new HttpClient())
            {
                await client.DeleteAsync(requestUrl);
            }
            return 0;

        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
