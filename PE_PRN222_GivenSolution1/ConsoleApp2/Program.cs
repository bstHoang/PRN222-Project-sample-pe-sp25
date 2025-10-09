using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

// ============================
// MODEL
// ============================
public class Book
{
    public int BookId { get; set; }
    public string Title { get; set; } = "";
    public int? PublicationYear { get; set; }
    public Genre? GenreId { get; set; }
}

public class Genre
{
    public int GenreId { get; set; }
    public string GenreName { get; set; }
}

public static class FakeDatabase
{
    public static List<Genre> Genres = new List<Genre>
    {
        new Genre { GenreId = 1, GenreName = "Fantasy" },
        new Genre { GenreId = 2, GenreName = "Dystopian" },
        new Genre { GenreId = 3, GenreName = "Mystery" },
        new Genre { GenreId = 4, GenreName = "Thriller" },
        new Genre { GenreId = 5, GenreName = "Non-fiction" },
        new Genre { GenreId = 6, GenreName = "Science Fiction" }
    };

    public static List<Book> Books = new List<Book>
    {
        new Book { BookId = 1, Title = "Harry Potter and the Philosopher's Stone", PublicationYear = 1997, GenreId =  Genres[1]},
        new Book { BookId = 2, Title = "1984", PublicationYear = 1949, GenreId = Genres[1] },
        new Book { BookId = 3, Title = "The Hobbit", PublicationYear = 1937, GenreId = Genres[1] },
        new Book { BookId = 4, Title = "Murder on the Orient Express", PublicationYear = 1934, GenreId = Genres[1]  },
        new Book { BookId = 5, Title = "The Da Vinci Code", PublicationYear = 2003, GenreId =  Genres[1]},
        new Book { BookId = 6, Title = "Sapiens: A Brief History of Humankind", PublicationYear = 2011, GenreId =  Genres[1]},
        new Book { BookId = 7, Title = "The Handmaid's Tale", PublicationYear = 1985, GenreId = Genres[1]  },
        new Book { BookId = 8, Title = "Foundation", PublicationYear = 1951, GenreId =  Genres[1] },
        new Book { BookId = 9, Title = "I, Robot", PublicationYear = 1950, GenreId =  Genres[1]   },
        new Book { BookId = 10, Title = "Foundation and Empire", PublicationYear = 1952, GenreId = Genres[1] }
    };



    public static int GetNextBookId()
    {
        return Books.Count == 0 ? 1 : Books.Max(b => b.BookId) + 1;
    }
}

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Đọc cấu hình từ appsettings.json
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        string serverUrl = config["ServerUrl"];

        // 2. Khởi tạo HttpListener với URL từ config
        var listener = new HttpListener();
        listener.Prefixes.Add(serverUrl);
        listener.Start();
        Console.WriteLine($"Server started at {serverUrl}");

        while (true)
        {
            var context = await listener.GetContextAsync();
            var request = context.Request;
            var response = context.Response;

            var path = request.Url.AbsolutePath.ToLower();
            var method = request.HttpMethod;

            Console.WriteLine($"{method} {path}");

            // =======================
            // 1. GET /books - List
            // =======================
            if (path == "/books" && method == "GET")
            {
                await WriteJsonResponse(response, FakeDatabase.Books);
            }
            // =======================
            // 2. POST /books - Create
            // =======================
            else if (path == "/books" && method == "POST")
            {
                using var reader = new StreamReader(request.InputStream);
                var body = await reader.ReadToEndAsync();

                var newBook = JsonSerializer.Deserialize<Book>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (newBook != null && !string.IsNullOrWhiteSpace(newBook.Title))
                {
                    newBook.BookId = FakeDatabase.GetNextBookId();
                    FakeDatabase.Books.Add(newBook);
                    response.StatusCode = 201; // Created
                    await WriteJsonResponse(response, newBook);
                }
                else
                {
                    response.StatusCode = 400; // Bad Request
                }
            }
            // =======================
            // 3. PUT /books/{id} - Update
            // =======================
            else if (path.StartsWith("/books/") && method == "PUT")
            {
                if (int.TryParse(path.Replace("/books/", ""), out int id))
                {
                    var book = FakeDatabase.Books.FirstOrDefault(b => b.BookId == id);
                    if (book == null)
                    {
                        response.StatusCode = 404;
                    }
                    else
                    {
                        using var reader = new StreamReader(request.InputStream);
                        var body = await reader.ReadToEndAsync();

                        var updatedBook = JsonSerializer.Deserialize<Book>(body, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (updatedBook != null)
                        {
                            if (!string.IsNullOrWhiteSpace(updatedBook.Title))
                                book.Title = updatedBook.Title;

                            if (updatedBook.PublicationYear.HasValue)
                                book.PublicationYear = updatedBook.PublicationYear;

                            response.StatusCode = 200;
                            await WriteJsonResponse(response, book);
                        }
                        else
                        {
                            response.StatusCode = 400;
                        }
                    }
                }
                else
                {
                    response.StatusCode = 400;
                }
            }
            // =======================
            // 4. DELETE /books/{id} - Delete
            // =======================
            else if (path.StartsWith("/books/") && method == "DELETE")
            {
                if (int.TryParse(path.Replace("/books/", ""), out int id))
                {
                    var book = FakeDatabase.Books.FirstOrDefault(b => b.BookId == id);
                    if (book == null)
                    {
                        response.StatusCode = 404;
                    }
                    else
                    {
                        FakeDatabase.Books.Remove(book);
                        response.StatusCode = 200;
                        await WriteJsonResponse(response, new { message = "Deleted successfully" });
                    }
                }
                else
                {
                    response.StatusCode = 400;
                }
            }
            // =======================
            // GET /books/{id}
            // =======================
            else if (path.StartsWith("/books/") && method == "GET")
            {
                if (int.TryParse(path.Replace("/books/", ""), out int id))
                {
                    var book = FakeDatabase.Books.FirstOrDefault(b => b.BookId == id);
                    if (book == null)
                    {
                        response.StatusCode = 404;
                    }
                    else
                    {
                        response.StatusCode = 200;
                        await WriteJsonResponse(response, book);
                    }
                }
                else
                {
                    response.StatusCode = 400;
                }
            }
            else
            {
                response.StatusCode = 404;
            }


            response.Close();
        }
    }

    // Helper method to write JSON response
    private static async Task WriteJsonResponse(HttpListenerResponse response, object data)
    {
        var json = JsonSerializer.Serialize(data);
        var buffer = Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json";
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }
}
