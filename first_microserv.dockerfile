FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ENV DOTNET_NUGET_FALLBACK_PACKAGES=""
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1

WORKDIR /src
COPY ["./FoodSelectionAPI/FoodSelection.csproj", "FoodSelectionAPI/"]
RUN dotnet restore "FoodSelectionAPI/FoodSelection.csproj"

COPY ["./FoodSelectionAPI/", "FoodSelectionAPI/"]
WORKDIR "/src/FoodSelectionAPI"
RUN dotnet build "./FoodSelection.csproj" -c release -o /first_microserv/build


FROM build
WORKDIR "/src/FoodSelectionAPI"
RUN dotnet build "./FoodSelection.csproj" -c release -o /first_microserv/build


FROM build AS publish
WORKDIR "/src/FoodSelectionAPI"
RUN dotnet publish "./FoodSelection.csproj" -c release -o /first_microserv/publish


FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /api-first
COPY --from=publish /first_microserv/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "FoodSelection.dll"]