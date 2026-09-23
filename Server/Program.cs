using MiniWar.Server;

string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiniWar", "Server");
int port = 7777;
bool localOnly = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--data" && i + 1 < args.Length) data = Path.GetFullPath(args[++i]);
    else if (args[i] == "--port" && i + 1 < args.Length) port = int.Parse(args[++i]);
    else if (args[i] == "--local") localOnly = true;
    else throw new ArgumentException("Usage: MiniWar.Server [--data directory] [--port 7777] [--local]");
}
if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
await using var host = new LanHost(data, port, localOnly ? System.Net.IPAddress.Loopback : System.Net.IPAddress.Any);
host.Start();
Console.WriteLine($"MiniWar LAN Server — TCP {host.Port}");
if (localOnly) Console.WriteLine("Local preview: accepting connections from this PC only.");
Console.WriteLine($"Save directory: {data}");
Console.WriteLine($"Server identity (SHA-256): {host.Fingerprint}");
Console.WriteLine("Share the server's LAN IP and identity with classmates. Ctrl+C stops the server.");
var stopped = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopped.TrySetResult(); };
await stopped.Task;
