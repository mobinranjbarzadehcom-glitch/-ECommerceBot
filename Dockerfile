# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/ECommerceBot.API/ECommerceBot.API.csproj", "src/ECommerceBot.API/"]
RUN dotnet restore "src/ECommerceBot.API/ECommerceBot.API.csproj"

COPY . .
WORKDIR "/src/src/ECommerceBot.API"
RUN dotnet publish "ECommerceBot.API.csproj" -c Release -o /app/publish --no-restore

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

RUN mkdir -p /app/Logs && chmod 777 /app/Logs

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ECommerceBot.API.dll"]
