<a href="https://github.com/cloudtoid"><img src="https://raw.githubusercontent.com/cloudtoid/assets/master/logos/cloudtoid-black-red.png" width="100"></a>

# Gateway Core

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/cloudtoid/url-patterns/blob/master/LICENSE)

A modern API Gateway and Reverse Proxy library for .NET Core and beyond.

## V1 - TODOs

- Add functional tests
- Write documentation
  - Add a table of all config values with a description and links/anchors from the rest of the doc
  - URL Pattern Matching
- Add tests for trailing TrailingHeaderSetter
- Enable github build actions
- Publish NuGet
- Add multiple Sample Projects
  - Basic/simple
  - Multiple kestrel servers
  - Advanced extensibility
  - Use of options
  - Named HttpClient Management from the outside
- Side by side tests with nginx
- Kestrel should only listen on relevant routes defined in Options
- Benchmark
- Increase code coverage
- Add support for modification of the Path attribute in the Set-Cookie header
- Ensure that all 3 projects compile on macos and VS for Mac

## Future version

- Protocols
  - Add support for web-sockets
  - Test gRPC
  - Test SignalR
- Features
  - Request Aggregation
  - Caching
  - Retry policies / QoS
  - Load Balancing
- Platforms
  - Kubernetes
  - Service Fabric

## Tracing

GatewayCore uses two headers to pass on trace identifiers to both the server and the client:

### Correlation-id header

A request originated from a client can include a correlation-id header that is passed on unchanged to the proxied server. The default correlation-id header is `x-correlation-id` but can be renamed as shown here:

```json
{
  "routes": {
    "/api/": {
      "proxy": {
        "to": "http://upstream/v1/",
        "correlationIdHeader": "x-request-id",
```

In the case of a missing correlation-id header, GatewayCore generates a unique identifier instead.

The correlation-id header can also be added to the response message by explicitly enabling `includeCorrelationId`:

```json
{
  "routes": {
    "/api/": {
      "proxy": {
        "to": "http://upstream/v1/",
        "downstreamResponse": {
          "headers": {
            "includeCorrelationId": true
```

It is also possible to omit this header from the request to the proxied server using `'ignoreCorrelationId'` as shown below:

```json
{
  "routes": {
    "/api/": {
      "proxy": {
        "to": "http://upstream/v1/",
        "upstreamRequest": {
          "headers": {
            "ignoreCorrelationId": true,
```

### Call-id header

A unique call-id is generated and forwarded for every request received by GatewayCore. The header that is appended is `x-call-id` and can be removed from outbound upstream requests using `ignoreCallId`. It can also be included in responses sent to the client by enabling `includeCallId` as shown in the sample below:

```json
{
  "routes": {
    "/api/": {
      "proxy": {
        "to": "http://upstream/v1/",
        "upstreamRequest": {
          "headers": {
            "ignoreCallId": true
          }
        },
        "downstreamResponse": {
          "headers": {
            "includeCallId": true
```

> An inbound call-id header received from the client or the proxied server is silently ignored.

## Route tracking

There are two types of route tracking, one that includes information about the proxies and one that provides for client details.

### Via header

A proxy typically uses the [`Via`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Via) header for tracking message forwards, avoiding request loops, and identifying the protocol capabilities of senders along the request/response chain.

This header is a comma-separated list of proxies along the message chain with the closest proxy to the sender being the left-most value.

The value added by GatewayCore includes the pseudonym of the proxy. This name is `gwcore` but can be customized, as shown below:

```json
{
  "routes": {
    "/api/": {
      "proxy": {
        "to": "http://upstream/v1/",
        "proxyName": "my-proxy-name",
```

GatewayCore appends one of the following values:

| Sender's protocol | Value | Example |
|:--- |:--- |:-- |
| `HTTP/1.0` | `1.0 \<proxy-name>` | `1.0 gwcore` |
| `HTTP/1.1` | `1.1 \<proxy-name>` | `1.1 gwcore` |
| `HTTP/2.0` | `2.0 \<proxy-name>` | `2.0 gwcore` |
| `HTTP/X.Y` | `X.Y \<proxy-name>` | `X.Y gwcore` |
| `<protocol>\<version>` | `<protocol>\<version> <proxy-name>` |  

> As per the above, GatewayCore omits the protocol for HTTP requests and responses.

The Via header is included by default on both the request to the proxied server and the response to the client. You can change this behavior using `ignoreVia` as shown below:

```json
{
  "routes": {
    "/api/": {
      "proxy": {
        "to": "http://upstream/v1/",
        "upstreamRequest": {
          "headers": {
            "ignoreVia": true,
            }
          }
        },
        "downstreamResponse": {
          "headers": {
            "ignoreVia": true,
```

### Forwarded category of headers

The forwared class of headers contains information from the client-facing side of proxy servers that is altered or lost when a proxy is involved in the path of the request. This information is passed on using one of these techniques:

