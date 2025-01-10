using System.Net;
using System.Net.Sockets;
using System.Text;

public class TCPServerService
{
    private string _host;
    private int _port;
    private TcpListener _server;
    private NetworkStream _clientStream; // Store the client's stream
    private TcpClient _client;           // Store the client connection
    private Action<string> _updateUI;    // Callback to update the UI

    public TCPServerService(string host, int port, Action<string> updateUI)
    {
        _host = host;
        _port = port;
        _updateUI = updateUI;
    }

    // This method will process data from the client asynchronously
    private async Task ProcessDataAsync(TcpClient client)
    {
        string data;
        int count;

        try
        {
            _client = client;
            _clientStream = _client.GetStream(); // Store the stream

            Byte[] bytes = new Byte[1024];
            while ((count = await _clientStream.ReadAsync(bytes, 0, bytes.Length)) != 0)
            {
                data = Encoding.UTF8.GetString(bytes, 0, count);
                Console.WriteLine($"Received {data} at {DateTime.Now:t}");

                // Update WPF UI with received message
                _updateUI?.Invoke($"{data}");
            }

            _client.Close();
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
            while (true)
            {
                TcpClient client = await server.AcceptTcpClientAsync();
                Console.WriteLine($"Client connected!");

                // Process each connected client in a separate task
                _ = Task.Run(() => ProcessDataAsync(client));
            }
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

    // Send a response from the WPF UI back to the connected client asynchronously
    public async Task SendMessageToClientAsync(string message)
    {
        if (_clientStream != null)
        {
            byte[] msg = Encoding.UTF8.GetBytes(message);
            await _clientStream.WriteAsync(msg, 0, msg.Length);
            Console.WriteLine($"Sent to client: {message}");
        }
        else
        {
            Console.WriteLine("No client connected.");
        }
    }

    public void CloseServer()
    {
        _server?.Stop();
    }
}
