# Use the official .NET 8 SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["InvoiceExpenseSystem.csproj", "."]
RUN dotnet restore "./InvoiceExpenseSystem.csproj"

# Copy the rest of the code and build it
COPY . .
RUN dotnet publish "InvoiceExpenseSystem.csproj" -c Release -o /app/publish

# Use the ASP.NET runtime image to run the app
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Create the Uploads directory
RUN mkdir -p /app/Uploads

# Set the environment to Production
ENV ASPNETCORE_ENVIRONMENT=Production
# Expose the port Render uses (Render default is often 10000 or 8080, but we'll set 8080)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "InvoiceExpenseSystem.dll"]