1. The [`Forwarded`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Forwarded) header is what GatewayCore uses by default. This is the standardized version of the header.
1. The [`X-Forwarded-For`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Forwarded-For), [`X-Forwarded-Host`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Forwarded-Host), and [`X-Forwarded-Proto`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/X-Forwarded-Proto) headers which are considered the [de-facto standard](https://en.wikipedia.org/wiki/De_facto_standard) versions of the [`Forwared`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Forwarded) header.

The information included in these headers typically consists of the IP address of the client, the IP address where the request came into the proxy server, the [`Host`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Host) request header field as received by the proxy, and the protocol used by the request (typically "http" or "https").

> IP V6 addresses are quoted and enclosed in square brackets.

GatewayCore uses the `Forwarded` header by default and replaces all inbound `X-Forwarded-*` headers. You can use `useXForwarded` to reverse this behavior and to prefer `X-Forwarded-*` headers instead:

```json
{
  "routes": {
    "/api/": {
      "proxy": {
        "to": "http://upstream/v1/",
        "upstreamRequest": {
          "headers": {
            "useXForwarded": true,
```

It is also possible to not include any of these headers on proxy's outbound request by using `ignoreForwarded` as per below:

```json
{
  "routes": {
    "/api/": {
      "proxy": {
        "to": "http://upstream/v1/",
        "upstreamRequest": {
          "headers": {
            "ignoreForwarded": true,
```

## Cookie handling

The [`Set-Cookie`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Set-Cookie) HTTP response header is used to send cookies from the server to the client so that the client can send them back to the server later.

This header can include attributes such as:

- `Expires`: The maximum lifetime of the cookie as an HTTP-date timestamp.
- `Max-Age`: Number of seconds until the cookie expires. A zero or negative number will expire the cookie immediately. `Max-Age` has precedence over `Expires`.
- `Domain`: Host to which the cookie will be sent on subsequent calls.
- `Path`: A path that must exist in the requested URL, or the client won't send the `Cookie` header on subsequent requests.
- `Secure`: A secure cookie is only sent to the server when a request is made with the `https:` scheme.
- `HttpOnly`: Limits the scope of the cookie to HTTP
requests. In particular, the attribute instructs the client to omit the cookie when providing access to cookies via "non-HTTP" APIs such as JavaScript's `Document.cookie` API.
- `SameSite`: Helps with potential cross-site security issues.
  - If set to `none`, cookies are sent with both cross-site requests and same-site requests.
  - If set to `strict`, cookies are sent only for same-site requests.
  - If set to `lax`, same-site cookies are withheld on cross-site subrequests, such as calls to load images or frames, but will be sent when a user navigates to the URL from an external site; for example, by following a link.

A reverse proxy such as GatewayCore often changes the domain, path, and scheme (http/https) of proxied requests and the responses. Therefore, it might be necessary also to update the `Domain`, `Path`, `SameSite`, `Secure`, and `HttpOnly` attributes.

```json
{
  "routes": {
    "/api/": {
      "proxy": {
        "to": "http://upstream/v1/",
        "downstreamResponse": {
          "headers": {
            "cookies": {
              "sessionId": {
                "secure": true,
                "httpOnly": false,
                "sameSite": "lax",
                "domain": "example.com"
              },
```

In the example above, GatewayCore will ensure that the `Set-Cookie` response header for a cookie named `sessionId` is modified such that:

- the `Secure` attribute is set,
- the `HttpOnly` attribute is removed if it was specified,
- the value of `SameSite` is changed to `lax`, and
- the `Domain` attribute is updated to `example.com`

> Set `domain` to an empty string (`"domain": ""`) if the `Domain` attribute should be fully removed from the `Set-Cookie` header.

It is also possible to use the wildcard symbol `"*"` to provide a rule that applies to all cookies.

> The wildcard rule is ignored if a rule is an exact name match.

GatewayCore pools [`HttpMessageHandler`](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpmessagehandler) instances and can reuse them for outbound upstream requests. Thus, local cookie handling is disabled by default as unanticipated [`CookieContainer`](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.cookiecontainer) object sharing often results in incorrect behavior. Although strongly discouraged, it is possible to change this behavior using `UseCookie`, as shown below.

```json
{
  "routes": {
    "/api/": {
      "proxy": {
        "to": "http://upstream/v1/",
        "upstreamRequest": {
          "sender": {
            "useCookies": true
```

> Avoid enabling `UseCookies` unless you are confident that this is the behavior that your application needs.

## Gateway settings and options

- how to pass using DI
- A table of what they are

| Key | Can be an expression | Default value | Description |
|:--- |:---:|:-- |:-- |
| `system` | | | This is the section that contains all system wide configurations. |
| `system:routeCacheMaxCount` | | `100,000` cache entries| This is the maximum number of mappings between "inbound downstream request path" and "outbound upstream request URL" that can be cached in memory. |
| `routes` | | | This is the section in which proxy routes are defined. |
| `routes:<path>` | | | This is the url path pattern that if matched, the request is proxied to the address defined by it's `to` property. |
| `routes:<path>:to` | :heavy_check_mark: | | This is an expression that defines the URL of the upstream server to which the downstream request is forwarded to. This is a required property. |
| `routes:<path>:proxyName` | :heavy_check_mark: | `gwcore` | This is an expression that defines the name of this proxy. This value is used in the Via HTTP header send on the outbound upstream request, and also the outbound downstream response. If a value is specified, an `x-gwcore-proxy-name` header with this value is added to the outbound upstream request. |
