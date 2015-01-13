﻿using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using HttpMachine;
using Middleman.Server.Connection;
using Middleman.Server.Utils;
using NLog;

namespace Middleman.Server.Request
{
    internal class MiddlemanRequestParser
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public async Task<MiddlemanRequest> ParseAsync(InboundConnection conn, Stream stream)
        {
            var del = new ParseDelegate();
            var parser = new HttpParser(del);

            int read;
            var readTotal = 0;
            var buffer = new byte[8192];

            Log.Debug("{0}: RequestParser starting", conn.RemoteEndPoint);

            var requestString = "";

            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                requestString += Encoding.GetEncoding("us-ascii").GetString(buffer, 0, read);
                readTotal += read;

                if (parser.Execute(new ArraySegment<byte>(buffer, 0, read)) != read)
                    throw new FormatException("Parse error in request");

                if (del.HeaderComplete)
                    break;
            }

            Log.Debug("{0}: RequestParser read enough ({1} bytes)", conn.RemoteEndPoint, readTotal);
            Log.Debug(requestString);

            if (readTotal == 0)
                return null;

            if (!del.HeaderComplete)
                throw new FormatException("Parse error in request");

            var request = del.Request;

            request.ProtocolVersion = new Version(parser.MajorVersion, parser.MinorVersion);

            var cl = request.ContentLength;

            if (cl > 0)
            {
                request.RequestBody = del.RequestBodyStart.Count > 0
                    ? new MaxReadStream(new StartAvailableStream(del.RequestBodyStart, stream), cl)
                    : new MaxReadStream(stream, cl);
            }

            return request;
        }

        private sealed class ParseDelegate : IHttpParserHandler
        {
            private string _headerName;
            public bool HeaderComplete;
            public ArraySegment<byte> RequestBodyStart;
            public readonly MiddlemanRequest Request = new MiddlemanRequest();

            void IHttpParserHandler.OnBody(HttpParser parser, ArraySegment<byte> data)
            {
                RequestBodyStart = data;
            }

            void IHttpParserHandler.OnFragment(HttpParser parser, string fragment)
            {
            }

            void IHttpParserHandler.OnHeaderName(HttpParser parser, string name)
            {
                _headerName = name;
            }

            void IHttpParserHandler.OnHeaderValue(HttpParser parser, string value)
            {
                Request.Headers.Add(_headerName, value);
            }

            void IHttpParserHandler.OnHeadersEnd(HttpParser parser)
            {
                HeaderComplete = true;
            }

            void IHttpParserHandler.OnMessageBegin(HttpParser parser)
            {
            }

            void IHttpParserHandler.OnMessageEnd(HttpParser parser)
            {
            }

            void IHttpParserHandler.OnMethod(HttpParser parser, string method)
            {
                Request.Method = method;
            }

            void IHttpParserHandler.OnQueryString(HttpParser parser, string queryString)
            {
            }

            void IHttpParserHandler.OnRequestUri(HttpParser parser, string requestUri)
            {
                Request.RequestUri = requestUri;
            }
        }
    }
}