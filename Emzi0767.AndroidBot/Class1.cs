using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using DSharpPlus.Net.WebSocket;
using ws4net = WebSocket4Net;
using s = System;

namespace DSharpPlus.Net.WebSocket
{
    public class WebSocket4NetClient : BaseWebSocketClient
    {
        internal static UTF8Encoding UTF8 { get; } = new UTF8Encoding(false);
        internal ws4net.WebSocket _socket;

        public WebSocket4NetClient()
        {
            this._connect = new AsyncEvent(this.EventErrorHandler, "WS_CONNECT");
            this._disconnect = new AsyncEvent<SocketCloseEventArgs>(this.EventErrorHandler, "WS_DISCONNECT");
            this._message = new AsyncEvent<SocketMessageEventArgs>(this.EventErrorHandler, "WS_MESSAGE");
            this._error = new AsyncEvent<SocketErrorEventArgs>(null, "WS_ERROR");
        }

        public override Task<BaseWebSocketClient> ConnectAsync(string uri)
        {
            _socket = new ws4net.WebSocket(uri);

            _socket.Opened += (sender, e) => _connect.InvokeAsync().GetAwaiter().GetResult();

            _socket.Closed += (sender, e) =>
            {
                var sock = (SocketCloseEventArgs)Activator.CreateInstance(typeof(SocketCloseEventArgs), null);
                
                if (e is ws4net.ClosedEventArgs ea)
                {
                    typeof(SocketCloseEventArgs).GetProperty("CloseCode").SetValue(sock, ea.Code);
                    typeof(SocketCloseEventArgs).GetProperty("CloseMessage").SetValue(sock, ea.Reason);
                    _disconnect.InvokeAsync(sock).GetAwaiter().GetResult();
                }
                else
                {
                    typeof(SocketCloseEventArgs).GetProperty("CloseCode").SetValue(sock, -1);
                    typeof(SocketCloseEventArgs).GetProperty("CloseMessage").SetValue(sock, "unknown");
                    _disconnect.InvokeAsync(sock).GetAwaiter().GetResult();
                }
            };

            _socket.MessageReceived += (sender, e) =>
            {
                var sock = (SocketMessageEventArgs)Activator.CreateInstance(typeof(SocketMessageEventArgs), null);
                typeof(SocketCloseEventArgs).GetProperty("Message").SetValue(sock, e.Message);
                _message.InvokeAsync(sock).GetAwaiter().GetResult();
            };

            _socket.DataReceived += (sender, e) =>
            {
                var msg = "";

                using (var ms1 = new MemoryStream(e.Data, 2, e.Data.Length - 2))
                using (var ms2 = new MemoryStream())
                {
                    using (var zlib = new DeflateStream(ms1, CompressionMode.Decompress))
                        zlib.CopyTo(ms2);

                    msg = UTF8.GetString(ms2.ToArray(), 0, (int)ms2.Length);
                }

                var sock = (SocketMessageEventArgs)Activator.CreateInstance(typeof(SocketMessageEventArgs), null);
                typeof(SocketCloseEventArgs).GetProperty("Message").SetValue(sock, msg);
                _message.InvokeAsync(sock).GetAwaiter().GetResult();
            };

            _socket.Open();

            return Task.FromResult<BaseWebSocketClient>(this);
        }

        public override Task InternalDisconnectAsync(SocketCloseEventArgs e)
        {
            if (_socket.State != ws4net.WebSocketState.Closed)
                _socket.Close();
            return Task.Delay(0);
        }

        public override Task<BaseWebSocketClient> OnConnectAsync()
        {
            return Task.FromResult<BaseWebSocketClient>(this);
        }

        public override Task<BaseWebSocketClient> OnDisconnectAsync(SocketCloseEventArgs e)
        {
            return Task.FromResult<BaseWebSocketClient>(this);
        }

        public override void SendMessage(string message)
        {
            if (_socket.State == ws4net.WebSocketState.Open)
                _socket.Send(message);
        }

        public override event AsyncEventHandler OnConnect
        {
            add => this._connect.Register(value);
            remove => this._connect.Unregister(value);
        }
        private AsyncEvent _connect;

        public override event AsyncEventHandler<SocketCloseEventArgs> OnDisconnect
        {
            add => this._disconnect.Register(value);
            remove => this._disconnect.Unregister(value);
        }
        private AsyncEvent<SocketCloseEventArgs> _disconnect;

        public override event AsyncEventHandler<SocketMessageEventArgs> OnMessage
        {
            add => this._message.Register(value);
            remove => this._message.Unregister(value);
        }
        private AsyncEvent<SocketMessageEventArgs> _message;

        public override event AsyncEventHandler<SocketErrorEventArgs> OnError
        {
            add => this._error.Register(value);
            remove => this._error.Unregister(value);
        }
        private AsyncEvent<SocketErrorEventArgs> _error;

        private void EventErrorHandler(string evname, Exception ex)
        {
            if (evname.ToLowerInvariant() == "ws_error")
            {
                Console.WriteLine($"WSERROR: {ex.GetType()} in {evname}!");
            }
            else
            {
                var sock = (SocketErrorEventArgs)Activator.CreateInstance(typeof(SocketErrorEventArgs), null);
                typeof(SocketCloseEventArgs).GetProperty("Exception").SetValue(sock, ex);
                this._error.InvokeAsync(sock).GetAwaiter().GetResult();
            }
        }
    }
}