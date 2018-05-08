using System;
using System.Text;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;
using Grace.Runtime;

namespace Grace.Execution
{

    /// <summary>
    /// Connects Grace code to a WebSocket client in a browser.
    /// </summary>
    public class WebSocketServer
    {

        private static IPEndPoint endpoint = new IPEndPoint(IPAddress.Loopback,
                0x6367);

        private static string magicString = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private TcpListener server;

        private HashSet<string> allowedOrigins = new HashSet<string>();

        /// <summary>
        /// Open a web socket server and return the stream of
        /// its (eventual) connection.
        /// </summary>
        public WebSocketStream Start()
        {
            server = new TcpListener(endpoint);
            server.Start();
            Console.WriteLine("Server started on "
                    + endpoint.Address + ":" + endpoint.Port
                    + ", awaiting connection...");
            Console.WriteLine("You can either use a Grace WebSocket client, "
                    + "or visit http://"
                    + endpoint.Address + ":" + endpoint.Port
                    + " in your web browser.");
            var client = server.AcceptTcpClient();
            Console.WriteLine("Connection received from "
                    + ((IPEndPoint)client.Client.RemoteEndPoint).Address
                    + ".");
            var stream = client.GetStream();
            if (Handshake(stream))
            {
                var wss = new WebSocketStream(stream);
                return wss;
            }
            return Next();
        }

        /// <summary>
        /// After a disconnection, await a further connection and
        /// return the stream.
        /// </summary>
        public WebSocketStream Next()
        {
            Console.WriteLine("Connection closed. Server still open on "
                    + endpoint.Address + ":" + endpoint.Port
                    + ", awaiting connection...");
            var client = server.AcceptTcpClient();
            Console.WriteLine("Connection received from "
                    + ((IPEndPoint)client.Client.RemoteEndPoint).Address
                    + ".");
            var stream = client.GetStream();
            if (Handshake(stream))
            {
                var wss = new WebSocketStream(stream);
                return wss;
            }
            return Next();
        }

        private void httpServe(NetworkStream stream, string path,
                string mime)
        {
            var bases = Interpreter.GetStaticModulePaths();
            foreach (var p in bases)
            {
                string filePath = System.IO.Path.Combine(p, path);
                if (System.IO.File.Exists(filePath))
                {
                    var data = System.IO.File.ReadAllBytes(filePath);
                    Console.WriteLine("Serving " + path + " (" + data.Length
                            + " bytes) over HTTP...");
                    byte[] response = Encoding.ASCII.GetBytes(
                            "HTTP/1.0 200 OK\r\n"
                            + "Content-type: " + mime + "\r\n"
                            + "Connection: close\r\n"
                            + "\r\n");
                    stream.Write(response, 0, response.Length);
                    stream.Write(data, 0, data.Length);
                }
            }
            stream.Close();
        }


        private void httpServeCode(NetworkStream stream, int code)
        {
            Console.WriteLine("Serving " + code + " response over HTTP...");
            byte[] response = Encoding.ASCII.GetBytes(
                    "HTTP/1.0 " + code + "\r\n"
                    + "Connection: close\r\n"
                    + "\r\n");
            stream.Write(response, 0, response.Length);
            stream.Close();
        }

