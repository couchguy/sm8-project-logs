using System.Net;
using System.Text;
using ProjectLogs.Api.Entities;
using ProjectLogs.Api.ServiceM8;

namespace ProjectLogs.Api.Views;

public static class PopupRenderer
{
    private const string SdkCss = "https://platform.servicem8.com/sdk/1.0/sdk.css";
    private const string SdkJs = "https://platform.servicem8.com/sdk/1.0/sdk.js";

    public static string RenderConfirmation(string message)
    {
        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <link rel="stylesheet" href="{{SdkCss}}">
                <script src="{{SdkJs}}"></script>
            </head>
            <body>
                <p>{{message}}</p>
                <button onclick="SMClient.init().closeWindow()">OK</button>
                <script>SMClient.init().resizeWindow(400, 200);</script>
            </body>
            </html>
            """;
    }

    public static string RenderFullPage(string title, string bodyContent)
    {
        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <link rel="stylesheet" href="{{SdkCss}}">
                <script src="{{SdkJs}}"></script>
            </head>
            <body>
                <h1>{{Encode(title)}}</h1>
                {{bodyContent}}
                <script>SMClient.init().resizeWindow(720, 600);</script>
            </body>
            </html>
            """;
    }

    public static string RenderDailyLogPage(
        string jobLabel,
        List<Sm8JobMaterial> unclaimed,
        List<DailyLog> pastLogs)
    {
        var content = RenderDailyLogContent(jobLabel, unclaimed, pastLogs);

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <link rel="stylesheet" href="{{SdkCss}}">
                <script src="{{SdkJs}}"></script>
                <style>
                    .message { padding: 0.5em; margin-bottom: 1em; background: #d4edda; border-radius: 4px; }
                    .empty-state { color: #6c757d; font-style: italic; }
                    .log-entry { margin-bottom: 1em; border-bottom: 1px solid #eee; padding-bottom: 0.5em; }
                    .log-lines { margin-left: 1em; font-size: 0.9em; color: #555; }
                </style>
            </head>
            <body>
                <h1>Daily Log &mdash; Job #{{Encode(jobLabel)}}</h1>
                <div id="content">{{content}}</div>
                <script>
                    var client = SMClient.init();
                    client.resizeWindow(720, 600);

                    function closeOut() {
                        var btn = document.getElementById('closeout-btn');
                        btn.disabled = true;
                        btn.textContent = 'Closing out...';
                        var today = new Date().toISOString().split('T')[0];
                        client.invoke('close_out', { logDate: today })
                            .then(function(result) {
                                var html = typeof result === 'string' ? result : (result.eventResponse || '');
                                document.getElementById('content').innerHTML = html;
                            })
                            .catch(function(err) {
                                btn.disabled = false;
                                btn.textContent = "Close out today's log";
                                alert('Close-out failed: ' + err);
                            });
                    }
                </script>
            </body>
            </html>
            """;
    }

    public static string RenderDailyLogContent(
        string jobLabel,
        List<Sm8JobMaterial> unclaimed,
        List<DailyLog> pastLogs,
        string? message = null)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(message))
            sb.AppendLine($"""<div class="message">{Encode(message)}</div>""");

        // --- Unclaimed materials ---
        sb.AppendLine("<h2>Unclaimed Materials</h2>");

        if (unclaimed.Count == 0)
        {
            sb.AppendLine("""<p class="empty-state">No new materials since last close-out.</p>""");
        }
        else
        {
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>Item</th><th>Qty</th><th>Price</th><th>Total</th></tr></thead>");
            sb.AppendLine("<tbody>");

            foreach (var m in unclaimed)
            {
                var qty = ParseDecimal(m.Quantity);
                var price = ParseDecimal(m.Price);
                var total = qty * price;
                sb.AppendLine($"<tr><td>{Encode(m.Name ?? "(unnamed)")}</td>" +
                              $"<td>{qty}</td><td>${price:F2}</td><td>${total:F2}</td></tr>");
            }

            sb.AppendLine("</tbody></table>");
            sb.AppendLine("""<button id="closeout-btn" onclick="closeOut()">Close out today's log</button>""");
        }

        // --- Log history ---
        sb.AppendLine("<h2>Log History</h2>");

        if (pastLogs.Count == 0)
        {
            sb.AppendLine("""<p class="empty-state">No logs recorded yet.</p>""");
        }
        else
        {
            foreach (var log in pastLogs)
            {
                sb.AppendLine($"""<div class="log-entry">""");
                sb.AppendLine($"<strong>{log.LogDate:yyyy-MM-dd}</strong> &mdash; {Encode(log.Summary ?? "")}");

                if (log.Lines.Count > 0)
                {
                    sb.AppendLine("""<div class="log-lines">""");
                    foreach (var line in log.Lines)
                    {
                        sb.AppendLine($"<div>{Encode(line.Name)}: " +
                                      $"{line.Quantity} &times; ${line.UnitPrice:F2} = ${line.LineTotal:F2}</div>");
                    }
                    sb.AppendLine("</div>");
                }

                sb.AppendLine("</div>");
            }
        }

        return sb.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, out var result) ? result : 0;
}
