using System.Windows;

namespace ChatClient.Views
{
    /// <summary>
    /// Interaction logic for FormWindow.xaml
    /// </summary>
    public partial class FormWindow : Window
    {
        private string _mode;

        public FormWindow(string mode)
        {
            InitializeComponent();
            this._mode = mode;
        }

        // When the Connect button is clicked, proceed to the ChatWindow
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(IPTextBox.Text) || string.IsNullOrEmpty(PortTextBox.Text))
            {
                MessageBox.Show("Please enter both IP Address and Port.");
                return;
            }

            string ipAddress = IPTextBox.Text;
            int port = Int32.Parse(PortTextBox.Text);

            // Here you can add logic to connect to the server or set up the server based on "mode"
            // For now, we will just go to the ChatWindow

            ChatWindow chatWindow = new ChatWindow(_mode, ipAddress, port);
            chatWindow.Show();
            this.Close();
        }
    }
}
