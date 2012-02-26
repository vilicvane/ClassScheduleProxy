using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text.RegularExpressions;
using System.IO;

namespace ClassScheduleProxy {
    public static class Solution {
        public static DateTime lastRulesCacheTime = new DateTime(0);
        public static DateTime lastInfoCacheTime = new DateTime(0);
        private static Dictionary<string, Dictionary<string, object>> rulesCache = new Dictionary<string, Dictionary<string, object>>();
        private static Dictionary<int, SolutionInfo> solutionInfoCache = new Dictionary<int, SolutionInfo>();
        private static Regex itemRE = new Regex(@"\[(.+?)\](?:\n(?:(.+?)\n)?|\{""\n([\s\S]*?)\n""\}\n|(\[\])\n(?:(.[\s\S]*?)\n)?)(?:\n|$)");

        public static Dictionary<string, object> GetRules(string solution) {
            var now = DateTime.Now;
            if ((now - lastRulesCacheTime).TotalMinutes > 15) {
                rulesCache.Clear();
                lastRulesCacheTime = now;
            }

            if (rulesCache.ContainsKey(solution))
                return rulesCache[solution];

            var path = HttpContext.Current.Server.MapPath("Universities/Rules/" + solution);

            var text = File.ReadAllText(path).Replace("\r\n", "\n");

            var rules = new Dictionary<string, object>();
            var matches = itemRE.Matches(text);

            foreach (Match match in matches) {
                var groups = match.Groups;
                var name = groups[1].Value;
                if (groups[2].Success)
                    rules[name] = groups[2].Value;
                else if (groups[3].Success)
                    rules[name] = groups[3].Value;
                else if (groups[5].Success)
                    rules[name] = groups[5].Value.Split('\n');
                else if (groups[4].Success) rules[name] = new string[0];
                else rules[name] = "";
            }

            rulesCache[solution] = rules;
            return rules;
        }

        public static SolutionInfo GetSolutionInfo(int universityId) {
            var now = DateTime.Now;
            if ((now - lastInfoCacheTime).TotalMinutes > 15) {
                solutionInfoCache.Clear();
                lastInfoCacheTime = now;
            }

            if (solutionInfoCache.ContainsKey(universityId))
                return solutionInfoCache[universityId];

            var reader = File.OpenText(HttpContext.Current.Server.MapPath("Universities/List"));
            var start = universityId + ";";
            while (!reader.EndOfStream) {
                var line = reader.ReadLine();
                if (line.StartsWith(start)) {
                    var items = line.Split(';');
                    var info = new SolutionInfo() {
                        Name = items[1],
                        BaseUrl = items[3]
                    };
                    solutionInfoCache[universityId] = info;
                    return info;
                }
            }

            return null;
        }

        public class SolutionInfo {
            public string Name { get; set; }
            public string BaseUrl { get; set; }
            public Dictionary<string, object> Rules { get { return GetRules(Name); } }
        }


    }
}