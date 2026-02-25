FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["my-gpx-activities.ApiService/my-gpx-activities.ApiService.csproj", "my-gpx-activities.ApiService/"]
COPY ["my-gpx-activities.ServiceDefaults/my-gpx-activities.ServiceDefaults.csproj", "my-gpx-activities.ServiceDefaults/"]
RUN dotnet restore "my-gpx-activities.ApiService/my-gpx-activities.ApiService.csproj"
COPY . .
WORKDIR "/src/my-gpx-activities.ApiService"
RUN dotnet publish "my-gpx-activities.ApiService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "my-gpx-activities.ApiService.dll"]
