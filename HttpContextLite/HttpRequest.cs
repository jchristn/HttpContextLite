using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Grapevine;
using Grapevine.Internals;

namespace HttpContextLite
{
    public class HttpRequest : IHttpRequest
    {
        #region Public-Members

        public string[] AcceptTypes { get; private set; } = null;
        public long ContentLength64 { get; private set; } = 0;
        public string ContentType { get; private set; } = null;
        public CookieCollection Cookies { get; private set; } = new CookieCollection();
        public NameValueCollection Headers
        {
            get
            {
                return _Headers;
            }
        }
        public HttpMethod HttpMethod
        {
            get
            {
                return _HttpMethod;
            }
        }
        public Stream InputStream
        {
            get
            {
                return _Stream;
            }
        }
        public NameValueCollection QueryString
        {
            get
            {
                return _QueryString;
            }
        }
        public IPEndPoint RemoteEndPoint
        {
            get
            {
                return _RemoteEndPoint;
            }
        }
        public Uri Url { get; private set; } = null;
        public Uri UrlReferrer { get; private set; } = null;
        public string UserAgent { get; private set; }
        public string[] UserLanguages { get; private set; } = null;

        public Encoding ContentEncoding => throw new NotImplementedException();

        public string HostPrefix => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public string Endpoint => throw new NotImplementedException();

        public IDictionary<string, string> PathParameters { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public string RawUrl => throw new NotImplementedException();

        public string UserHostAddress => throw new NotImplementedException();

        public string UserHostname => throw new NotImplementedException();

        #endregion

        #region Private-Members

        private string _IpPort = null;
        private string _SourceIp = null;
        private int _SourcePort = 0;
        private string _HeaderString = null;
        private int _StreamBufferSize = 65536;
        private Stream _Stream = null;

        private IPEndPoint _RemoteEndPoint = null;
        private NameValueCollection _Headers = new NameValueCollection();
        private Grapevine.HttpMethod _HttpMethod = new HttpMethod("GET");
        private NameValueCollection _QueryString = new NameValueCollection();

        #endregion

        #region Constructors-and-Factories

        public HttpRequest()
        {

        }

        public HttpRequest(string ipPort, Stream stream, string headerStr, int streamBufferSize = 65536)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (String.IsNullOrEmpty(headerStr)) throw new ArgumentNullException(nameof(headerStr));
            if (streamBufferSize < 1) throw new ArgumentException("Stream buffer size must be greater than zero.");

            _IpPort = ipPort;
            _Stream = stream;
            _HeaderString = headerStr;
            _StreamBufferSize = streamBufferSize;

            string ip = null;
            int port = 0;
            HttpCommon.ParseIpPort(_IpPort, out ip, out port);
            _SourceIp = ip;
            _SourcePort = port;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        private void Build()
        {
            _RemoteEndPoint = new IPEndPoint(
                IPAddress.Parse(HttpCommon.IpFromIpPort(_IpPort)), 
                HttpCommon.PortFromIpPort(_IpPort));

            #region Headers

            string[] headers = _HeaderString.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (headers != null && headers.Length > 0)
            {
                for (int i = 0; i < headers.Length; i++)
                {
                    if (i == 0)
                    {
                        #region First-Line

                        string[] requestLine = headers[i].Trim().Trim('\0').Split(' ');
                        if (requestLine.Length < 3) throw new ArgumentException("Request line does not contain at least three parts (method, raw URL, protocol/version).");

                        _HttpMethod = (HttpMethod)Enum.Parse(typeof(HttpMethod), requestLine[0], true);

                        Url = new Uri(requestLine[1]);

                        if (requestLine[1].Contains("?"))
                        {
                            // 012345
                            // /foo?hello=world

                            string query = requestLine[1].Substring(requestLine[1].IndexOf("?"));
                            while (query.StartsWith("?")) query = query.Substring(1);

                            string[] queryParts = query.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                            if (queryParts.Length > 0)
                            {
                                foreach (string queryPart in queryParts)
                                {
                                    if (queryPart.Contains("="))
                                    {
                                        string[] currQuery = queryPart.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                                        if (currQuery.Length == 1) _QueryString.Add(currQuery[0], null);
                                        else _QueryString.Add(currQuery[0], currQuery[1]);
                                    }
                                    else
                                    {
                                        _QueryString.Add(queryPart, null);
                                    }
                                }
                            }
                        }

                        #endregion
                    }
                    else
                    {
                        #region Subsequent-Line

                        string[] headerLine = headers[i].Split(':');
                        if (headerLine.Length == 2)
                        {
                            string key = headerLine[0].Trim();
                            string val = headerLine[1].Trim();

                            if (String.IsNullOrEmpty(key)) continue;

                            _Headers.Add(key, val);

                            string keyEval = key.ToLower();

                            if (keyEval.Equals("accept"))
                            {
                                if (!String.IsNullOrEmpty(val))
                                {
                                    string[] acceptParts = val.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (acceptParts != null)
                                    {
                                        List<string> acceptList = new List<string>();
                                        foreach (string acceptPart in acceptParts)
                                        {
                                            string acceptPartTrimmed = acceptPart.Trim();
                                            if (!String.IsNullOrEmpty(acceptPartTrimmed)) acceptList.Add(acceptPartTrimmed);
                                        }
                                        AcceptTypes = acceptList.ToArray();
                                    }
                                }
                            }
                            else if (keyEval.Equals("accept-language"))
                            {
                                if (!String.IsNullOrEmpty(val))
                                {
                                    string[] acceptParts = val.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (acceptParts != null)
                                    {
                                        List<string> acceptList = new List<string>();
                                        foreach (string acceptPart in acceptParts)
                                        {
                                            string acceptPartTrimmed = acceptPart.Trim();
                                            if (!String.IsNullOrEmpty(acceptPartTrimmed)) acceptList.Add(acceptPartTrimmed);
                                        }
                                        UserLanguages = acceptList.ToArray();
                                    }
                                }
                            }
                            else if (keyEval.Equals("cookie"))
                            {
                                if (!String.IsNullOrEmpty(val))
                                {
                                    string[] cookieParts = val.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (cookieParts != null)
                                    {
                                        foreach (string cookie in cookieParts)
                                        {
                                            string[] cookiePart = cookie.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                                            if (cookiePart.Length == 1) Cookies.Add(new Cookie(cookiePart[0], null));
                                            else Cookies.Add(new Cookie(cookiePart[0], cookiePart[1]));
                                        }
                                    }
                                }
                            }
                            else if (keyEval.Equals("user-agent"))
                            {
                                UserAgent = val;
                            }
                            else if (keyEval.Equals("referer"))
                            {
                                if (!String.IsNullOrEmpty(val))
                                {
                                    UrlReferrer = new Uri(val);
                                }
                            }
                            else if (keyEval.Equals("content-length"))
                            {
                                ContentLength64 = Convert.ToInt32(val);
                            }
                            else if (keyEval.Equals("content-type"))
                            {
                                ContentType = val;
                            }
                        }

                        #endregion
                    }
                }
            }

            #endregion
        }

        #endregion
    }
}
