# Строим
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["FoodSelection.csproj", "."]
RUN dotnet restore "FoodSelection.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "FoodSelection.csproj" -c Release -o /app/build

# Публикуем
FROM build AS publish
RUN dotnet publish "FoodSelection.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Финальный образ
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FoodSelection.dll"]