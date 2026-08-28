# Use SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["TripHaeven.Api.csproj", "./"]
RUN dotnet restore "TripHaeven.Api.csproj"

# Copy everything else and build
COPY . .
RUN dotnet publish "TripHaeven.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use runtime image to run the app
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Set ASP.NET Core URL to listen on Port 8080 (standard for Render/Railway)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TripHaeven.Api.dll"]
