FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY ChessFormularix.Console/ChessFormularix.Console.csproj ChessFormularix.Console/
COPY ChessFormulatrix.WebAPI/ChessFormulatrix.WebAPI.csproj ChessFormulatrix.WebAPI/
RUN dotnet restore ChessFormulatrix.WebAPI/ChessFormulatrix.WebAPI.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish ChessFormulatrix.WebAPI/ChessFormulatrix.WebAPI.csproj \
    -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "ChessFormulatrix.WebAPI.dll"]
