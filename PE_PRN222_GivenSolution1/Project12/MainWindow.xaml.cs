using Project12.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;


namespace Project12
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly HttpClient _client;
        public MainWindow()
        {
            InitializeComponent();
            _client = new HttpClient();
            _ = LoadBooks(); // gọi ngay khi mở app
        }
        private async Task LoadBooks()
        {
            try
            {
                StatusText.Text = "Connecting to server...";
                var books = await _client.GetFromJsonAsync<List<Book>>("http://localhost:8080/books");

                if (books != null)
                {
                    BooksGrid.ItemsSource = books;
                    StatusText.Text = "Server connection successful. Data loaded.";
                    StatusText.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    StatusText.Text = "No data received from server.";
                }
            }
            catch
            {
                StatusText.Text = "Unable to connect to server. Please check server status or try reconnecting again.";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private async void Reconnect_Click(object sender, RoutedEventArgs e)
        {
            await LoadBooks();
        }
    }
}