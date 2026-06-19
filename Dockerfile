FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/InCleanHome.PaymentService/InCleanHome.PaymentService.csproj src/InCleanHome.PaymentService/
RUN dotnet restore "src/InCleanHome.PaymentService/InCleanHome.PaymentService.csproj"
COPY . .
RUN dotnet publish "src/InCleanHome.PaymentService/InCleanHome.PaymentService.csproj" \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends wget && rm -rf /var/lib/apt/lists/*
RUN useradd -m -u 10001 appuser
USER appuser
COPY --from=build --chown=appuser:appuser /app/publish .
EXPOSE 5004
ENV ASPNETCORE_URLS=http://+:5004
ENTRYPOINT ["dotnet", "InCleanHome.PaymentService.dll"]
