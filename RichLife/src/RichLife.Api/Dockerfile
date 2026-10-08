FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/RichLife.Api/RichLife.Api.csproj",             "src/RichLife.Api/"]
COPY ["src/RichLife.Application/RichLife.Application.csproj",   "src/RichLife.Application/"]
COPY ["src/RichLife.Domain/RichLife.Domain.csproj",       "src/RichLife.Domain/"]
COPY ["src/RichLife.Infrastructure/RichLife.Infrastructure.csproj","src/RichLife.Infrastructure/"]
COPY ["src/RichLife.ServiceDefaults/RichLife.ServiceDefaults.csproj","src/RichLife.ServiceDefaults/"]

RUN dotnet restore "src/RichLife.Api/RichLife.Api.csproj"

COPY . .
WORKDIR "/src/src/RichLife.Api"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RichLife.Api.dll"]
