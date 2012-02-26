using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using System.Collections.Specialized;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace ClassScheduleProxy {
    public class HttpRequest {
        private HttpWebRequest request;
        private HttpWebResponse response;
        private Encoding encoding;

        public CookieContainer CookieContainer { get; set; }

        public WebHeaderCollection RequestHeaders { get { return request.Headers; } }
        public WebHeaderCollection ResponseHeaders { get { return response.Headers; } }
        public string ContentType { get { return request.ContentType; } set { request.ContentType = value; } }

        public HttpStatusCode StatusCode { get { return response.StatusCode; } }
        public string ResponseText { get { return new StreamReader(ResponseStream, encoding).ReadToEnd(); } }
        public Stream ResponseStream { get { return response.GetResponseStream(); } }

        public HttpRequest() {
            CookieContainer = new CookieContainer();
        }

        public void Open(string method, string url) {
            request = WebRequest.Create(url) as HttpWebRequest;
            request.Method = method;
            request.CookieContainer = CookieContainer;

            response = null;
        }

        public void Send(NameValueCollection data, Encoding encoding) {
            request.ContentType = "application/x-www-form-urlencoded";
            Send(GetQueryString(data, encoding));
        }

        public void Send(string data) {
            var bytes = Encoding.ASCII.GetBytes(data);
            Send(bytes);
        }

        public void Send() {
            Send(new byte[0]);
        }

        public void Send(byte[] bytes) {
            if (bytes.Length > 0) {
                var reqStream = request.GetRequestStream();
                reqStream.Write(bytes, 0, bytes.Length);
            }

            try {
                response = request.GetResponse() as HttpWebResponse;
            }
            catch (WebException e) {
                response = e.Response as HttpWebResponse;
            }

            var contentType = response.Headers[HttpResponseHeader.ContentType];
            encoding = Encoding.GetEncoding(new Regex(@"(?:^|[^\w])charset=(.+?)(?:;|$)", RegexOptions.IgnoreCase).Match(contentType).Groups[1].Value);
        }

        public static string GetQueryString(NameValueCollection data, Encoding encoding) {
            var itemStrs = new List<string>();
            foreach (var key in data.AllKeys)
                itemStrs.Add(HttpUtility.UrlEncode(key, encoding) + "=" + HttpUtility.UrlEncode(data[key], encoding));
            return string.Join("=", itemStrs);
        }

        public static string GetQueryString(NameValueCollection data) {
            return GetQueryString(data, Encoding.UTF8);
        }
    }
}