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
    //public List<Genre>? GenreId { get; set; }
    public Genre? GenreId { get; set; }
}

public class Genre
{
    public int GenreId { get; set; }
    public string GenreName { get; set; }
}

class Program
{
    private static readonly HttpClient client = new HttpClient();
    private static string baseUrl = "";

    static async Task Main(string[] args)
    {
        // ======================================
        // 1. Load cấu hình từ appsettings.json
        // ======================================
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        baseUrl = config["BaseUrl"] ;
        Console.WriteLine($"Base URL: {baseUrl}");

        bool running = true;

        while (running)
        {
            Console.WriteLine("\n====== Library Client ======");
            Console.WriteLine("1. List Books");
            Console.WriteLine("2. Create Book");
            Console.WriteLine("3. Update Book");
            Console.WriteLine("4. Delete Book");
            Console.WriteLine("5. Quit");
            Console.Write("Choose an option: ");

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
                    Console.WriteLine("Goodbye!");
                    break;
                default:
                    Console.WriteLine("Invalid choice. Try again.");
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

                Console.WriteLine("\n====== Book List ======");
                if (books != null && books.Count > 0)
                {
                    foreach (var book in books)
                    {
                        Console.WriteLine(Utils.Stringify(book));
                    }
                }
                else
                {
                    Console.WriteLine("No books found.");
                }
            }
            else
            {
                Console.WriteLine($"Error: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
    }

    // ===========================
    // 2. Create Book
    // ===========================
    private static async Task CreateBookAsync()
    {
        Console.Write("Enter title: ");
        string title = Console.ReadLine() ?? "";

        Console.Write("Enter publication year: ");
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
                Console.WriteLine("Book created successfully!");
            }
            else
            {
                Console.WriteLine($"Failed to create book. Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
    }

    // ===========================
    // 3. Update Book Menu
    // ===========================
    private static async Task UpdateBookMenuAsync()
    {
        await ListBooksAsync();

        Console.Write("\nEnter the ID of the book to update: ");
        if (!int.TryParse(Console.ReadLine(), out int id))
        {
            Console.WriteLine("Invalid ID.");
            return;
        }

        var currentBook = await GetBookByIdAsync(id);
        if (currentBook == null)
        {
            Console.WriteLine("Book not found.");
            return;
        }

        Console.WriteLine("\nCurrent book info:");
        Console.WriteLine(Utils.FormatObject(currentBook));

        bool updating = true;
        while (updating)
        {
            Console.WriteLine("\n--- Update Menu ---");
            Console.WriteLine("1. Update Title");
            Console.WriteLine("2. Update Publication Year");
            Console.WriteLine("3. Quit");
            Console.Write("Choose an option: ");
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
                    Console.WriteLine("Returning to main menu...");
                    break;
                default:
                    Console.WriteLine("Invalid choice. Try again.");
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
            Console.Write("Enter new title: ");
            updatedBook.Title = Console.ReadLine() ?? "";
        }
        else if (field == "year")
        {
            Console.Write("Enter new publication year: ");
            if (int.TryParse(Console.ReadLine(), out int newYear))
            {
                updatedBook.PublicationYear = newYear;
            }
            else
            {
                Console.WriteLine("Invalid year.");
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
                Console.WriteLine("Book updated successfully!");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine("Book not found.");
            }
            else
            {
                Console.WriteLine($"Failed to update book. Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
    }

    // ===========================
    // 4. Delete Book
    // ===========================
    private static async Task DeleteBookAsync()
    {
        await ListBooksAsync();

        Console.Write("\nEnter the ID of the book to delete: ");
        if (!int.TryParse(Console.ReadLine(), out int id))
        {
            Console.WriteLine("Invalid ID.");
            return;
        }

        try
        {
            var response = await client.DeleteAsync($"{baseUrl}books/{id}");

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Book deleted successfully!");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine("Book not found.");
            }
            else
            {
                Console.WriteLine($"Failed to delete book. Status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
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
            Console.WriteLine($"Exception: {ex.Message}");
        }
        return null;
    }

}
