using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Project11.Models;
using System.Net;
using System.Text.Json;

var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false);

var config = builder.Build();

var optionsBuilder = new DbContextOptionsBuilder<LibraryContext>();
optionsBuilder.UseSqlServer(config.GetConnectionString("LibraryDb"));

var listener = new HttpListener();
listener.Prefixes.Add("http://localhost:8080/");
listener.Start();
Console.WriteLine("Server started at http://localhost:8080/");

while (true)
{
    var context = await listener.GetContextAsync();
    var request = context.Request;
    var response = context.Response;

    using var db = new LibraryContext(optionsBuilder.Options);

    // Lấy đường dẫn
    var path = request.Url.AbsolutePath.ToLower();
    var method = request.HttpMethod;

    // =======================
    // 1. GET /books - List
    // =======================
    if (path == "/books" && method == "GET")
    {
        var books = db.Books.ToList();

        var json = JsonSerializer.Serialize(books);
        var buffer = System.Text.Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json";
        response.ContentLength64 = buffer.Length;

        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
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

        if (newBook != null)
        {
            db.Books.Add(newBook);
            db.SaveChanges();

            response.StatusCode = 201; // Created
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
        var idStr = path.Replace("/books/", "");
        if (int.TryParse(idStr, out int id))
        {
            var book = db.Books.FirstOrDefault(b => b.BookId == id);
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
                    // Update các trường
                    if (!string.IsNullOrWhiteSpace(updatedBook.Title))
                        book.Title = updatedBook.Title;

                    if (updatedBook.PublicationYear.HasValue)
                        book.PublicationYear = updatedBook.PublicationYear;

                    db.SaveChanges();
                    response.StatusCode = 200;
                }
                else
                {
                    response.StatusCode = 400;
                }
            }
        }
    }
    // =======================
    // 4. DELETE /books/{id} - Delete
    // =======================
    else if (path.StartsWith("/books/") && method == "DELETE")
    {
        var idStr = path.Replace("/books/", "");
        if (int.TryParse(idStr, out int id))
        {
            var book = db.Books.FirstOrDefault(b => b.BookId == id);
            if (book == null)
            {
                response.StatusCode = 404;
            }
            else
            {
                db.Books.Remove(book);
                db.SaveChanges();
                response.StatusCode = 200;
            }
        }
    }
    else
    {
        response.StatusCode = 404;
    }

    response.Close();
}
