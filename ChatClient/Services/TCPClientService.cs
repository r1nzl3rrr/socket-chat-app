using System.IO;
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
    private string _fileDirectory = "ReceivedFiles";  // Directory to store received files

    public TCPClientService(string host, int port, Action<string> updateUI)
    {
        _host = host;
        _port = port;
        _updateUI = updateUI;

        if (!Directory.Exists(_fileDirectory))
        {
            Directory.CreateDirectory(_fileDirectory);
        }
    }

    // Connect to server with host address and open port
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

    // Send message to the server
    public async Task SendMessageAsync(string message)
    {
        try
        {
            if (_clientStream == null || !_client.Connected)
            {
                _updateUI?.Invoke("Error: Not connected to the server.");
                return;
            }

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

    // Close client connection
    public void CloseClient()
    {
       _client?.Close();
    }

    // Listening for server messages that is either from the server or other clients' messages broadcasted
    private async Task ListenForServerMessagesAsync()
    {
        try
        {
            if (_clientStream == null || !_client.Connected)
            {
                _updateUI?.Invoke("Error: Not connected to the server.");
                return;
            }

            Byte[] buffer = new Byte[4096];
            int bytesRead;
            bool isReceivingFile = false;
            string fileName = string.Empty;
            FileStream fileStream = null!;

            while ((bytesRead = await _clientStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
            {
                string serverMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                // Check if a file transfer is starting
                if (serverMessage.StartsWith("STARTFILE:"))
                {
                    fileName = serverMessage.Replace("STARTFILE:", "").Trim();
                    string filePath = Path.Combine(_fileDirectory, fileName);

                    // Start writing the file
                    fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                    isReceivingFile = true;
                    _updateUI?.Invoke($"Receiving file: {fileName}");
                }
                else if (serverMessage.StartsWith("ENDFILE"))
                {
                    // File transfer completed
                    isReceivingFile = false;
                    fileStream?.Close();
                    _updateUI?.Invoke($"Received file: {fileName}");
                }
                else if (isReceivingFile)
                {
                    // Write the binary data to the file if we are receiving a file
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                }
                else
                {
                    // Normal text message from the server
                    _updateUI?.Invoke($"{serverMessage}");
                }
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


    // Send File to server
    public async Task SendFileAsync(string filePath)
    {
        try
        {
            if (_clientStream == null || !_client.Connected)
            {
                _updateUI?.Invoke("Error: Not connected to the server.");
                return;
            }

            byte[] buffer = new byte[4096]; // Buffer to hold chunks of the file
            int bytesRead;

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                // Notify the server that the file transfer is starting
                await SendMessageAsync($"STARTFILE:{Path.GetFileName(filePath)}");

                // Read file in chunks and send each chunk to the server
                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await _clientStream.WriteAsync(buffer, 0, bytesRead); // Send the chunk
                }

                // Notify the server that the file transfer is complete
                await SendMessageAsync("ENDFILE");
            }

            _updateUI?.Invoke("File transfer completed.");
        }
        catch (Exception ex)
        {
            _updateUI?.Invoke($"Error: {ex.Message}");
        }
    }

}
