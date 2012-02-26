﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Net;
using System.IO;
using System.Web.Script.Services;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.Serialization.Json;
using Microsoft.JScript;

/// <summary>
/// Summary description for Proxy
/// </summary>
[WebService(Namespace = "http://tempuri.org/")]
[WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
// To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
[ScriptService]
public class Proxy : System.Web.Services.WebService {

    public static DateTime lastCacheTime = new DateTime(0);
    public static Dictionary<int, string> universityInfoJsonCache = new Dictionary<int, string>();
    public static string universityListJson;

    public Proxy() {
        var context = HttpContext.Current;

        var now = DateTime.Now;
        if ((now - lastCacheTime).TotalMinutes < 15)
            return;

        lastCacheTime = now;

        universityInfoJsonCache.Clear();
        var lines = File.ReadAllLines(context.Server.MapPath("Universities/List"));
        var list = new List<UniversityListInfo>();
        foreach (var line in lines) {
            var items = line.Split(';');
            var id = int.Parse(items[0]);
            universityInfoJsonCache[id] = Json.Stringify(new UniversityInfo() {
                //Id = int.Parse(items[0]),
                //Name = items[4],
                HasVerifier = items[2] == "1",
                FirstWeek = items[5],
                WeekCount = int.Parse(items[6]),
                Sessions = items[7]
            });
            list.Add(new UniversityListInfo() {
                Id = id,
                Name = items[4]
            });
        }
        universityListJson = Json.Stringify(list);
        //Uncomment the following line if using designed components 
        //InitializeComponent(); 
    }

    [WebMethod]
    public void ListUniversities() {
        var context = HttpContext.Current;
        context.Response.ContentType = "application/json";
        context.Response.Write(universityListJson);
    }

    [WebMethod]
    public void GetUniverisityInfo(int universityId) {
        var context = HttpContext.Current;
        context.Response.ContentType = "application/json";

        var json =
            universityInfoJsonCache.ContainsKey(universityId) ?
            universityInfoJsonCache[universityId] :
            "null";

        context.Response.Write(json);
    }

    [WebMethod(EnableSession = true)]
    public void FetchVerifier(int universityId) {
        var solutionInfo = Solution.GetSolutionInfo(universityId);
        var rules = solutionInfo.Rules;

        var url = (rules["LoginVerifierUrl"] as string).Replace("{BaseUrl}", solutionInfo.BaseUrl);

        var request = new HttpRequest();
        request.Open("GET", url);
        request.Send();
        var stream = request.ResponseStream;

        Session.Add("CookieContainer", request.CookieContainer);

        var context = HttpContext.Current;
        context.Response.ContentType = "image/jpeg";

        var buffer = new byte[1024];
        var size = 0;
        while ((size = stream.Read(buffer, 0, 1024)) > 0)
            context.Response.BinaryWrite(buffer.Take(size).ToArray());
    }

    [WebMethod(EnableSession = true)]
    public void FetchClasses(int universityId, string username, string password, string verifier) {
        var cookieContainer = Session["CookieContainer"] as CookieContainer;

        var request = new HttpRequest();
        if (cookieContainer != null)
            request.CookieContainer = cookieContainer;

        var solutionInfo = Solution.GetSolutionInfo(universityId);
        var rules = solutionInfo.Rules;

        var encoding = Encoding.GetEncoding(rules["Encoding"] as string);

        var queryVariables = new Dictionary<string, object>() {
            {"Username", username},
            {"Password", password},
            {"Verifier", verifier}
        };

        //login
        new Action(() => {
            SendRequest(request, rules, solutionInfo.BaseUrl, "Login", queryVariables, encoding);
            var text = request.ResponseText;

            var loginErrorKeys = rules["LoginErrorKeys"] as string[];

            foreach (var keyInfo in loginErrorKeys) {
                var index = keyInfo.LastIndexOf(',');
                var key = keyInfo.Substring(0, index);
                var error = keyInfo.Substring(index + 1);
                if (text.Contains(key))
                    throw new Exception(error);
            }

            if (!text.Contains(rules["LoginSuccessKey"] as string))
                throw new Exception("UnknownLoginError");
        })();

        //pre-fetch
        new Action(() => {
            SendRequest(request, rules, solutionInfo.BaseUrl, "PreFetch", queryVariables, encoding);
            var text = request.ResponseText;

            var preFetchValues = new List<string>();

            var regexInfos = rules["PreFetchRegexes"] as string[];

            foreach (var info in regexInfos) {
                var regexInfo = new RegexInfo(info);
                preFetchValues.Add(regexInfo.Match(text));
            }

            queryVariables["PreFetchValues"] = preFetchValues.ToArray();
        })();

        ClassInfo[] classInfos = null;
        //fetch
        new Action(() => {
            SendRequest(request, rules, solutionInfo.BaseUrl, "Fetch", queryVariables, encoding);
            var text = request.ResponseText;

            var rowsRegexInfo = new RegexInfo(rules["RowsRegex"] as string);
            var rowsStr = rowsRegexInfo.Match(text);

            var cellsRegexInfo = new RegexInfo(rules["CellsRegex"] as string);
            var tdsStrs = cellsRegexInfo.Matches(rowsStr);

            var cellValueRegexInfo = new RegexInfo(rules["CellValueRegex"] as string);

            var rows = new List<List<string>>();
            foreach (var tdsStr in tdsStrs) {
                var cells = new List<string>();
                cells.AddRange(cellValueRegexInfo.Matches(tdsStr));
                rows.Add(cells);
            }

            var dataJson = Json.Stringify(rows);

            var expression = string.Format("{0}\ngetClasses({1});", rules["GetClassesScript"] as string, dataJson);

            //File.WriteAllText(@"C:\test.txt", expression);

            var jsClassInfos = JScriptEvaluator.Evaluator.Eval(expression) as JSObject;
            var length = (int)jsClassInfos["length"];

            classInfos = new ClassInfo[length];

            for (var i = 0; i < length; i++) {
                var jsInfo = jsClassInfos[i] as JSObject;

                var info = new ClassInfo();
                info.Name = jsInfo["name"] as string;
                var jsClasses = jsInfo["classes"] as JSObject;

                var cLength = (int)jsClasses["length"];
                var classes = new SubClassInfo[cLength];

                for (var j = 0; j < cLength; j++) {
                    var jsCl = jsClasses[j] as JSObject;
                    classes[j] = new SubClassInfo() {
                        DayOfWeek = (int)jsCl["dayOfWeek"],
                        Location = jsCl["location"] as string,
                        Sessions = GetIntArray(jsCl["sessions"] as JSObject),
                        Teacher = jsCl["teacher"] as string,
                        Weeks = GetIntArray(jsCl["weeks"] as JSObject)
                    };
                }

                info.Classes = classes;
                classInfos[i] = info;
            }

        })();

        var context = HttpContext.Current;
        context.Response.ContentType = "application/json";
        context.Response.Write(Json.Stringify(classInfos));
    }

    private int[] GetIntArray(JSObject jsArray) {
        var length = (int)jsArray["length"];
        var array = new int[length];
        for (var i = 0; i < length; i++)
            array[i] = (int)(double)jsArray[i];
        return array;
    }

    private void SendRequest(HttpRequest request, Dictionary<string, object> rules, string baseUrl, string rulePrefix) {
        SendRequest(request, rules, rulePrefix, baseUrl, null, null);
    }

    private void SendRequest(HttpRequest request, Dictionary<string, object> rules, string baseUrl, string rulePrefix, Dictionary<string, object> tplData, Encoding encoding) {
        var query = string.Join("&", rules[rulePrefix + "Params"] as string[]);
        if (tplData != null)
            query = FillQueryTemplate(query, tplData, encoding);

        var url = (rules[rulePrefix + "Url"] as string).Replace("{BaseUrl}", baseUrl);
        var method = rules[rulePrefix + "Method"] as string;

        if (method == "POST") {
            request.Open(method, url);
            request.ContentType = "application/x-www-form-urlencoded";
            request.Send(query);
        }
        else {
            if (query != "")
                url += "?" + query;
            request.Open(method, url);
            request.Send();
        }
    }

    private string FillQueryTemplate(string template, Dictionary<string, object> data, Encoding encoding) {
        var tagRE = new Regex(@"\{(.+?)(?:\[(\d+)\])?\}");
        var result = tagRE.Replace(template, (match) => {
            var groups = match.Groups;
            var name = groups[1].Value;

            if (!data.ContainsKey(name))
                return match.Value;

            var isArray = groups[2].Success;

            if (isArray) {
                var array = data[name] as string[];
                var index = int.Parse(groups[2].Value);
                return HttpUtility.UrlEncode(array[index], encoding);
            }
            else return HttpUtility.UrlEncode(data[name] as string, encoding);

        });
        return result;
    }
}