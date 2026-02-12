FROM mcr.microsoft.com/dotnet/sdk:8.0 
AS build
WORKDIR /src
COPY ["DUB/DUB.csproj", "DUB/"]
RUN dotnet restore "DUB/DUB.csproj"
COPY . .
WORKDIR "/src/DUB"
RUN dotnet publish "DUB.csproj" -c Release 
-o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DUB.dll"]