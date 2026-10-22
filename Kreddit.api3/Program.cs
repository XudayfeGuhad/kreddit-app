using System.Text.Json.Serialization;
using Kreddit.Api.Data;
using Kreddit.api3.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=kreddit.db"));

// CORS: tillad Blazor-klienten at kalde API'et
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.WithOrigins("http://localhost:5202")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Undgå "object cycle"-fejl ved serialisering
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

var app = builder.Build();

// Testdata ved opstart (kun hvis databasen er tom)
// Data gemmes dog nu når man opretter et post eller comment
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Posts.Any())
    {
        var anna = new User { Username = "Anna" };
        var bo = new User { Username = "Bo" };
        var carla = new User { Username = "Carla" };

        var post1 = new Post
        {
            Title = "Velkommen til Kreddit",
            Content = "Dette er den første tråd. Skriv gerne en kommentar!",
            User = anna,
            CreatedAt = DateTime.UtcNow.AddHours(-3),
            Upvotes = 5,
            Downvotes = 1
        };
        post1.Comments.Add(new Comment
        {
            Content = "Fedt med en ny side!",
            User = bo,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            Upvotes = 2
        });
        post1.Comments.Add(new Comment
        {
            Content = "Enig, det ser godt ud.",
            User = carla,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });

        var post2 = new Post
        {
            Title = "Microsofts dokumentation til Blazor",
            Content = "https://learn.microsoft.com/aspnet/core/blazor",
            User = bo,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            Upvotes = 8
        };
        post2.Comments.Add(new Comment
        {
            Content = "Nyttigt link, tak.",
            User = anna,
            CreatedAt = DateTime.UtcNow.AddMinutes(-90),
            Upvotes = 1
        });

        var post3 = new Post
        {
            Title = "Hvad laver I i weekenden?",
            Content = "Jeg skal kode på miniprojektet :)",
            User = carla,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30)
        };

        db.Posts.AddRange(post1, post2, post3);
        db.SaveChanges();
    }
}

app.UseCors("AllowBlazor");

// Finder en bruger med det navn, eller opretter en ny
static async Task<User> GetOrCreateUser(AppDbContext db, string? username)
{
    var name = string.IsNullOrWhiteSpace(username) ? "Anonym" : username.Trim();

    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == name);
    if (user is null)
    {
        user = new User { Username = name };
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
    return user;
}

// GET /posts/  (nyeste først)
app.MapGet("/posts/", async (AppDbContext db) =>
    await db.Posts
        .Include(p => p.User)
        .Include(p => p.Comments)
        .ThenInclude(c => c.User)
        .OrderByDescending(p => p.CreatedAt)
        .ToListAsync());

// GET /posts/{id}/
app.MapGet("/posts/{id}/", async (int id, AppDbContext db) =>
{
    var post = await db.Posts
        .Include(p => p.User)
        .Include(p => p.Comments.OrderBy(c => c.CreatedAt))
        .ThenInclude(c => c.User)
        .FirstOrDefaultAsync(p => p.Id == id);

    return post is null
        ? Results.NotFound()
        : Results.Ok(post);
});

// POST /posts
app.MapPost("/posts", async (Post post, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(post.Title))
        return Results.BadRequest("Overskrift mangler.");

    post.Id = 0;
    post.CreatedAt = DateTime.UtcNow;
    post.Upvotes = 0;
    post.Downvotes = 0;
    post.Comments = new();
    post.User = await GetOrCreateUser(db, post.User?.Username);

    db.Posts.Add(post);
    await db.SaveChangesAsync();

    return Results.Created($"/posts/{post.Id}", post);
});

// POST /posts/{id}/comments
app.MapPost("/posts/{id}/comments",
    async (int id, Comment comment, AppDbContext db) =>
{
    var post = await db.Posts
        .Include(p => p.Comments)
        .FirstOrDefaultAsync(p => p.Id == id);

    if (post is null)
        return Results.NotFound();

    if (string.IsNullOrWhiteSpace(comment.Content))
        return Results.BadRequest("Kommentaren er tom.");

    comment.Id = 0;
    comment.CreatedAt = DateTime.UtcNow;
    comment.Upvotes = 0;
    comment.Downvotes = 0;
    comment.User = await GetOrCreateUser(db, comment.User?.Username);

    post.Comments.Add(comment);
    await db.SaveChangesAsync();

    return Results.Created($"/posts/{id}/comments", comment);
});

// PUT /posts/{id}/upvote/
app.MapPut("/posts/{id}/upvote/",
    async (int id, AppDbContext db) =>
{
    var post = await db.Posts.FindAsync(id);
    if (post is null) return Results.NotFound();

    post.Upvotes++;
    await db.SaveChangesAsync();
    return Results.Ok(post);
});

// PUT /posts/{id}/downvote/
app.MapPut("/posts/{id}/downvote/",
    async (int id, AppDbContext db) =>
{
    var post = await db.Posts.FindAsync(id);
    if (post is null) return Results.NotFound();

    post.Downvotes++;
    await db.SaveChangesAsync();
    return Results.Ok(post);
});

// PUT /posts/{postid}/comments/{commentid}/upvote/
app.MapPut("/posts/{postid}/comments/{commentid}/upvote/",
    async (int postid, int commentid, AppDbContext db) =>
{
    var comment = await db.Posts
        .Where(p => p.Id == postid)
        .SelectMany(p => p.Comments)
        .FirstOrDefaultAsync(c => c.Id == commentid);

    if (comment is null) return Results.NotFound();

    comment.Upvotes++;
    await db.SaveChangesAsync();
    return Results.Ok(comment);
});

// PUT /posts/{postid}/comments/{commentid}/downvote/
app.MapPut("/posts/{postid}/comments/{commentid}/downvote/",
    async (int postid, int commentid, AppDbContext db) =>
{
    var comment = await db.Posts
        .Where(p => p.Id == postid)
        .SelectMany(p => p.Comments)
        .FirstOrDefaultAsync(c => c.Id == commentid);

    if (comment is null) return Results.NotFound();

    comment.Downvotes++;
    await db.SaveChangesAsync();
    return Results.Ok(comment);
});

app.Run();