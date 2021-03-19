using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CavemanTcp;

namespace HttpContextLite
{
    public class HttpServer
    {
        #region Public-Members

        /// <summary>
        /// Method to use for sending log messages.
        /// </summary>
        public Action<string> Logger = null;

        /// <summary>
        /// The port number on which to listen.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
        }

        #endregion

        #region Private-Members

        private string _Header = "[HttpServer] ";
        private int _Port = 8000;
        private int _DelayIntervalMs = 10;
        private int _ReadTimeoutMs = 5000;

        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private CancellationToken _Token;

        private CavemanTcpServer _TcpServer = null;
        private ConcurrentQueue<HttpContext> _PendingRequests = new ConcurrentQueue<HttpContext>();

        #endregion

        #region Constructors-and-Factories

        public HttpServer(int port)
        {
            _Port = port;
            _Token = _TokenSource.Token;
            _TcpServer = new CavemanTcpServer("127.0.0.1", _Port);
            _TcpServer.Settings.MonitorClientConnections = false;
            _TcpServer.Events.ClientConnected += ClientConnected;
            _TcpServer.Events.ClientDisconnected += ClientDisconnected;
        }

        #endregion

        #region Public-Methods

        public void Start()
        {
            Logger?.Invoke(_Header + "starting on port " + _Port);
            _TcpServer.Start();
        }

        public void Stop()
        {
            Logger?.Invoke(_Header + "stopping");
            _TcpServer.Stop();
        }

