using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

// ===== MODEL =====
public class Book
{
    public int BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? PublicationYear { get; set; }
}

class Program
{
    private static readonly HttpClient client = new HttpClient();

    static async Task Main(string[] args)
    {
        bool running = true;

        while (running)
        {
            Console.Clear();
            Console.WriteLine("====== Library Client ======");
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
                    await UpdateBookAsync();
                    break;
                case "4":
                    await DeleteBookAsync();
                    break;
                case "5":
                    running = false;
                    Console.WriteLine("Goodbye!");
                    break;
                default:
                    Console.WriteLine("Invalid choice. Press Enter to try again...");
                    Console.ReadLine();
                    break;
            }
        }
    }

    // ==========================
    // Hàm hiển thị sách không dừng chương trình
    // ==========================
    private static async Task<List<Book>> DisplayBooksAsync()
    {
        var response = await client.GetAsync("http://localhost:8080/books");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error: {response.StatusCode}");
            return new List<Book>();
        }

        var json = await response.Content.ReadAsStringAsync();

        var books = JsonSerializer.Deserialize<List<Book>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Console.WriteLine("====== Book List ======");
        if (books != null && books.Count > 0)
        {
            foreach (var book in books)
            {
                Console.WriteLine($"ID: {book.BookId} | Title: {book.Title} | Year: {book.PublicationYear}");
            }
        }
        else
        {
            Console.WriteLine("No books found.");
        }

        return books ?? new List<Book>();
    }

    // ==========================
    // 1. List Books
    // ==========================
    private static async Task ListBooksAsync()
    {
        Console.Clear();
        await DisplayBooksAsync();
        Console.WriteLine("\nPress Enter to return to menu...");
        Console.ReadLine();
    }

    // ==========================
    // 2. Create Book
    // ==========================
    private static async Task CreateBookAsync()
    {
        Console.Clear();
        Console.WriteLine("====== Create Book ======");
        Console.Write("Enter Title: ");
        var title = Console.ReadLine();

        Console.Write("Enter Publication Year: ");
        int year = int.Parse(Console.ReadLine() ?? "0");

        var newBook = new Book { Title = title, PublicationYear = year };
        var json = JsonSerializer.Serialize(newBook);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("http://localhost:8080/books", content);

        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Created)
        {
            Console.WriteLine("Book created successfully!");
        }
        else
        {
            Console.WriteLine("Failed to create book.");
        }

        Console.WriteLine("Press Enter to return to menu...");
        Console.ReadLine();
    }

    // ==========================
    // 3. Update Book
    // ==========================
    private static async Task UpdateBookAsync()
    {
        Console.Clear();
        Console.WriteLine("====== Update Book ======");

        // Hiển thị danh sách, không dừng lại
        await DisplayBooksAsync();

        Console.Write("\nEnter ID of the book to update: ");
        int id = int.Parse(Console.ReadLine() ?? "0");

        Console.WriteLine("Choose field to update:");
        Console.WriteLine("1. Title");
        Console.WriteLine("2. Publication Year");
        var fieldChoice = Console.ReadLine();

        var updatedBook = new Book();

        if (fieldChoice == "1")
        {
            Console.Write("Enter new Title: ");
            updatedBook.Title = Console.ReadLine() ?? "";
        }
        else if (fieldChoice == "2")
        {
            Console.Write("Enter new Publication Year: ");
            updatedBook.PublicationYear = int.Parse(Console.ReadLine() ?? "0");
        }
        else
        {
            Console.WriteLine("Invalid choice.");
            Console.WriteLine("Press Enter to return to menu...");
            Console.ReadLine();
            return;
        }

        var json = JsonSerializer.Serialize(updatedBook);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PutAsync($"http://localhost:8080/books/{id}", content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Book updated successfully!");
        }
        else
        {
            Console.WriteLine($"Failed to update book. Status: {response.StatusCode}");
        }

        Console.WriteLine("Press Enter to return to menu...");
        Console.ReadLine();
    }

    // ==========================
    // 4. Delete Book
    // ==========================
    private static async Task DeleteBookAsync()
    {
        Console.Clear();
        Console.WriteLine("====== Delete Book ======");

        // Hiển thị danh sách, không dừng lại
        await DisplayBooksAsync();

        Console.Write("\nEnter ID of the book to delete: ");
        int id = int.Parse(Console.ReadLine() ?? "0");

        var response = await client.DeleteAsync($"http://localhost:8080/books/{id}");

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("Book deleted successfully!");
        }
        else
        {
            Console.WriteLine($"Failed to delete book. Status: {response.StatusCode}");
        }

        Console.WriteLine("Press Enter to return to menu...");
        Console.ReadLine();
    }
}
