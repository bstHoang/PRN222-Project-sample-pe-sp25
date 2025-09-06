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
    var response = context.Response;

    if (context.Request.Url.AbsolutePath == "/books")
    {
        using var db = new LibraryContext(optionsBuilder.Options);
        var books = db.Books.ToList();

        var json = JsonSerializer.Serialize(books);
        var buffer = System.Text.Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json";
        response.ContentLength64 = buffer.Length;

        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
    }
    else
    {
        response.StatusCode = 404;
    }

    response.Close();
}
