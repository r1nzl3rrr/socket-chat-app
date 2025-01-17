using System.Collections.Concurrent;
using System.IO;
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
    private string _fileDirectory = "ReceivedFiles";  // Directory to store received files

    public TCPServerService(string host, int port, Action<string> updateUI)
    {
        _host = host;
        _port = port;
        _clients = new ConcurrentDictionary<TcpClient, NetworkStream>();
        _updateUI = updateUI;
        if (!Directory.Exists(_fileDirectory))
        {
            Directory.CreateDirectory(_fileDirectory);
        }

    }

    // This method will process data from the client asynchronously
    private async Task ProcessDataAsync(TcpClient client)
    {
        int count;
        bool isReceivingFile = false;
        string fileName = string.Empty;
        FileStream fileStream = null;
        long fileSize = 0;
        long receivedFileSize = 0;

        try
        {
            NetworkStream clientStream = _clients[client]; // Store the stream
            var clientEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
            string clientIP = clientEndPoint?.Address.ToString() ?? "Unknown IP";
            Byte[] bytes = new Byte[4096];

            while ((count = await clientStream.ReadAsync(bytes, 0, bytes.Length)) != 0)
            {
                if (!isReceivingFile)
                {
                    string data = Encoding.UTF8.GetString(bytes, 0, count);

                    if (data.StartsWith("STARTFILE:"))
                    {
                        // Start receiving the file, now expecting the file size and name
                        isReceivingFile = true;
                        string[] fileInfo = data.Replace("STARTFILE:", "").Trim().Split(':');
                        fileName = fileInfo[0];
                        fileSize = long.Parse(fileInfo[1]);  // Get the file size

                        string filePath = Path.Combine(_fileDirectory, fileName);
                        fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                        receivedFileSize = 0;  // Reset received file size tracker
                        _updateUI?.Invoke($"Receiving file '{fileName}' ({fileSize} bytes)...");

                        continue;  // Skip processing further and continue reading file data
                    }
                    else
                    {
                        // Normal message from the client
                        string messageWithIP = $"[{clientIP}]: {data}";
                        _updateUI?.Invoke($"{messageWithIP}");

                        // Broadcast the message to other clients
                        await BroadcastMessageToClientsParallel(messageWithIP, client);
                    }
                }
                else
                {
                    // File data receiving mode
                    await fileStream.WriteAsync(bytes, 0, count);
                    receivedFileSize += count;

                    // Check if the file is completely received
                    if (receivedFileSize >= fileSize)
                    {
                        isReceivingFile = false;  // Exit file receiving mode
                        fileStream?.Close();
                        fileStream = null;
                        _updateUI?.Invoke($"File '{fileName}' received successfully from {clientIP}.");

                        // Broadcast the file to all other clients
                        await BroadcastFileToClientsParallelAsync(fileName, client);
                    }
                }
            }

            // Remove client when done
            _clients.TryRemove(client, out _);
            client.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
        finally
        {
            // Ensure the file stream is closed in case of error
            fileStream?.Close();
        }
    }


    // Method to broadcast a file to all clients
    public async Task BroadcastFileToClientsParallelAsync(string fileName, TcpClient senderClient)
    {
        try
        {
            string filePath = Path.Combine(_fileDirectory, fileName);

            if (!File.Exists(filePath))
            {
                _updateUI?.Invoke($"Error: File '{fileName}' not found for broadcasting.");
                return;
            }

            byte[] buffer = new byte[4096];

            // Notify clients that a file is being sent
            await BroadcastMessageToClientsParallel($"STARTFILE:{fileName}", senderClient);

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                int bytesRead;

                // Read file in chunks and send them in parallel to all clients
                while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    // Send file chunks to all clients except the sender in parallel using PLINQ
                    var tasks = _clients.Keys.AsParallel().Select(async client =>
                    {
                        try
                        {
                            if (client != senderClient && client.Connected)
                            {
                                await _clients[client].WriteAsync(buffer, 0, bytesRead);
                                await _clients[client].FlushAsync(); // Ensure the stream is flushed
                            }
                        }
                        catch (Exception ex)
                        {
                            _updateUI?.Invoke($"Error broadcasting to client: {ex.Message}");
                        }
                    });

                    // Await all the tasks to ensure each chunk is sent before moving to the next
                    await Task.WhenAll(tasks);
                }
            }

            // Notify clients that file transfer is complete
            await BroadcastMessageToClientsParallel("ENDFILE", senderClient);
            _updateUI?.Invoke($"File '{fileName}' broadcasted to all clients.");
        }
        catch (Exception ex)
        {
            _updateUI?.Invoke($"Error broadcasting file: {ex.Message}");
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

    // Listening for clients
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
    public async Task BroadcastMessageToClientsParallel(string message, TcpClient senderClient)
    {
        byte[] msg = Encoding.UTF8.GetBytes(message);

        // Use PLINQ to broadcast messages to clients in parallel
        var tasks = _clients.Keys.AsParallel().Select(async client =>
        {
            try
            {
                if (client != senderClient && client.Connected)
                {
                    // Write the message asynchronously for each client
                    await _clients[client].WriteAsync(msg, 0, msg.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error broadcasting to client: {ex.Message}");
            }
        });

        // Wait for all the broadcast tasks to complete
        await Task.WhenAll(tasks);
    }

    // Close server connection
    public void CloseServer()
    {
        foreach (var client in _clients.Keys)
        {
            client.Close();
        }
        _server?.Stop();
    }
}
