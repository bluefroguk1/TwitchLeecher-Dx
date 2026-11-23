using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace TwitchLeecher.Services.Services
{
    /// <summary>
    /// Drop-in replacement for the obsolete WebClient. Per-request headers and query string
    /// live on this lightweight object, the connections live in one shared HttpClient.
    /// Non-success status codes throw HttpRequestException with StatusCode set (the equivalent
    /// of WebExceptionStatus.ProtocolError); network failures and timeouts throw it with a null StatusCode.
    /// </summary>
    internal sealed class HttpRequestClient : IDisposable
    {
        // Default timeout (100s) matches what WebClient used
        private static readonly HttpClient _httpClient = new HttpClient();

        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>();
        private readonly List<KeyValuePair<string, string>> _query = new List<KeyValuePair<string, string>>();
        private HttpResponseMessage _lastResponse;

        public void AddHeader(string name, string value) => _headers[name] = value;

        public void AddQuery(string key, string value) => _query.Add(new KeyValuePair<string, string>(key, value));

        // response headers first, then content headers (Content-Type, Content-Length live there)
        public string GetResponseHeader(string name)
        {
            if (_lastResponse == null)
            {
                return null;
            }

            if (_lastResponse.Headers.TryGetValues(name, out IEnumerable<string> values) ||
                _lastResponse.Content.Headers.TryGetValues(name, out values))
            {
                return string.Join(",", values);
            }

            return null;
        }

        public string DownloadString(string url) => Encoding.UTF8.GetString(Send(HttpMethod.Get, url, null, CancellationToken.None));

        public byte[] DownloadData(string url, CancellationToken cancellationToken = default) =>
            Send(HttpMethod.Get, url, null, cancellationToken);

        public string UploadString(string url, string data) => Encoding.UTF8.GetString(Send(HttpMethod.Post, url, data, CancellationToken.None));

        // HttpClient.Send is truly synchronous (no sync-over-async), so callers on worker threads stay as they are
        private byte[] Send(HttpMethod method, string url, string body, CancellationToken cancellationToken)
        {
            if (_query.Count > 0)
            {
                StringBuilder sb = new StringBuilder(url);
                sb.Append(url.Contains('?') ? '&' : '?');
                sb.Append(string.Join("&", _query.ConvertAll(
                    q => Uri.EscapeDataString(q.Key) + "=" + Uri.EscapeDataString(q.Value ?? string.Empty))));
                url = sb.ToString();
            }

            using (HttpRequestMessage request = new HttpRequestMessage(method, url))
            {
                foreach (KeyValuePair<string, string> header in _headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                if (body != null)
                {
                    // like WebClient.UploadString: the body as UTF-8, with no Content-Type of its own
                    request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
                }

                try
                {
                    _lastResponse?.Dispose();
                    _lastResponse = _httpClient.Send(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
                    _lastResponse.EnsureSuccessStatusCode();

                    using (Stream stream = _lastResponse.Content.ReadAsStream(cancellationToken))
                    using (MemoryStream ms = new MemoryStream())
                    {
                        stream.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
                {
                    // HttpClient reports its timeout as cancellation, WebClient reported a WebException
                    throw new HttpRequestException("The request timed out.", ex);
                }
            }
        }

        public void Dispose()
        {
            _lastResponse?.Dispose();
            _lastResponse = null;
        }
    }
}