        /// <summary>
        /// Perform the web socket handshake on a base NetworkStream.
        /// </summary>
        /// <param name="stream">NetworkStream to handshake on</param>
        public bool Handshake(NetworkStream stream)
        {
            byte[] buffer = new byte[2048];
            // Await handshake
            int read = stream.Read(buffer, 0, buffer.Length);
            var str = Encoding.UTF8.GetString(buffer, 0, read);
            if (str.StartsWith("GET / HTTP/1"))
            {
                httpServe(stream, "websocket/index.html",
                        "text/html");
                return false;
            }
            if (str.StartsWith("GET /minigrace.js HTTP/1"))
            {
                httpServe(stream, "websocket/minigrace.js",
                        "text/javascript");
                return false;
            }
            if (!str.StartsWith("GET /grace HTTP/1"))
            {
                httpServeCode(stream, 404);
                return false;
            }
#if DEBUG_WS
            Console.WriteLine("Read: " + str);
#endif
            var lines = str.Split(new string[] {"\r\n"},
                    StringSplitOptions.None);
            string keyStr = null;
            bool originOK = true;
            foreach (var line in lines)
            {
                if (line.StartsWith("Sec-WebSocket-Key:",
                            StringComparison.OrdinalIgnoreCase))
                {
                    var bits = line.Split(new char[] { ':' });
                    keyStr = bits[1].Trim();
                }
                else if (line.StartsWith("Origin:"))
                {
                    var bits = line.Split(new char[] { ':' }, 2);
                    var origin = bits[1].Trim();
                    if (origin == "http://127.0.0.1:25447"
                            || origin.StartsWith("file://"))
                        originOK = true;
                    else if (allowedOrigins.Contains(origin))
                    {
                        originOK = true;
                        Console.WriteLine("Connection originates from "
                                + origin + "; remembered OK.");
                    }
                    else {
                        Console.WriteLine("Connection originating from "
                                + origin + "; OK? Y/n\u0007");
                        var resp = Console.ReadLine();
                        originOK = resp == "" || resp[0] == 'y'
                            || resp[0] == 'Y';
                        if (originOK)
                            allowedOrigins.Add(origin);
                    }
                }
            }
            if (keyStr != null && originOK) {
                var keyResponse = Convert.ToBase64String(
                        SHA1.Create().ComputeHash(
                                Encoding.ASCII.GetBytes(
                                    keyStr + magicString
                                )
                            )
                        );
                byte[] response = Encoding.ASCII.GetBytes(
                            "HTTP/1.1 101 Switching Protocols\r\n"
                            + "Connection: Upgrade\r\n"
                            + "Upgrade: websocket\r\n"
                            + "Sec-WebSocket-Accept: "
                                + keyResponse + "\r\n"
                            + "\r\n"
                        );
                stream.Write(response, 0, response.Length);
                Console.WriteLine("Accepted connection.");
            }
            else if (keyStr != null && !originOK)
            {
                Console.WriteLine("Rejected connection.");
            }
            return true;
        }
    }

    /// <summary>
    /// Event representing receiving text over a web socket
    /// stream.
    /// </summary>
    public class TextWSEvent : EventArgs
    {
        /// <summary>Received text</summary>
        public string Text { get; private set; }

        /// <param name="text">
        /// Text received in this event
        /// </param>
        public TextWSEvent(string text)
        {
            Text = text;
        }
    }

    /// <summary>
    /// Event representing receiving JSON over a web socket
    /// stream.
    /// </summary>
    public class JsonWSEvent : EventArgs
    {
        /// <summary>
        /// Root element of the JSON object.
        /// </summary>
        public XElement Root { get; private set; }

        /// <param name="root">
        /// Root element of the JSON object.
        /// </param>
        public JsonWSEvent(XElement root)
        {
            Root = root;
        }
    }

    /// <summary>
    /// An individual web socket stream to a single client.
    /// </summary>
    public class WebSocketStream
    {
        /// <summary>
        /// Number of frames sent in this session.
        /// </summary>
        public int SentFrames { get; private set; }

        /// <summary>
        /// Number of frames received in this session.
        /// </summary>
        public int ReceivedFrames { get; private set; }

        /// <summary>
        /// Handler when text is received.
        /// </summary>
        public delegate void TextReceivedHandler(object sender, EventArgs e);

        /// <summary>
        /// Handler when JSON is received.
        /// </summary>
        public delegate void JsonReceivedHandler(object sender, EventArgs e);

        /// <summary>
        /// Handler when text is received.
        /// </summary>
        public event TextReceivedHandler TextReceived;

        /// <summary>
        /// Handler when JSON is received.
        /// </summary>
        public event JsonReceivedHandler JsonReceived;

        NetworkStream stream;

        internal WebSocketStream(NetworkStream _stream)
        {
            stream = _stream;
        }

