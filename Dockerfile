FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["EfcomReport.csproj", "."]
RUN dotnet restore "EfcomReport.csproj"
COPY . .
RUN dotnet publish "EfcomReport.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ConnectionStrings__DefaultConnection="Data Source=/data/efcom.db"
VOLUME ["/data"]
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EfcomReport.dll"]
