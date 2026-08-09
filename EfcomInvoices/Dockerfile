FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["EfcomInvoices/EfcomInvoices.csproj", "EfcomInvoices/"]
COPY ["EfcomCore/EfcomCore.csproj", "EfcomCore/"]
RUN dotnet restore "EfcomInvoices/EfcomInvoices.csproj"
COPY . .
RUN dotnet publish "EfcomInvoices/EfcomInvoices.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
RUN apt-get update \
    && apt-get install -y --no-install-recommends poppler-utils tesseract-ocr tesseract-ocr-eng tesseract-ocr-heb tesseract-ocr-rus \
    && rm -rf /var/lib/apt/lists/*
ENV ConnectionStrings__DefaultConnection="Data Source=/home/efcomreport.db"
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EfcomInvoices.dll"]