        private bool stop = false;
        /// <summary>
        /// Stop this stream after the completion of the next event to
        /// finish.
        /// </summary>
        public void Stop()
        {
            stop = true;
        }

        /// <summary>
        /// Consume incoming events from this stream and
        /// perform the appropriate callbacks.
        /// </summary>
        public void Run()
        {
            byte[] buffer = new byte[2048];
            while (!stop)
            {
                stream.Read(buffer, 0, 2);
                ReceivedFrames++;
                //var fin = (buffer[1] & 128) == 128;
                var op = buffer[0] & 0xf;
                uint plen = (uint)buffer[1] & 127;
                uint length;
#if DEBUG_WS
                Console.WriteLine("read something");
                Console.WriteLine("b1: " + buffer[1]);
                Console.WriteLine("plen: " + plen);
#endif
                if (plen <= 125)
                    length = plen;
                else if (plen == 126)
                {
                    byte[] buf = new byte[2];
                    stream.Read(buf, 0, 2);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(buf);
                    length = BitConverter.ToUInt16(buf, 0);
                }
                else
                {
                    byte[] buf = new byte[8];
                    stream.Read(buf, 0, 8);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(buf);
                    var tmp = BitConverter.ToUInt64(buf, 0);
                    if (tmp > int.MaxValue)
                        close();
                    length = (uint)tmp;
                }
                stream.Read(buffer, 0, 4);
                byte[] mask = new byte[4];
                Array.Copy(buffer, mask, 4);
#if DEBUG_WS
                Console.WriteLine("----------\nMessage:");
                Console.WriteLine("Op:  " + op);
                Console.WriteLine("Len: " + length);
                Console.WriteLine("Mask:" + String.Join(" ",
                        from b in mask
                        select b.ToString("X2")
                    )
                );
#endif
                byte[] pbuf = new byte[length];
                int offset = 0;
                while (offset < length)
                    offset += stream.Read(pbuf, offset, (int)(length - offset));
                for (int i = 0; i < offset; i++)
                    pbuf[i] ^= mask[i % 4];
                if (op == 1)
                    gotText(pbuf);
                if (op == 8)
                {
                    close();
                    stream.Close();
                    return;
                }
                if (op == 9)
                    gotPing(pbuf);
                if (op == 10)
                    Console.WriteLine("PONG");
            }
        }

        private void gotPing(byte[] data)
        {
            pong(data);
        }

        private void gotJSON(byte[] data)
        {
            var jsonReader = JsonReaderWriterFactory.CreateJsonReader(data,
                    new System.Xml.XmlDictionaryReaderQuotas());
            var root = XElement.Load(jsonReader);
            if (JsonReceived != null)
                JsonReceived(this, new JsonWSEvent(root));
        }

        private void gotText(byte[] data)
        {
            var str = Encoding.UTF8.GetString(data, 0, data.Length);
#if DEBUG_WS
            Console.WriteLine("Body: " + str);
            Console.WriteLine("has textreceived? " + (TextReceived != null));
#endif
            if (TextReceived != null)
                TextReceived(this, new TextWSEvent(str));
            if (data[0] == '{')
                gotJSON(data);
        }

