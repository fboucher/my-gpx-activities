FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["webapp/webapp.csproj", "webapp/"]
COPY ["my-gpx-activities.ServiceDefaults/my-gpx-activities.ServiceDefaults.csproj", "my-gpx-activities.ServiceDefaults/"]
RUN dotnet restore "webapp/webapp.csproj"
COPY . .
WORKDIR "/src/webapp"
RUN dotnet publish "webapp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "webapp.dll"]
