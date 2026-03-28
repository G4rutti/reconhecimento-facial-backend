# ============================================================
# STAGE 1: Build (SDK oficial do .NET 9)
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /app

COPY FaceAuth.API/*.csproj ./FaceAuth.API/
RUN dotnet restore ./FaceAuth.API/FaceAuth.API.csproj

COPY . ./
RUN dotnet publish ./FaceAuth.API/FaceAuth.API.csproj -c Release -o /app/out

# Copia explicitamente o .so nativo do NuGet cache para o output
RUN SO_FILE=$(find /root/.nuget -name "libOpenCvSharpExtern.so" -path "*/linux*" 2>/dev/null | head -1) && \
    if [ -n "$SO_FILE" ]; then \
      echo ">>> Encontrado: $SO_FILE" && \
      cp "$SO_FILE" /app/out/ && \
      mkdir -p /app/out/runtimes/linux-x64/native && \
      cp "$SO_FILE" /app/out/runtimes/linux-x64/native/ ; \
    else \
      echo ">>> AVISO: .so nao encontrado no NuGet cache" ; \
    fi

# ============================================================
# STAGE 2: Runtime — Ubuntu 22.04 (tem libjpeg.so.8,
# libtiff.so.5, libIlmImf-2_5.so.25, libtesseract.so.4)
# ============================================================
FROM ubuntu:22.04 AS runtime

ENV DEBIAN_FRONTEND=noninteractive

# Instala o ASP.NET 9 runtime do repositório oficial da Microsoft
RUN apt-get update && \
    apt-get install -y --no-install-recommends wget ca-certificates && \
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb && \
    dpkg -i /tmp/packages-microsoft-prod.deb && \
    rm /tmp/packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y --no-install-recommends aspnetcore-runtime-9.0 && \
    rm -rf /var/lib/apt/lists/*

# Instala dependências nativas do OpenCvSharp e DlibDotNet
# Ubuntu 22.04 tem TODAS as versões corretas:
#   libjpeg.so.8, libtiff.so.5, libIlmImf-2_5.so.25
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgdiplus \
    libc6-dev \
    libx11-6 \
    libasound2 \
    libxext6 \
    libsm6 \
    libxrender1 \
    libgl1-mesa-glx \
    libglib2.0-0 \
    libjpeg8 \
    libpng16-16 \
    libtiff5 \
    libwebp7 \
    libopenjp2-7 \
    libilmbase25 \
    libopenexr25 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build-env /app/out .

ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:8080
ENV LD_LIBRARY_PATH=/app:/app/runtimes/linux-x64/native:$LD_LIBRARY_PATH
EXPOSE 8080

ENTRYPOINT ["dotnet", "FaceAuth.API.dll"]
