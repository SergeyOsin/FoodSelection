FROM mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /src

COPY . .

RUN dotnet restore "FoodSelectionAPITests/FoodSelectionAPITest.csproj"
RUN dotnet build "FoodSelectionAPITests/FoodSelectionAPITest.csproj" -c Release --no-restore

ENTRYPOINT ["dotnet", "test", "FoodSelectionAPITests/FoodSelectionAPITest.csproj", "-c", "Release", "--no-build"]