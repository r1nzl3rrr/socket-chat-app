using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class TCPServerService
{
    private string _host;
    private int _port;
    private TcpListener _server;
    private ConcurrentDictionary<TcpClient, NetworkStream> _clients;  // Store multiple clients and their streams
    private Action<string> _updateUI;    // Callback to update the UI

    public TCPServerService(string host, int port, Action<string> updateUI)
    {
        _host = host;
        _port = port;
        _clients = new ConcurrentDictionary<TcpClient, NetworkStream>();
        _updateUI = updateUI;
    }

    // This method will process data from the client asynchronously
    private async Task ProcessDataAsync(TcpClient client)
    {
        string data;
        int count;

        try
        {
            NetworkStream clientStream = _clients[client]; // Store the stream

            // Get the client's IP address
            var clientEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
            string clientIP = clientEndPoint?.Address.ToString() ?? "Unknown IP";

            Byte[] bytes = new Byte[1024];
            while ((count = await clientStream.ReadAsync(bytes, 0, bytes.Length)) != 0)
            {
                data = Encoding.UTF8.GetString(bytes, 0, count);
                string messageWithIP = $"[{clientIP}]: {data}";
                Console.WriteLine($"Received {data} at {DateTime.Now:t}");

                // Update WPF UI with received message
                _updateUI?.Invoke($"{messageWithIP}");

                // Broadcast the message to all connected clients
                BroadcastMessageToClientsParallel(messageWithIP, client);
            }

            // Remove client when done
            _clients.TryRemove(client, out _);
            client.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
    }

    // This method will start the server and wait for connections asynchronously
    public async Task ExecuteServerAsync()
    {
        TcpListener server = null;

        try
        {
            IPAddress localAddr = IPAddress.Parse(_host);
            server = new TcpListener(localAddr, _port);
            _server = server;
            server.Start();
            Console.WriteLine("Server started...");

            // Listen for connected clients
            await TcpListen(server);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
        finally
        {
            server?.Stop();
            Console.WriteLine("Server stopped!");
        }
    }

    private async Task TcpListen(TcpListener server)
    {
        while (true)
        {
            TcpClient client = await server.AcceptTcpClientAsync();
            var clientEndPoint = client.Client.RemoteEndPoint as IPEndPoint;

            if (clientEndPoint != null)
            {
                // Show which client has connected in the UI
                string clientIP = clientEndPoint.Address.ToString();
                _updateUI?.Invoke($"Client {clientIP} connected");
            }

            // Add client to the dictionary
            _clients.TryAdd(client, client.GetStream());

            // Process each connected client in a separate task
            _ = Task.Run(() => ProcessDataAsync(client));
        }
    }

    // Broadcast a message to all connected clients asynchronously using PLINQ
    public void BroadcastMessageToClientsParallel(string message, TcpClient senderClient)
    {
        byte[] msg = Encoding.UTF8.GetBytes(message);

        // Use PLINQ to broadcast messages to clients in parallel
        _clients.Keys.AsParallel().ForAll(client =>
        {
            try
            {
                if (client != senderClient && client.Connected)
                {
                    // Write the message asynchronously for each client
                    _clients[client].WriteAsync(msg, 0, msg.Length).Wait();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting to client: {ex.Message}");
            }
        });
    }


    public void CloseServer()
    {
        foreach (var client in _clients.Keys)
        {
            client.Close();
        }
        _server?.Stop();
    }
}
