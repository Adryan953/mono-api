FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["Mono.Api.csproj", "./"]
RUN dotnet restore "Mono.Api.csproj"
COPY . .
RUN dotnet publish "Mono.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Mono.Api.dll"]