using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authorization;
using System.Reflection.Metadata;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme) //This means it will look for an Authorization: Bearer <token...> header in every request.
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
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
        policy => policy.WithOrigins("http://localhost:7042")
                            .AllowAnyHeader()
                            .AllowAnyMethod());
});

builder.Services.AddControllers();
//builder.Services.AddOpenApi();
builder.Services.AddOpenApi( options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new OpenApiInfo { Title = "HMS API", Version = "v1" };

        // Define the OAuth2.0 scheme that's compatible with Keycloak
        var securityScheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow // Use Authorization Code flow with PKCE
                {
                    // Set the URLs based on your Keycloak Authority
                    AuthorizationUrl = new Uri("http://localhost:8080/realms/HMS/protocol/openid-connect/auth"),
                    TokenUrl = new Uri("http://localhost:8080/realms/HMS/protocol/openid-connect/token"),
                    Scopes = new Dictionary<string, string>
                        {
                            // Define scopes your API needs. "openid" is a standard.
                            { "openid", "OpenID Connect" },
                            { "profile", "User Profile" },
                            { "roles", "User Roles" } // You might need this to get roles
                        }
                }
            }
        };

        // Add the security scheme to the document's components
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes.Add("oauth2", securityScheme);

        // Add a security requirement to use the "oauth2" scheme
        var securityRequirement = new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "oauth2" // This "Id" must match the name above
                    }
                },
                new string[] { "openid", "profile", "roles" } // Match the scopes
            }
        };

        document.SecurityRequirements.Add(securityRequirement);

        return Task.CompletedTask;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/openapi/v1.json", "HMS API V1");

        c.OAuthClientId("HMS-frontend"); // Use your frontend's client ID
        c.OAuthAppName("HMS Backend API - Swagger UI");
        c.OAuthUsePkce(); // Use PKCE for better security

        // Tell Swagger UI which scopes to request
        c.OAuthScopes("openid", "profile", "roles");
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowMyBlazorApp"); // Use CORS
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
