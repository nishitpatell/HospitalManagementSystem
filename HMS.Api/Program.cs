using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme) //This means it will look for an Authorization: Bearer <token...> header in every request.
    .AddJwtBearer(options =>
    {
        options.Authority = "http://localhost:8080/realms/HMS"; // KeyCloak realm 
        options.Audience = "HMS-frontend"; // Client ID

        // We need this to make it work with Keycloak's roles
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;

        // It tells the API how to find the roles in the token.
        // Keycloak nests roles under "realm_access" > "roles".
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is ClaimsIdentity identity)
                {
                    // Find the "realm_access" claim
                    var realmAccessClaim = identity.FindFirst("realm_access");

                    if (realmAccessClaim != null)
                    {
                        // Parse the JSON inside the claim
                        var realmAccess = System.Text.Json.JsonDocument.Parse(realmAccessClaim.Value);

                        // Get the "roles" array
                        if (realmAccess.RootElement.TryGetProperty("roles", out var rolesElement))
                        {
                            var roles = rolesElement.EnumerateArray()
                                .Select(role => new Claim(ClaimTypes.Role, role.GetString()!));

                            // Add each role as a new ClaimTypes.Role
                            identity.AddClaims(roles);
                        }
                    }
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMyBlazorApp",
        policy => policy.WithOrigins("http://localhost:5000")
                            .AllowAnyHeader()
                            .AllowAnyMethod());
});
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/openapi/v1.json", "My API V1");
        // Optionally set c.RoutePrefix = string.Empty; to host at root
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowMyBlazorApp"); // Use CORS
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
