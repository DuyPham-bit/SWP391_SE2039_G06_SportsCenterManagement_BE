using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using MimeKit;

// An isolated SMTP receiver: no DNS, credentials, TLS bypass outside loopback, or outgoing email.
sealed class FakeSmtpServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly Task worker;
    public string? Recipient { get; private set; }
    public string Message { get; private set; } = "";
    public bool Reject { get; set; }
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public string Otp
    {
        get
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(Message));
            var mime = MimeMessage.Load(stream);
            var body = mime.TextBody ?? mime.HtmlBody ?? "";
            return Regex.Match(body, @"\b[0-9]{6}\b").Value;
        }
    }

    public FakeSmtpServer()
    {
        listener.Start();
        worker = Run();
    }

    private async Task Run()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(stop.Token);
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
                await writer.WriteLineAsync("220 localhost audit SMTP");
                while (await reader.ReadLineAsync(stop.Token) is { } line)
                {
                    if (line.StartsWith("EHLO") || line.StartsWith("HELO")) await writer.WriteLineAsync("250 localhost");
                    else if (line.StartsWith("RCPT TO:", StringComparison.OrdinalIgnoreCase))
                    {
                        Recipient = line;
                        await writer.WriteLineAsync(Reject ? "550 audit delivery rejected" : "250 OK");
                    }
                    else if (line == "DATA")
                    {
                        await writer.WriteLineAsync("354 End with dot");
                        var data = new StringBuilder();
                        while (await reader.ReadLineAsync(stop.Token) is { } part && part != ".") data.Append(part).Append("\r\n");
                        Message = data.ToString();
                        await writer.WriteLineAsync("250 Accepted");
                    }
                    else if (line == "QUIT") { await writer.WriteLineAsync("221 Bye"); break; }
                    else await writer.WriteLineAsync("250 OK");
                }
            }
        }
        catch (Exception exception) when (stop.IsCancellationRequested && exception is OperationCanceledException or SocketException or IOException) { }
    }

    public async ValueTask DisposeAsync()
    {
        stop.Cancel(); listener.Stop();
        await worker;
        stop.Dispose();
    }
}
