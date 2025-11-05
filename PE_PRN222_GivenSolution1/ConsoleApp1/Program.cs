using ConsoleApp1;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class Book
{
    public int BookId { get; set; }
    public string Title { get; set; } = "";
    public int? PublicationYear { get; set; }
    public Genre? Genre { get; set; }
}

public class Genre
{
    public int GenreId { get; set; }
    public string GenreName { get; set; } = "";
}

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static string baseUrl = "";

    static async Task Main(string[] args)
    {
        // Initialize ConsoleManager to handle F1 key and clear input buffer
        ConsoleManager.Initialize();

        // ======================================
        // 1. Load cấu hình từ appsettings.json
        // ======================================
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        baseUrl = config["BaseUrl"] ;
        ConsoleManager.WriteLine($"Base URL: {baseUrl}");

        bool running = true;

        while (running)
        {
                        ConsoleManager.WriteLine("\n====== Library Client ======");
                        ConsoleManager.WriteLine("1. List Books");
                        ConsoleManager.WriteLine("2. Create Book");
                        ConsoleManager.WriteLine("3. Update Book");
                        ConsoleManager.WriteLine("4. Delete Book");
                        ConsoleManager.WriteLine("5. Quit");
                        ConsoleManager.Write("Choose an option: ");

            var choice = Console.ReadLine();

                            switch (choice)
                            {
                                case "1":
                                    await ListBooksAsync();
                                    break;
                                case "2":
                                    await CreateBookAsync();
                                    break;
                                case "3":
                                    await UpdateBookMenuAsync();
                                    break;
                                case "4":
                                    await DeleteBookAsync();
                                    break;
                                case "5":
                                    running = false;
                                    ConsoleManager.WriteLine("Goodbye!");
                                    break;
                                default:
                                    ConsoleManager.WriteLine("Invalid choice. Try again.");
                                    break;
                            }
        }
    }

    // ===========================
    // 1. List Books
    // ===========================
    private static async Task ListBooksAsync()
    {
        try
        {
            var response = await client.GetAsync($"{baseUrl}books");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var books = JsonSerializer.Deserialize<List<Book>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                ConsoleManager.WriteLine("\n====== Book List ======");
                if (books != null && books.Count > 0)
                {
                    foreach (var book in books)
                    {
                        ConsoleManager.WriteLine(Utils.Stringify(book));
                    }
                }
                else
                {
                    ConsoleManager.WriteLine("No books found.");
                }
            }
            else
            {
                ConsoleManager.WriteLine($"Error: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            ConsoleManager.WriteLine($"Exception: {ex.Message}");
        }
    }

    // ===========================
    // 2. Create Book
    // ===========================
    private static async Task CreateBookAsync()
    {
        ConsoleManager.Write("Enter title: ");
        string title = Console.ReadLine() ?? "";

        ConsoleManager.Write("Enter publication year: ");
        int.TryParse(Console.ReadLine(), out int year);

        var newBook = new Book
        {
            Title = title,
            PublicationYear = year
        };

        try
        {
            var json = JsonSerializer.Serialize(newBook);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{baseUrl}books", content);

            if (response.IsSuccessStatusCode)
            {
                ConsoleManager.WriteLine("Book created successfully!");
            }
            else
            {
                ConsoleManager.WriteLine($"Failed to create book. Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            ConsoleManager.WriteLine($"Exception: {ex.Message}");
        }
    }

    // ===========================
    // 3. Update Book Menu
    // ===========================
    private static async Task UpdateBookMenuAsync()
    {
        await ListBooksAsync();

        ConsoleManager.Write("\nEnter the ID of the book to update: ");
        if (!int.TryParse(Console.ReadLine(), out int id))
        {
            ConsoleManager.WriteLine("Invalid ID.");
            return;
        }

        var currentBook = await GetBookByIdAsync(id);
        if (currentBook == null)
        {
            ConsoleManager.WriteLine("Book not found.");
            return;
        }

        ConsoleManager.WriteLine("\nCurrent book info:");
        ConsoleManager.WriteLine(Utils.FormatObject(currentBook));

        bool updating = true;
        while (updating)
        {
            ConsoleManager.WriteLine("\n--- Update Menu ---");
            ConsoleManager.WriteLine("1. Update Title");
            ConsoleManager.WriteLine("2. Update Publication Year");
            ConsoleManager.WriteLine("3. Quit");
            ConsoleManager.Write("Choose an option: ");
            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    await UpdateBookAsync(id, "title");
                    break;
                case "2":
                    await UpdateBookAsync(id, "year");
                    break;
                case "3":
                    updating = false;
                    ConsoleManager.WriteLine("Returning to main menu...");
                    break;
                default:
                    ConsoleManager.WriteLine("Invalid choice. Try again.");
                    break;
            }
        }
    }

    // Thực hiện cập nhật từng phần
    private static async Task UpdateBookAsync(int id, string field)
    {
        var updatedBook = new Book();

        if (field == "title")
        {
            ConsoleManager.Write("Enter new title: ");
            updatedBook.Title = Console.ReadLine() ?? "";
        }
        else if (field == "year")
        {
            ConsoleManager.Write("Enter new publication year: ");
            if (int.TryParse(Console.ReadLine(), out int newYear))
            {
                updatedBook.PublicationYear = newYear;
            }
            else
            {
                ConsoleManager.WriteLine("Invalid year.");
                return;
            }
        }

        try
        {
            var json = JsonSerializer.Serialize(updatedBook);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PutAsync($"{baseUrl}books/{id}", content);

            if (response.IsSuccessStatusCode)
            {
                ConsoleManager.WriteLine("Book updated successfully!");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                ConsoleManager.WriteLine("Book not found.");
            }
            else
            {
                ConsoleManager.WriteLine($"Failed to update book. Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            ConsoleManager.WriteLine($"Exception: {ex.Message}");
        }
    }

    // ===========================
    // 4. Delete Book
    // ===========================
    private static async Task DeleteBookAsync()
    {
        await ListBooksAsync();

        ConsoleManager.Write("\nEnter the ID of the book to delete: ");
        if (!int.TryParse(Console.ReadLine(), out int id))
        {
            ConsoleManager.WriteLine("Invalid ID.");
            return;
        }

        try
        {
            var response = await client.DeleteAsync($"{baseUrl}books/{id}");

            if (response.IsSuccessStatusCode)
            {
                ConsoleManager.WriteLine("Book deleted successfully!");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                ConsoleManager.WriteLine("Book not found.");
            }
            else
            {
                ConsoleManager.WriteLine($"Failed to delete book. Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            ConsoleManager.WriteLine($"Exception: {ex.Message}");
        }
    }

    private static async Task<Book?> GetBookByIdAsync(int id)
    {
        try
        {
            var response = await client.GetAsync($"{baseUrl}books/{id}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var book = JsonSerializer.Deserialize<Book>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return book;
            }
        }
        catch (Exception ex)
        {
            ConsoleManager.WriteLine($"Exception: {ex.Message}");
        }
        return null;
    }
}