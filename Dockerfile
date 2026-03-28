# Usa a imagem oficial do SDK do .NET 9 como ambiente de build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /app

# Copia os arquivos de projeto primeiro para restaurar as dependências (caching)
COPY FaceAuth.API/*.csproj ./FaceAuth.API/
RUN dotnet restore ./FaceAuth.API/FaceAuth.API.csproj

# Copia todo o restante do código e compila a aplicação
COPY . ./
RUN dotnet publish ./FaceAuth.API/FaceAuth.API.csproj -c Release -o /app/out

# Usa a imagem oficial do ASP.NET Core Runtime 9 como ambiente de execução
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Instala dependências nativas necessárias para DlibDotNet e OpenCvSharp em Linux
RUN apt-get update && apt-get install -y \
    libgdiplus \
    libc6-dev \
    libx11-dev \
    libasound2 \
    libxext6 \
    libsm6 \
    libxrender1 \
    libgl1-mesa-glx \
    libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*

# Copia os arquivos compilados do ambiente de build
COPY --from=build-env /app/out .

# Configura as variáveis de ambiente base
ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Define o ponto de entrada da aplicação
ENTRYPOINT ["dotnet", "FaceAuth.API.dll"]
