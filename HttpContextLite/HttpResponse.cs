using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grapevine;
using Grapevine.Internals;

namespace HttpContextLite
{
    public class HttpResponse : IHttpResponse
    {
        #region Public-Members

        public Encoding ContentEncoding { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public TimeSpan ContentExpiresDuration { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public long ContentLength64 { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string ContentType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public CookieCollection Cookies { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public WebHeaderCollection Headers { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string RedirectLocation { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public bool ResponseSent => throw new NotImplementedException();

        public int StatusCode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string StatusDescription { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool SendChunked { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        #endregion

        #region Private-Members

        private HttpRequest _Request = null;
        private string _IpPort = null;
        private string _SourceIp = null;
        private int _SourcePort = 0;
        private int _StreamBufferSize = 65536;
        private Stream _Stream = null;

        #endregion

        #region Constructors-and-Factories

        public HttpResponse()
        {

        }

        public HttpResponse(string ipPort, HttpRequest request, Stream stream, int streamBufferSize = 65536)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite) throw new ArgumentException("Cannot write to the supplied stream.");
            if (streamBufferSize < 1) throw new ArgumentException("Stream buffer size must be greater than zero.");

            _IpPort = ipPort;
            _Request = request;
            _Stream = stream;
            _StreamBufferSize = streamBufferSize;

            string ip = null;
            int port = 0;
            HttpCommon.ParseIpPort(_IpPort, out ip, out port);
            _SourceIp = ip;
            _SourcePort = port;
        }

        #endregion

        #region Public-Methods

        public void Abort()
        {
            throw new NotImplementedException();
        }

        public void AddHeader(string name, string value)
        {
            throw new NotImplementedException();
        }

        public void AppendCookie(Cookie cookie)
        {
            throw new NotImplementedException();
        }

        public void AppendHeader(string name, string value)
        {
            throw new NotImplementedException();
        }

        public void Redirect(string url)
        {
            throw new NotImplementedException();
        }

        public Task SendResponseAsync(byte[] contents)
        {
            throw new NotImplementedException();
        }

        public void SetCookie(Cookie cookie)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
