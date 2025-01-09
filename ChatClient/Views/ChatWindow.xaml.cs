using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ChatClient.Views
{
    /// <summary>
    /// Interaction logic for ChatWindow.xaml
    /// </summary>
    public partial class ChatWindow : Window
    {
        private string _mode;
        private string _ipAddress;
        private int _port;
        private TCPServerService _tcpServerService;
        private TCPClientService _tcpClientService;

        const string CLIENT_STRING = "Client";
        const string SERVER_STRING = "Server";

        public ChatWindow(string mode, string ipAddress, int port)
        {
            InitializeComponent();
            this._mode = mode;
            this._ipAddress = ipAddress;
            this._port = port;
            title.Text = mode;
            StartServerOrClient();
            InitializeMessage();
        }

        // Start socket server or establish client to communicate with the server
        private void StartServerOrClient()
        {
            if (_mode == SERVER_STRING)
            {
                // Start the server asynchronously
                _tcpServerService = new TCPServerService(_ipAddress, _port, UpdateChatMessage);
                Task.Run(() => _tcpServerService.ExecuteServerAsync());
            }
            else if (_mode == CLIENT_STRING)
            {
                // Connect to the server asynchronously
                _tcpClientService = new TCPClientService(_ipAddress, _port, UpdateChatMessage);
                Task.Run(() => _tcpClientService.ConnectToServerAsync());
            }
        }

        // Initial message when server starts
        private void InitializeMessage()
        {
            if (_mode != SERVER_STRING) return;

            string message = string.Format("Listening to {0} on port {1}...", _ipAddress, _port);
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
        private void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
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
                Text = $"{_mode} ({DateTime.Now:t}): {message}",  // Adding time in the format HH:mm
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
                    ? $"{CLIENT_STRING} ({DateTime.Now:t}): {message}"  // Adding time for the receiver
                    : $"{SERVER_STRING} ({DateTime.Now:t}): {message}",
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
            MessageTextBox.Clear();
        }

        // Send the message to the server or client
        private void SendMessage(string message)
        {
            // Send the message to the client from the server
            if (_mode == SERVER_STRING)
            {
                Task.Run(() => _tcpServerService.SendMessageToClientAsync(message));
            }
            else if (_mode == CLIENT_STRING)
            {
                Task.Run(() => _tcpClientService.SendMessageAsync(message));
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
    }
}
