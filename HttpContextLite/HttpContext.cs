using System;
using System.IO;
using System.Threading;
using Grapevine;
using Grapevine.Internals;

namespace HttpContextLite
{
    public class HttpContext : IHttpContext
    {
        #region Public-Members

        public CancellationToken CancellationToken => throw new NotImplementedException();

        public string Id => throw new NotImplementedException();

        public bool WasRespondedTo => throw new NotImplementedException();

        public IHttpRequest Request
        {
            get
            {
                return _Request;
            }
        }

        public IHttpResponse Response
        {
            get
            {
                return _Response;
            }
        }

        public IServiceProvider Services
        {
            get
            {
                return _ServiceProvider;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        #endregion

        #region Private-Members

        private string _IpPort = null;
        private string _SourceIp = null;
        private int _SourcePort = 0;
        private string _HeaderString = null;
        private int _StreamBufferSize = 65536;
        private Stream _Stream = null;

        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private CancellationToken _Token;
        private HttpRequest _Request = new HttpRequest();
        private HttpResponse _Response = new HttpResponse();
        private ServiceProvider _ServiceProvider = new ServiceProvider();

        #endregion

        #region Constructors-and-Factories

        public HttpContext()
        {
            _Token = _TokenSource.Token;
        }

        public HttpContext(string ipPort, Stream stream, string headerStr, int streamBufferSize = 65536)
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

            _Request = new HttpRequest(_IpPort, _Stream, _HeaderString, _StreamBufferSize);
            _Response = new HttpResponse(_IpPort, _Request, _Stream, _StreamBufferSize);
        }

        #endregion

        #region Public-Methods

        public bool Contains(string key)
        {
            throw new NotImplementedException();
        }

        public object Get(string key)
        {
            throw new NotImplementedException();
        }

        public void Set(string key, object val)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
