using Emoji.Wpf;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WindowsInput.Events;
using TextBlock = Emoji.Wpf.TextBlock;

namespace ChatClient.Views
{
    /// <summary>
    /// Interaction logic for ChatWindow.xaml
    /// </summary>
    public partial class ChatWindow : Window
    {
        private string _mode;
        private string _hostIpAddress;
        private string _machineIpAddress;
        private int _port;
        private TCPServerService _tcpServerService;
        private TCPClientService _tcpClientService;

        const string CLIENT_STRING = "Client";
        const string SERVER_STRING = "Server";

        public ChatWindow(string mode, string ipAddress, int port)
        {
            InitializeComponent();
            this._mode = mode;
            this._hostIpAddress = ipAddress;
            this._port = port;
            title.Text = mode;
            _machineIpAddress = GetLocalIpAddress();
            StartServerOrClient();
            InitializeMessage();
        }

        // Start socket server or establish client to communicate with the server
        private void StartServerOrClient()
        {
            if (_mode == SERVER_STRING)
            {
                // Start the server asynchronously
                _tcpServerService = new TCPServerService(_hostIpAddress, _port, UpdateChatMessage);
                Task.Run(() => _tcpServerService.ExecuteServerAsync());
            }
            else if (_mode == CLIENT_STRING)
            {
                // Connect to the server asynchronously
                _tcpClientService = new TCPClientService(_hostIpAddress, _port, UpdateChatMessage);
                Task.Run(() => _tcpClientService.ConnectToServerAsync());
            }
        }

        // Initial message when server starts
        private void InitializeMessage()
        {
            if (_mode != SERVER_STRING) return;

            string message = string.Format("Listening to {0} on port {1}...", _hostIpAddress, _port);
            TextBlock initMessage = CreateSenderMessage(message);
            AddMessageToChat(initMessage);
        }

        // Event handler for Send Button click
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string message = MessageTextBox.Text;
            if (!CheckEmptyMessage(message))
            {
                SendMessage(message);
                TextBlock senderMessage = CreateSenderMessage(message);
                AddMessageToChat(senderMessage);
            }
        }

        // Event handler for Send Button click
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            CloseServerOrClient();
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }

        // Event handler for Enter key press inside the TextBox
        private void MessageTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            string message = MessageTextBox.Text;
            if (e.Key == Key.Enter && !CheckEmptyMessage(message))
            {
                SendMessage(message);
                TextBlock senderMessage = CreateSenderMessage(message);
                AddMessageToChat(senderMessage);
            }
        }

        // Function to check if the message is empty
        private bool CheckEmptyMessage(string message)
        {
            return string.IsNullOrEmpty(message);
        }

        // Create message text block with color based on the sender
        private TextBlock CreateSenderMessage(string message)
        {
            TextBlock senderMessage = new TextBlock
            {
                Text = $"{_mode} ({DateTime.Now:t}) [{_machineIpAddress}]: {message}",  // Adding time in the format HH:mm
                Margin = new Thickness(5),
                Padding = new Thickness(5),
                Background = System.Windows.Media.Brushes.LightBlue
            };

            return senderMessage;
        }

        // Create message text block with color based on the receiver
        private TextBlock CreateReceiverMessage(string message)
        {
            TextBlock receiverMessage = new TextBlock
            {
                Text = _mode == SERVER_STRING
                    ? $"{CLIENT_STRING} ({DateTime.Now:t}) {message}"  // Adding time for the receiver
                    : $"{SERVER_STRING} ({DateTime.Now:t}) {message}",
                Margin = new Thickness(5),
                Padding = new Thickness(5),
                Background = System.Windows.Media.Brushes.LightGray
            };

            return receiverMessage;
        }

        // Function to add a message to the chat view
        private void AddMessageToChat(TextBlock message)
        {
            // Add message to the chat
            ChatStackPanel.Children.Add(message);

            // Scroll to the bottom of the chat box
            ChatScrollViewer.ScrollToBottom();

            // Clear the text box for the next message
            MessageTextBox.Text = "";
        }

        // Send the message to the server or client
        private void SendMessage(string message)
        {
            // Send the message to the client from the server
            if (_mode == SERVER_STRING)
            {
                Task.Run(() => _tcpServerService.BroadcastMessageToClientsParallel($"[{_machineIpAddress}]: {message}", null!));
            }
            else if (_mode == CLIENT_STRING)
            {
                Task.Run(() => _tcpClientService.SendMessageAsync($"{message}"));
            }
        }

        // Update the chat window with new messages
        private void UpdateChatMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (!CheckEmptyMessage(message))
                {
                    TextBlock senderMessage = CreateReceiverMessage(message);
                    AddMessageToChat(senderMessage);
                }
            });
        }

        // Close server or client connection
        private void CloseServerOrClient()
        {
            if (_mode == SERVER_STRING)
            {
                // Close Server
                _tcpServerService.CloseServer();
            }
            else if (_mode == CLIENT_STRING)
            {
                // Close Client
                _tcpClientService.CloseClient();
            }
        }

        // Show windows emoji picker
        private async void EmojiButton_Click(object sender, RoutedEventArgs e)
        {
            // Simulate Win + . hotkey
            await WindowsInput.Simulate.Events()
                .ClickChord(KeyCode.LWin, KeyCode.OemPeriod)
                .Invoke();

            // Delay to give the emoji picker time to appear
            await Task.Delay(100);

            // Regain focus on the TextBox
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                MessageTextBox.Focus();
            }));
        }

        // Get machine local address
        public string GetLocalIpAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ipAddress = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            return ipAddress?.ToString() ?? "No valid IP address found";
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // Create an instance of OpenFileDialog
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            // Set file filters (optional)
            openFileDialog.Filter = "All files (*.*)|*.*";

            // Show the dialog
            bool? result = openFileDialog.ShowDialog();

            // If the user selected a file
            if (result == true)
            {
                // Get the file path
                string filePath = openFileDialog.FileName;

                if (_mode == CLIENT_STRING)
                {
                    // Input the file path to the TCPClient to send the file
                    await _tcpClientService.SendFileAsync(filePath);
                }
                else if (_mode == SERVER_STRING) 
                {
                    // Input the file path to broadcast file to all clients
                    await _tcpServerService.BroadcastFileToClientsParallelAsync(filePath, null!);
                }
            }
        }
    }
}
