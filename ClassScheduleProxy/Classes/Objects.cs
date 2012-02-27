using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Text.RegularExpressions;
using System.Runtime.Serialization.Json;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.JScript;

namespace ClassScheduleProxy {
    public class Session {
        public string StartTime;
        public string EndTime;
    }

    public class SessionPeriod {
        public string Name;
        public Session[] Sessions;
    }

    public class UniversityInfo {
        internal UniversityInfo() { }
        public int Id;
        public string Name;
        public bool HasVerifier;
        public DateTime FirstWeek;
        public int WeekCount;
        public SessionPeriod[] SessionPeriods;
    }

    public class UniversityListInfo {
        internal UniversityListInfo() { }
        public int Id;
        public string Name;
    }

    public class SubClassInfo {
        public string Teacher;
        public int[] Weeks;
        public int DayOfWeek;
        public int[] Sessions;
        public string Location;
    }

    public class ClassInfo {
        internal ClassInfo() { }
        public string Name;
        public SubClassInfo[] Classes;
    }

    public static class Json {
        public static string Stringify(object @object) {
            var serializer = new DataContractJsonSerializer(@object.GetType());
            var stream = new MemoryStream();
            serializer.WriteObject(stream, @object);
            stream.Position = 0;
            return new StreamReader(stream).ReadToEnd();
        }

        public static T Parse<T>(string json) {
            var serializer = new DataContractJsonSerializer(typeof(T));
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            stream.Position = 0;
            var @object = serializer.ReadObject(stream);
            return (T)@object;
        }
    }

    public class RegexInfo {
        public Regex Regex { get; private set; }
        public int[] GroupNumbers { get; private set; }

        public RegexInfo(string info) {
            var infoGroups = new Regex(@"^(.+)\[(\d+(?:,\d+)*)\]$").Match(info).Groups;
            Regex = new Regex(infoGroups[1].Value);
            var numStrs = infoGroups[2].Value.Split(',');

            GroupNumbers = new int[numStrs.Length];

            for (var i = 0; i < numStrs.Length; i++)
                GroupNumbers[i] = int.Parse(numStrs[i]);
        }

        public string Match(string text) {
            var groups = Regex.Match(text).Groups;

            foreach (var i in GroupNumbers)
                if (groups[i].Success)
                    return groups[i].Value;

            return "";
        }

        public string[] Matches(string text) {
            var matches = Regex.Matches(text);

            var strs = new string[matches.Count];

            for (var i = 0; i < strs.Length; i++) {
                var groups = matches[i].Groups;
                foreach (var j in GroupNumbers)
                    if (groups[j].Success) {
                        strs[i] = groups[j].Value;
                        break;
                    }
            }

            return strs.ToArray();
        }
    }
}