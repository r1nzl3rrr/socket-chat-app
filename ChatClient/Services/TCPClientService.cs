using System.Net;
using System.Net.Sockets;
using System.Text;

public class TCPClientService
{
    private string _host;
    private int _port;
    private NetworkStream _clientStream; // Store the client's stream
    private TcpClient _client;           // Store the client connection
    private Action<string> _updateUI;    // Callback to update the UI

    public TCPClientService(string host, int port, Action<string> updateUI)
    {
        _host = host;
        _port = port;
        _updateUI = updateUI;
    }

    public async Task ConnectToServerAsync()
    {
        try
        {
            // Establish the connection to the server
            _client = new TcpClient(_host, _port);
            _clientStream = _client.GetStream();

            // Start listening for server messages
            await ListenForServerMessagesAsync();
        }
        catch (Exception ex)
        {
            _updateUI?.Invoke($"Error: {ex.Message}");
        }
    }

    public async Task SendMessageAsync(string message)
    {
        try
        {
            if (_clientStream != null)
            {
                // Convert the message to bytes and send it to the server
                Byte[] data = Encoding.UTF8.GetBytes(message);
                await _clientStream.WriteAsync(data, 0, data.Length);
            }
        }
        catch (Exception ex)
        {
            _updateUI?.Invoke($"Error: {ex.Message}");
        }
    }

    public void CloseClient()
    {
       _client?.Close();
    }

    private async Task ListenForServerMessagesAsync()
    {
        try
        {
            Byte[] data = new Byte[1024];
            int bytes;

            while ((bytes = await _clientStream.ReadAsync(data, 0, data.Length)) != 0)
            {
                string responseData = Encoding.UTF8.GetString(data, 0, bytes);
                _updateUI?.Invoke($"{responseData}"); // Update the UI with the server's response
            }
        }
        catch (Exception ex)
        {
            _updateUI?.Invoke($"Error: {ex.Message}");
        }
        finally
        {
            _client?.Close();
        }
    }
}
