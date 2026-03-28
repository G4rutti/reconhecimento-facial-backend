# ============================================================
# STAGE 1: Build
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /app

# Restaura dependências (sem -r para manter estrutura runtimes/)
COPY FaceAuth.API/*.csproj ./FaceAuth.API/
RUN dotnet restore ./FaceAuth.API/FaceAuth.API.csproj

# Compila e publica (sem -r para manter pasta runtimes/linux-x64/native/)
COPY . ./
RUN dotnet publish ./FaceAuth.API/FaceAuth.API.csproj -c Release -o /app/out

# Copia EXPLICITAMENTE o .so nativo do NuGet cache para o output
RUN echo "=== Procurando libOpenCvSharpExtern.so ===" && \
    find /root/.nuget /app -name "libOpenCvSharpExtern.so" 2>/dev/null && \
    SO_FILE=$(find /root/.nuget -name "libOpenCvSharpExtern.so" -path "*/linux-x64/*" 2>/dev/null | head -1) && \
    if [ -n "$SO_FILE" ]; then \
      echo "Encontrado: $SO_FILE" && \
      mkdir -p /app/out/runtimes/linux-x64/native && \
      cp "$SO_FILE" /app/out/runtimes/linux-x64/native/ && \
      cp "$SO_FILE" /app/out/ ; \
    else \
      echo "ERRO: libOpenCvSharpExtern.so nao encontrado no NuGet cache!" && \
      find /root/.nuget -name "*.so" 2>/dev/null | head -20 ; \
    fi

# Verifica resultado
RUN echo "=== Arquivos nativos no output ===" && \
    find /app/out -name "*.so" | head -20

# ============================================================
# STAGE 2: Runtime
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Instala dependências nativas do OpenCvSharp (slim) e DlibDotNet
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
    libjpeg62-turbo \
    libpng16-16 \
    libtiff6 \
    libwebp7 \
    libopenjp2-7 \
    && rm -rf /var/lib/apt/lists/* \
    && ln -sf /usr/lib/x86_64-linux-gnu/libjpeg.so.62 /usr/lib/x86_64-linux-gnu/libjpeg.so.8 \
    && ln -sf /usr/lib/x86_64-linux-gnu/libtiff.so.6 /usr/lib/x86_64-linux-gnu/libtiff.so.5 \
    && ldconfig

# Copia os arquivos compilados do build
COPY --from=build-env /app/out .

# Garante que o runtime encontre todas as bibliotecas nativas
ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:8080
ENV LD_LIBRARY_PATH=/app:/app/runtimes/linux-x64/native:$LD_LIBRARY_PATH
EXPOSE 8080

ENTRYPOINT ["dotnet", "FaceAuth.API.dll"]
