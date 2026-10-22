using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using shared.Model;

namespace kreddit_app.Data;

public class ApiService
{
    private readonly HttpClient http;
    private readonly string baseAPI;

    public ApiService(HttpClient http, IConfiguration configuration)
    {
        this.http = http;
        baseAPI = configuration["base_api"]
            ?? throw new InvalidOperationException("'base_api' mangler i wwwroot/appsettings.json");
    }

    // GET /posts/
    public async Task<Post[]> GetPosts()
    {
        var posts = await http.GetFromJsonAsync<Post[]>($"{baseAPI}posts/");
        return posts ?? Array.Empty<Post>();
    }

    // GET /posts/{id}/  (null hvis tråden ikke findes)
    public async Task<Post?> GetPost(int id)
    {
        var response = await http.GetAsync($"{baseAPI}posts/{id}/");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Post>();
    }

    // POST /posts   (enten content eller url)
    // POST /posts
    public async Task<Post> CreatePost(string title, string content, string username)
    {
        var post = new Post
        {
            Title = title,
            Content = content,
            User = new User { Username = username }
        };

        var response = await http.PostAsJsonAsync($"{baseAPI}posts", post);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<Post>();
        return created ?? throw new Exception("Failed to create post.");
    }

    // POST /posts/{id}/comments
    public async Task<Comment> CreateComment(int postId, string content, string username)
    {
        var comment = new Comment
        {
            Content = content,
            User = new User { Username = username }
        };

        var response = await http.PostAsJsonAsync($"{baseAPI}posts/{postId}/comments", comment);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<Comment>();
        return created ?? throw new Exception("Failed to create comment.");
    }

    // PUT /posts/{id}/upvote/ eller /downvote/
    public async Task VotePost(int postId, bool up)
    {
        var action = up ? "upvote" : "downvote";
        var response = await http.PutAsync($"{baseAPI}posts/{postId}/{action}/", null);
        response.EnsureSuccessStatusCode();
    }

    // PUT /posts/{postId}/comments/{commentId}/upvote/ eller /downvote/
    public async Task VoteComment(int postId, int commentId, bool up)
    {
        var action = up ? "upvote" : "downvote";
        var response = await http.PutAsync($"{baseAPI}posts/{postId}/comments/{commentId}/{action}/", null);
        response.EnsureSuccessStatusCode();
    }
}