        public async Task<HttpContext> GetContextAsync()
        {
            HttpContext ctx = null;

            while (true)
            {
                if (_PendingRequests.TryDequeue(out ctx))
                {
                    return ctx;
                }
                else
                {
                    await Task.Delay(_DelayIntervalMs).ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region Private-Methods

        private async void ClientConnected(object sender, ClientConnectedEventArgs args)
        {
            DateTime startTime = DateTime.Now;

            string ipPort = args.IpPort;
            string ip = null;
            int port = 0;
            ParseIpPort(ipPort, out ip, out port);
            HttpContext ctx = null;
            Logger?.Invoke(_Header + "connection from " + ip + ":" + port);

            try
            {
                #region Retrieve-Headers

                StringBuilder sb = new StringBuilder();

                //                           123456789012345 6 7 8
                // minimum request 16 bytes: GET / HTTP/1.1\r\n\r\n
                int preReadLen = 18;
                ReadResult preReadResult = await _TcpServer.ReadWithTimeoutAsync(
                    _ReadTimeoutMs,
                    args.IpPort,
                    preReadLen,
                    _Token).ConfigureAwait(false);

                if (preReadResult.Status != ReadResultStatus.Success
                    || preReadResult.BytesRead != preReadLen
                    || preReadResult.Data == null
                    || preReadResult.Data.Length != preReadLen) return;

                sb.Append(Encoding.ASCII.GetString(preReadResult.Data));

                bool retrievingHeaders = true;
                while (retrievingHeaders)
                {
                    if (sb.ToString().EndsWith("\r\n\r\n"))
                    {
                        // end of headers detected
                        retrievingHeaders = false;
                    }
                    else
                    {
                        ReadResult addlReadResult = await _TcpServer.ReadWithTimeoutAsync(
                            _ReadTimeoutMs,
                            args.IpPort,
                            1,
                            _Token).ConfigureAwait(false);

                        if (addlReadResult.Status == ReadResultStatus.Success)
                        {
                            sb.Append(Encoding.ASCII.GetString(addlReadResult.Data));
                        }
                        else
                        {
                            return;
                        }
                    }
                }

                #endregion

                #region Build-Context

                ctx = new HttpContext(
                    ipPort,
                    _TcpServer.GetStream(ipPort),
                    sb.ToString());
                 
                if (_Settings.Debug.Requests)
                {
                    _Events.Logger?.Invoke(
                        _Header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                        ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                }

                #endregion

                #region Check-Access-Control

                if (!_Settings.AccessControl.Permit(ctx.Request.Source.IpAddress))
                {
                    _Events.HandleRequestDenied(this, new RequestEventArgs(ctx));

                    if (_Settings.Debug.AccessControl)
                    {
                        _Events.Logger?.Invoke(_Header + "request from " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " denied due to access control");
                    }

                    return;
                }

                #endregion

                #region Process-Preflight-Requests

                if (ctx.Request.Method == HttpMethod.OPTIONS)
                {
                    if (_Routes.Preflight != null)
                    {
                        if (_Settings.Debug.Routing)
                        {
                            _Events.Logger?.Invoke(
                                _Header + "preflight route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                        }

                        await _Routes.Preflight(ctx).ConfigureAwait(false);
                        return;
                    }
                }

                #endregion

                #region Pre-Routing-Handler

                bool terminate = false;
                if (_Routes.PreRouting != null)
                {
                    terminate = await _Routes.PreRouting(ctx).ConfigureAwait(false);
                    if (terminate)
                    {
                        if (_Settings.Debug.Routing)
                        {
                            _Events.Logger?.Invoke(
                                _Header + "prerouting terminated connection for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                        }

                        return;
                    }
                }

                #endregion

                #region Content-Routes

                if (ctx.Request.Method == HttpMethod.GET || ctx.Request.Method == HttpMethod.HEAD)
                {
                    if (_Routes.Content.Exists(ctx.Request.Url.WithoutQuery))
                    {
                        if (_Settings.Debug.Routing)
                        {
                            _Events.Logger?.Invoke(
                                _Header + "content route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                        }

                        await _Routes.ContentHandler.Process(ctx, _Token).ConfigureAwait(false);
                        return;
                    }
                }

                #endregion

                #region Static-Routes

                Func<HttpContext, Task> handler = _Routes.Static.Match(ctx.Request.Method, ctx.Request.Url.WithoutQuery);
                if (handler != null)
                {
                    if (_Settings.Debug.Routing)
                    {
                        _Events.Logger?.Invoke(
                            _Header + "static route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.WithoutQuery);
                    }

                    await handler(ctx).ConfigureAwait(false);
                    return;
                }

                #endregion

                #region Parameter-Routes

                Dictionary<string, string> parameters = null;
                handler = _Routes.Parameter.Match(ctx.Request.Method, ctx.Request.Url.WithoutQuery, out parameters);
                if (handler != null)
                {
                    ctx.Request.Url.Parameters = new Dictionary<string, string>(parameters);

                    if (_Settings.Debug.Routing)
                    {
                        _Events.Logger?.Invoke(
                            _Header + "parameter route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.WithoutQuery);
                    }

                    await handler(ctx).ConfigureAwait(false);
                    return;
                }

                #endregion

                #region Dynamic-Routes

                handler = _Routes.Dynamic.Match(ctx.Request.Method, ctx.Request.Url.WithoutQuery);
                if (handler != null)
                {
                    if (_Settings.Debug.Routing)
                    {
                        _Events.Logger?.Invoke(
                            _Header + "dynamic route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                    }

                    await handler(ctx).ConfigureAwait(false);
                    return;
                }

                #endregion

                #region Default-Route

                if (_Settings.Debug.Routing)
                {
                    _Events.Logger?.Invoke(
                        _Header + "default route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                        ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                }

                if (_Routes.Default != null)
                {
                    await _Routes.Default(ctx).ConfigureAwait(false);
                    return;
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                    ctx.Response.ContentType = _Pages.Default404Page.ContentType;
                    await ctx.Response.SendAsync(_Pages.Default404Page.Content, _Token).ConfigureAwait(false);
                }

                #endregion  
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception e)
            {
                ctx.Response.StatusCode = 500;
                ctx.Response.ContentType = _Pages.Default500Page.ContentType;
                await ctx.Response.SendAsync(_Pages.Default500Page.Content, _Token).ConfigureAwait(false);

                _Events.Logger?.Invoke(_Header + "exception: " + Environment.NewLine + SerializationHelper.SerializeJson(e, true));
                _Events.HandleException(this, new ExceptionEventArgs(ctx, e));
                return;
            }
            finally
            {
                _TcpServer.DisconnectClient(ipPort);

                if (ctx != null)
                {
                    double totalMs = TotalMsFrom(startTime);

                    _Events.HandleResponseSent(this, new ResponseEventArgs(ctx, totalMs));

                    if (_Settings.Debug.Responses)
                    {
                        _Events.Logger?.Invoke(
                            _Header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full + ": " +
                            ctx.Response.StatusCode + " [" + totalMs + "ms]");
                    }

                    if (ctx.Response.ContentLength != null)
                    {
                        _Statistics.AddSentPayloadBytes(Convert.ToInt64(ctx.Response.ContentLength));
                    }
                }
            }

            #endregion
        }

        private void ClientDisconnected(object sender, ClientDisconnectedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void ParseIpPort(string ipPort, out string ip, out int port)
        {
            if (String.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            ip = null;
            port = -1;

            int colonIndex = ipPort.LastIndexOf(':');
            if (colonIndex != -1)
            {
                ip = ipPort.Substring(0, colonIndex);
                port = Convert.ToInt32(ipPort.Substring(colonIndex + 1));
            }
        }

        #endregion
    }
}
