
using Microsoft.Data.SqlClient;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

const int pageSize = 5;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

//получаем сервис IConfiguration, через свойство Services
var configurationService = app.Services.GetService<IConfiguration>();

//с помощью индексатора обращаемся к нужной строке подключения.
//необходимо указать полный путь к сукции, через двоеточие
string connectionString = configurationService["ConnectionStrings:DefaultConnection"];

app.Run(async (context) =>
{
    var response = context.Response;
    var request = context.Request;
    response.ContentType = "text/html; charset=utf-8";

    if (request.Path.StartsWithSegments("/"))
    {
        List<Button> buttons = new List<Button>()
        {
            new Button{ Link = "/editUser", Text = "Update", Class = "btn btn-warning"},
            new Button{ IsForm = true, Action = "/removeUser", Text = "Delete", Class = "btn btn-danger"},
        };

        uint currentPage = 1;
        if (request.Query.ContainsKey("page"))
        {
            uint.TryParse(request.Query["page"], out currentPage);
        }

        int usersCount = 0;
        List<User> users = new List<User>();
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            SqlCommand command = new SqlCommand("select COUNT(*) from Users", connection);
            usersCount = (int)await command.ExecuteScalarAsync();


            command.CommandText = $"select Id, Name, Age from Users Order by Id OFFSET {(currentPage - 1) * pageSize} ROWS FETCH NEXT {pageSize} ROWS ONLY";
            using (SqlDataReader reader = await command.ExecuteReaderAsync())
            {
                if (reader.HasRows)
                {
                    while (await reader.ReadAsync())
                    {
                        users.Add(new User(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2)));
                    }
                }
            }
        }
        await response.WriteAsync(GenerateHtmlPage("""
            <a class="btn btn-primary" href="/addUser">Create user</a>
            """ + ToTable(users, buttons), "All Users from DataBase", usersCount, currentPage, pageSize));
    }

    else if (request.Path.StartsWithSegments("/removeUser") && context.Request.Method == "POST")
    {
        await RemoveUser(context);
    }

    else if (request.Path.StartsWithSegments("/addUser") && context.Request.Method == "POST")
    {
        await CreateUser(context);
    }

    else if (request.Path.StartsWithSegments("/addUser"))
    {
        await response.WriteAsync(GenerateHtmlPage(GetUserCreateForm(), "Create user", isPaging: false));
    }

    else if (request.Path.StartsWithSegments("/editUser") && context.Request.Method == "POST")
    {
        await GetUser(context);
    }

    else if (request.Path.StartsWithSegments("/editUser"))
    {
        await GetUser(context);
    }

    else
    {
        response.StatusCode = 404;
        await response.WriteAsJsonAsync("Page Not Found");
    }
});
   
async Task UpdateUser(HttpContext context)
{
    string? id = context.Request.Form["id"];
    string? name = context.Request.Form["name"];
    string? age = context.Request.Form["age"];

    using (SqlConnection connection = new SqlConnection(connectionString))
    {
        await connection.OpenAsync();
        SqlCommand command = new SqlCommand($"update [Users] set [Name] = '{name}', [Age] = {age} WHERE [Id] = {id}", connection);
        command.ExecuteNonQuery();
    }
    context.Response.Redirect("/");
}

async Task GetUser(HttpContext context)
{
    string? id = context.Request.Query["id"];
    if (int.TryParse(id, out int userId))
    {
        User? user = null;
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            SqlCommand command = new SqlCommand($"select [Id], [Name], [Age] from [Users] WHERE [Id] = {userId}", connection);
            using (SqlDataReader reader = await command.ExecuteReaderAsync())
            {
                if (reader.HasRows)
                {
                    reader.Read();
                    user = new User(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2));
                }
            }
        }
        if (user != null)
        {
            await context.Response.WriteAsync(GenerateHtmlPage(GetUserUpdateForm(user), "Update user", isPaging: false));
        }
    }
    //context.Response.Redirect("/"); из-за нее не показывается форма для обновления пользователя, а так показывается, но не переходит на главную страницу
}

async Task CreateUser(HttpContext context)
{
    string? name = context.Request.Form["name"];
    string? age = context.Request.Form["age"];

    using (SqlConnection connection = new SqlConnection(connectionString))
    {
        await connection.OpenAsync();
        SqlCommand command = new SqlCommand($"insert into [Users] ([Name], [Age]) values ('{name}', {age})", connection);
        command.ExecuteNonQuery();
    }
    context.Response.Redirect("/");
}

async Task RemoveUser(HttpContext context)
{
    string? id = context.Request.Form["id"];

    if (int.TryParse(id, out int userId))
        {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            SqlCommand command = new SqlCommand($"delete from [Users] WHERE [Id] = {userId}", connection);
            command.ExecuteNonQuery();
        }
        context.Response.Redirect("/");
    }
}

app.Run();