        private void sendFrame(byte opcode, byte[] data)
        {
            SentFrames++;
            byte[] pkt;
            // Server-sent frames are unmasked, and so are encoded as:
            // 1 bit    FIN
            // 7 bits   OPCODE
            // 1 bit    unmasked (0)
            // 7 bits   LENGTH
            // ?2 bytes 16-bit length
            // This implementation does not yet support longer
            // frames.
            if (data.Length < 126)
            {
                pkt = new byte[data.Length + 2];
                // FIN | opcode
                pkt[0] = (byte)(128 | opcode);
                // !MASK | real length
                pkt[1] = (byte)(data.Length);
                Array.Copy(data, 0, pkt, 2, data.Length);
            }
            else if (data.Length < 65536)
            {
                pkt = new byte[data.Length + 4];
                // FIN | opcode
                pkt[0] = (byte)(128 | opcode);
                // !MASK | 126 (16-bit length)
                pkt[1] = (byte)(126);
                var blen = BitConverter.GetBytes((UInt16)data.Length);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(blen);
                Array.Copy(blen, 0, pkt, 2, 2);
                Array.Copy(data, 0, pkt, 4, data.Length);
            }
            else
            {
                pkt = new byte[data.Length + 10];
                // FIN | opcode
                pkt[0] = (byte)(128 | opcode);
                // !MASK | 126 (16-bit length)
                pkt[1] = (byte)(127);
                var blen = BitConverter.GetBytes((UInt64)data.Length);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(blen);
                Array.Copy(blen, 0, pkt, 2, 8);
                Array.Copy(data, 0, pkt, 10, data.Length);
            }
            try
            {
                stream.Write(pkt, 0, pkt.Length);
            }
            catch (ObjectDisposedException)
            {
                throw new WebSocketClosedException();
            }
#if DEBUG_WS
            Console.WriteLine("Sent frame: " + opcode
                    + " (" + data.Length + "):");
            Console.WriteLine(String.Join(" ", from x in pkt select x.ToString("X2")));
#endif
        }

        private void ping()
        {
            sendFrame(9, new byte[0]);
        }

        private void pong(byte[] data)
        {
            sendFrame(10, data);
        }

        private void close()
        {
            close(new byte[0]);
        }

        private void close(byte[] data)
        {
            sendFrame(8, data);
        }

        /// <summary>
        /// Send a given string as a web socket data frame
        /// to the client.
        /// </summary>
        /// <param name="message">
        /// String to send
        /// </param>
        public void Send(string message)
        {
            var utf8 = new UTF8Encoding(false, true);
            var data = utf8.GetBytes(message);
            sendFrame(1, data);
        }
    }

    /// <summary>
    /// Marker exception for a closed socket.
    /// </summary>
    public class WebSocketClosedException : Exception
    {}

    /// <summary>
    /// Interface for classes wrapping an RPC endpoint.
    /// </summary>
    public interface RPCSink
    {
        /// <summary>
        /// Send an RPC message to the remote client.
        /// </summary>
        /// <param name="receiver">
        /// Unique identifier of the object to receive the message
        /// on the remote end.
        /// </param>
        /// <param name="name">
        /// Name of the message to send.
        /// </param>
        /// <param name="args">
        /// Arguments to the message.
        /// </param>
        GraceObject SendRPC(int receiver, string name, object[] args);

        /// <summary>
        /// Send an RPC message to the remote client, ignoring any
        /// result and returning done immediately.
        /// </summary>
        /// <param name="receiver">
        /// Unique identifier of the object to receive the message
        /// on the remote end.
        /// </param>
        /// <param name="name">
        /// Name of the message to send.
        /// </param>
        /// <param name="args">
        /// Arguments to the message.
        /// </param>
        GraceObject SendRPCNoResult(int receiver, string name, object[] args);

        /// <summary>
        /// Attempt to wait for a callback or return value from
        /// the remote client.
        /// </summary>
        /// <param name="time">
        /// Timeout to use when waiting.
        /// </param>
        /// <param name="block">
        /// Grace block associated with the callback.
        /// </param>
        /// <param name="args">
        /// Arguments associated with the callback.
        /// </param>
        bool AwaitRemoteCallback(int time,
                out GraceObject block, out object[] args);

        /// <summary>
        /// Terminate this RPC connection.
        /// </summary>
        void Stop();

        /// <summary>
        /// Terminate this RPC connection, harder.
        /// </summary>
        void HardStop();

        /// <summary>
        /// Send a generic event over the wire.
        /// </summary>
        /// <param name="eventName">
        /// Identifying name of the event.
        /// </param>
        /// <param name="key">
        /// Key to identify event.
        /// </param>
        void SendEvent(string eventName, string key);

        /// <summary>True if this RPC sink has stopped</summary>
        bool Stopped { get; }
    }
}
