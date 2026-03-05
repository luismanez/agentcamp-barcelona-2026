using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Spectre.Console;

namespace Abyx.McpClient.Handlers;

public sealed class WiretapHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        if (IsMcpRequest(request))
        {
            WriteRequestHeader(request);
            WriteHeadersTable(request.Headers);

            if (request.Content is not null)
            {
                var body = await request.Content.ReadAsStringAsync(ct);
                WriteBody("Request Body", body);

                request.Content = RecreateContent(body, request.Content);
            }
        }

        var response = await base.SendAsync(request, ct);

        if (IsMcpRequest(request))
        {
            WriteResponseHeader(response);
            WriteHeadersTable(response.Headers);

            if (response.Content is not null)
            {
                var respBody = await response.Content.ReadAsStringAsync(ct);
                WriteBody("Response Body", respBody);

                response.Content = RecreateContent(respBody, response.Content);
            }
        }

        return response;
    }

    private static bool IsMcpRequest(HttpRequestMessage request)
        => request.RequestUri?.AbsolutePath.Contains("/mcp", StringComparison.OrdinalIgnoreCase) == true;

    private static void WriteRequestHeader(HttpRequestMessage request)
    {
        var title = $"[deepskyblue2]MCP Request[/] [grey]{Markup.Escape(request.Method.Method)} {Markup.Escape(request.RequestUri?.ToString() ?? string.Empty)}[/]";
        AnsiConsole.Write(new Rule(title).RuleStyle("grey").LeftJustified());
    }

    private static void WriteResponseHeader(HttpResponseMessage response)
    {
        var statusColor = response.IsSuccessStatusCode ? "green" : "red";
        var title = $"[{statusColor}]MCP Response[/] [grey]{(int)response.StatusCode} {Markup.Escape(response.ReasonPhrase ?? string.Empty)}[/]";
        AnsiConsole.Write(new Rule(title).RuleStyle("grey").LeftJustified());
    }

    private static void WriteHeadersTable(HttpHeaders headers)
    {
        if (!headers.Any())
        {
            return;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Header");
        table.AddColumn("Value");

        foreach (var header in headers)
        {
            var value = header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                ? "Bearer ***REDACTED***"
                : string.Join(", ", header.Value);

            table.AddRow(Markup.Escape(header.Key), Markup.Escape(value));
        }

        AnsiConsole.Write(table);
    }

    private static void WriteBody(string title, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        var prettyBody = TryFormatJson(body);
        var panel = new Panel(new Text(prettyBody))
        {
            Header = new PanelHeader(title),
            Border = BoxBorder.Rounded,
            Expand = true
        };

        AnsiConsole.Write(panel);
    }

    private static string TryFormatJson(string input)
    {
        try
        {
            using var document = JsonDocument.Parse(input);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (JsonException)
        {
            return input;
        }
    }

    private static StringContent RecreateContent(string body, HttpContent original)
    {
        var mediaType = original.Headers.ContentType?.MediaType ?? "application/json";
        var encoding = Encoding.UTF8;
        var charSet = original.Headers.ContentType?.CharSet;

        if (!string.IsNullOrWhiteSpace(charSet))
        {
            try
            {
                encoding = Encoding.GetEncoding(charSet);
            }
            catch (ArgumentException)
            {
                encoding = Encoding.UTF8;
            }
        }

        var content = new StringContent(body, encoding, mediaType);

        foreach (var header in original.Headers)
        {
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return content;
    }
}