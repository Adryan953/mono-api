# Usa a imagem oficial do SDK do .NET para build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia o csproj e restaura as dependências
COPY ["Mono.Api.csproj", "./"]
RUN dotnet restore "Mono.Api.csproj"

# Copia o restante do código e compila a aplicação
COPY . .
RUN dotnet build "Mono.Api.csproj" -c Release -o /app/build

# Publica a aplicação
FROM build AS publish
RUN dotnet publish "Mono.Api.csproj" -c Release -o /app/publish

# Configura a imagem final de runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Define a porta 8080 para a aplicação
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Mono.Api.dll"]
