# Production image for Btw.TemplatePdf API (linux/amd64).
# Build: docker build --platform linux/amd64 -t ingluigii/btw-template-pdf:latest .

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY Btw.TemplatePdf.slnx ./
COPY src/Btw.TemplatePdf.Domain/Btw.TemplatePdf.Domain.csproj src/Btw.TemplatePdf.Domain/
COPY src/Btw.TemplatePdf.Application/Btw.TemplatePdf.Application.csproj src/Btw.TemplatePdf.Application/
COPY src/Btw.TemplatePdf.Infrastructure/Btw.TemplatePdf.Infrastructure.csproj src/Btw.TemplatePdf.Infrastructure/
COPY src/Btw.TemplatePdf.Api/Btw.TemplatePdf.Api.csproj src/Btw.TemplatePdf.Api/

RUN dotnet restore src/Btw.TemplatePdf.Api/Btw.TemplatePdf.Api.csproj -a $TARGETARCH

COPY src/ src/
RUN dotnet publish src/Btw.TemplatePdf.Api/Btw.TemplatePdf.Api.csproj \
    -c Release \
    -a $TARGETARCH \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    PLAYWRIGHT_BROWSERS_PATH=/ms-playwright \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .

# Playwright Chromium + OS deps for this base image
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && curl -fsSL https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -o /tmp/ms.deb \
    && dpkg -i /tmp/ms.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends powershell \
    && pwsh ./playwright.ps1 install chromium --with-deps \
    && apt-get purge -y powershell curl \
    && apt-get autoremove -y \
    && rm -rf /var/lib/apt/lists/* /tmp/ms.deb

EXPOSE 8080
ENTRYPOINT ["dotnet", "Btw.TemplatePdf.Api.dll"]
