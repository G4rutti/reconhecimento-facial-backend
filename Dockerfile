# Usa a imagem oficial do SDK do .NET 9 como ambiente de build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /app

# Copia os arquivos de projeto primeiro para restaurar as dependências (caching)
COPY FaceAuth.API/*.csproj ./FaceAuth.API/
RUN dotnet restore ./FaceAuth.API/FaceAuth.API.csproj -r linux-x64

# Copia todo o restante do código e compila a aplicação
COPY . ./
RUN dotnet publish ./FaceAuth.API/FaceAuth.API.csproj -c Release -r linux-x64 --self-contained false -o /app/out

# Verifica que os binários nativos foram copiados
RUN echo "=== Binarios nativos ===" && \
    find /app/out -name "*OpenCv*" -o -name "*opencv*" | head -20

# ============================================================
# Runtime
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Instala TODAS as dependências nativas que o OpenCvSharp (full) e DlibDotNet precisam
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
    tesseract-ocr \
    libtesseract-dev \
    libgtk-3-0 \
    && rm -rf /var/lib/apt/lists/* \
    && ln -sf /usr/lib/x86_64-linux-gnu/libjpeg.so.62 /usr/lib/x86_64-linux-gnu/libjpeg.so.8 \
    && ln -sf /usr/lib/x86_64-linux-gnu/libtiff.so.6 /usr/lib/x86_64-linux-gnu/libtiff.so.5 \
    && ldconfig

# Copia os arquivos compilados do ambiente de build
COPY --from=build-env /app/out .

# Configura as variáveis de ambiente base
ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:8080
ENV LD_LIBRARY_PATH=/app:$LD_LIBRARY_PATH
EXPOSE 8080

# Define o ponto de entrada da aplicação
ENTRYPOINT ["dotnet", "FaceAuth.API.dll"]
