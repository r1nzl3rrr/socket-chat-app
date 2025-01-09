using System.Windows;

namespace ChatClient.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        const string CLIENT_STRING = "Client";
        const string SERVER_STRING = "Server";

        public MainWindow()
        {
            InitializeComponent();
        }

        // When Client button is clicked, navigate to FormWindow with 'Client' option
        private void ClientButton_Click(object sender, RoutedEventArgs e)
        {
            FormWindow formWindow = new FormWindow(CLIENT_STRING);
            formWindow.Show();
            this.Close();
        }

        // When Server button is clicked, navigate to FormWindow with 'Server' option
        private void ServerButton_Click(object sender, RoutedEventArgs e)
        {
            FormWindow formWindow = new FormWindow(SERVER_STRING);
            formWindow.Show();
            this.Close();
        }
    }
}