static string ToTable<T>(IEnumerable<T> collection, List<Button>? buttons = null)
{
    StringBuilder tableHtml = new StringBuilder();
    tableHtml.Append("<table class=\"table\">");
    PropertyInfo[] properties = typeof(T).GetProperties();
    tableHtml.Append("<tr>");
    foreach (PropertyInfo property in properties)
    {
        tableHtml.Append($"<th>{property.Name}</th>");
    }
    for (int i = 0; i < buttons.Count; i++)
    {
        tableHtml.Append("<th></th>");
    }
    tableHtml.Append("</tr>");

    foreach (T item in collection)
    {
        object idProperty = null;

        tableHtml.Append("<tr>");
        foreach (PropertyInfo property in properties)
        {
            object value = property.GetValue(item);
            tableHtml.Append($"<td> {value} </td>");

            if (property.Name.Equals("Id"))
            {
                idProperty = value!;
            }
        }
        if (buttons is not null)
        {
            foreach (Button button in buttons)
            {
                tableHtml.Append("<td>");

                if (button.IsForm)
                {
                    tableHtml.Append($"<form method=\"{button.Method}\" action=\"{button.Action}\"><input type=\"hidden\" name=\"id\" value=\"{idProperty}\">" +
                        $"<button class=\"{button.Class}\">{button.Text}</button></form>");
                }
                else
                {
                    tableHtml.Append($"<a href=\"{button.Link}?id={idProperty}\" class=\"{button.Class}\">{button.Text}</a>");
                }

                tableHtml.Append("</td>");
            }
        }
        tableHtml.Append("</tr>");
    }
    tableHtml.Append("</table>");
    return tableHtml.ToString();
}
static string BuildHtmlTable<T>(IEnumerable<T> collection)
{
    StringBuilder tableHtml = new StringBuilder();
    tableHtml.Append("<table class=\"table\">");

    PropertyInfo[] properties = typeof(T).GetProperties();

    tableHtml.Append("<tr>");
    foreach (PropertyInfo property in properties)
    {
        tableHtml.Append($"<th>{property.Name}</th>");
    }
    tableHtml.Append("</tr>");

    foreach (T item in collection)
    {
        tableHtml.Append("<tr>");
        foreach (PropertyInfo property in properties)
        {
            object value = property.GetValue(item);
            tableHtml.Append($"<td>{value}</td>");
        }
        tableHtml.Append("</tr>");
    }

    tableHtml.Append("</table>");
    return tableHtml.ToString();
}

static string GetPaginationSection(int usersCount, uint currentPage, int pageSize = pageSize)
{
    int totalPages = usersCount / pageSize;
    if (usersCount % pageSize > 0)
    {
        totalPages += 1;
    }
    string html = $"""
        <nav aria-label="Page navigation example">
            <ul class="pagination">
                <li class="page-item"><a class="page-link {(currentPage <= 1 ? "disabled" : "")}" href="/?page={currentPage - 1}">Previous</a></li>
                <li class="page-item"><a class="page-link {(currentPage >= totalPages ? "disabled" : "")}" href="/?page={currentPage + 1}">Next</a></li>
            </ul>
        </nav>
        """;

    return html;
}

static string GenerateHtmlPage(string body, string header, int pagesCount = 1, uint currentPage = 1, int pageSize = pageSize, bool isPaging = true)
{
    string html = $"""
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset="utf-8" />
            <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0-alpha3/dist/css/bootstrap.min.css" rel="stylesheet" 
            integrity="sha384-KK94CHFLLe+nY2dmCWGMq91rCGa5gtU4mk92HdvYe+M/SXH301p5ILy+dN9+nJOZ" crossorigin="anonymous">
            <title>{header}</title>
        </head>
        <body>
        <div class="container">
        <h2 class="d-flex justify-content-center">{header}</h2>
        <div class="mt-5"></div>
        {body}
        {(isPaging ? GetPaginationSection(pagesCount, currentPage, pageSize) : "")}
            <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0-alpha3/dist/js/bootstrap.bundle.min.js" 
            integrity="sha384-ENjdO4Dr2bkBIFxQpeoTz1HIcje39Wm4jDKdf19U8gI4ddQ3GYNS7NTKfAdVQSZe" crossorigin="anonymous"></script>
        </div>
        </body>
        </html>
        """;
    return html;
}

static string GetUserCreateForm()
{
    return """
        <form action="/addUser" method="post">
        <div class="form-group">
            <label for="name">Name:</label>
            <input type="text" name="name" class="form-control" required>
        </div>
        <div class="form-group">
            <label for="age">Age:</label>
            <input type="number" name="age" class="form-control" required>
        </div>
        <div class="form-group mt-3">
            <input type="submit" value="Submit" class="btn btn-primary">
        </div>
        </form>
        """;
}

static string GetUserUpdateForm(User user)
{
    return $"""
        <form action="/editUser" method="post">
        <input type="hidden" name="id" value="{user.Id}" >
        <div class="form-group">
          <label for="name">Name:</label>
          <input type="text" name="name" class="form-control" value="{user.Name}" required>
        </div>
        <div class="form-group">
          <label for="age">Age:</label>
          <input type="number" name="age" class="form-control" value="{user.Age}" required>
        </div>
        <div class="form-group mt-3">
          <input type="submit" value="Submit" class="btn btn-primary">
          </div>
        </form>
        """;
}


record User(int Id, string Name, int Age)
{
    public User(string Name, int Age) : this(0, Name, Age) 
    {
    
    }
}


public class Button
{
    public string Text { get; set; }
    public bool IsForm { get; set; } = false;
    public string? Link { get; set; }
    public string? Class { get; set; } = "btn btn-submit";
    public string? Action { get; set; }
    public string? Method { get; set; } = "post";
}