# 🧠 FaceAuth API — Sistema de Reconhecimento Facial

Backend completo para controle de acesso com reconhecimento facial usando **C#**, **ASP.NET Core**, **OpenCvSharp**, **DlibDotNet**, **PostgreSQL** e **Entity Framework Core**.

---

## 📋 Pré-requisitos

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)
- [Git](https://git-scm.com/)

---

## 🚀 Setup Rápido

### 1. Subir o PostgreSQL com Docker

```bash
cd backend
docker-compose up -d
```

Isso irá iniciar o PostgreSQL na porta **5432** com:
- **Database:** faceauth
- **User:** postgres
- **Password:** postgres

### 2. Baixar os Modelos do Dlib

Crie a pasta `Models` dentro de `FaceAuth.API/` e baixe os seguintes arquivos:

```bash
cd FaceAuth.API
mkdir Models
```

#### Haar Cascade (OpenCV)

O arquivo `haarcascade_frontalface_default.xml` já vem incluído com o OpenCvSharp. Caso não esteja disponível, baixe de:

```
https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_frontalface_default.xml
```

Salve em: `FaceAuth.API/Models/haarcascade_frontalface_default.xml`

#### Shape Predictor 68 Landmarks (Dlib)

```bash
# Baixar (95MB comprimido)
curl -L -o shape_predictor_68_face_landmarks.dat.bz2 https://github.com/davisking/dlib-models/raw/master/shape_predictor_68_face_landmarks.dat.bz2

# Descompactar (use 7-Zip no Windows ou bzip2 no Linux)
# Windows (com 7-Zip):
7z x shape_predictor_68_face_landmarks.dat.bz2 -oModels/

# Linux/Mac:
bzip2 -d shape_predictor_68_face_landmarks.dat.bz2
mv shape_predictor_68_face_landmarks.dat Models/
```

Salve em: `FaceAuth.API/Models/shape_predictor_68_face_landmarks.dat`

#### Modelo de Reconhecimento Facial ResNet (Dlib)

```bash
# Baixar (22MB comprimido)
curl -L -o dlib_face_recognition_resnet_model_v1.dat.bz2 https://github.com/davisking/dlib-models/raw/master/dlib_face_recognition_resnet_model_v1.dat.bz2

# Descompactar
# Windows (com 7-Zip):
7z x dlib_face_recognition_resnet_model_v1.dat.bz2 -oModels/

# Linux/Mac:
bzip2 -d dlib_face_recognition_resnet_model_v1.dat.bz2
mv dlib_face_recognition_resnet_model_v1.dat Models/
```

Salve em: `FaceAuth.API/Models/dlib_face_recognition_resnet_model_v1.dat`

### 3. Aplicar Migrations e Rodar

```bash
cd FaceAuth.API

# Instalar a tool do EF Core (se ainda não tiver)
dotnet tool install --global dotnet-ef

# Criar a migration inicial
dotnet ef migrations add InitialCreate

# Rodar a aplicação (migrations são aplicadas automaticamente)
dotnet run
```

A API estará disponível em: `http://localhost:5062`

---

## 📡 Endpoints

### POST `/api/auth/register` — Cadastro de Usuário

**Request:**
```json
{
  "name": "João Silva",
  "imageBase64": "/9j/4AAQSkZJRg... (imagem facial em base64)"
}
```

**Response (200):**
```json
{
  "message": "Usuário cadastrado com sucesso!",
  "userId": 1,
  "name": "João Silva"
}
```

**Erros:**
- `400` — Nenhum rosto detectado ou mais de um rosto na imagem
- `500` — Erro interno

---

### POST `/api/auth/authenticate` — Autenticação Facial

**Request:**
```json
{
  "imageBase64": "/9j/4AAQSkZJRg... (imagem facial em base64)"
}
```

**Response (200) — Sucesso:**
```json
{
  "success": true,
  "confidence": 85.42,
  "userName": "João Silva",
  "message": "Bem-vindo, João Silva!"
}
```

**Response (401) — Falha:**
```json
{
  "success": false,
  "confidence": 32.15,
  "userName": null,
  "message": "Usuário não reconhecido."
}
```

**Erros:**
- `400` — Nenhum rosto ou múltiplos rostos
- `500` — Erro interno

---

## ⚙️ Configuração

As configurações ficam em `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=faceauth;Username=postgres;Password=postgres"
  },
  "FaceRecognition": {
    "Threshold": 0.6,
    "HaarCascadePath": "Models/haarcascade_frontalface_default.xml",
    "ShapePredictorPath": "Models/shape_predictor_68_face_landmarks.dat",
    "FaceRecognitionModelPath": "Models/dlib_face_recognition_resnet_model_v1.dat"
  }
}
```

- **Threshold**: Limiar de distância euclidiana (padrão: 0.6). Quanto menor, mais restritivo.
- **Confiança**: Calculada como `(1 - distância) * 100`

---

## 🗄️ Banco de Dados

### Tabela `Users`
| Coluna    | Tipo   | Descrição                              |
|-----------|--------|----------------------------------------|
| Id        | int    | PK auto-incremento                     |
| Name      | string | Nome do usuário                        |
| Embedding | string | Vetor 128D serializado em JSON         |

### Tabela `AccessLogs`
| Coluna     | Tipo     | Descrição                             |
|------------|----------|---------------------------------------|
| Id         | int      | PK auto-incremento                    |
| UserId     | int?     | FK para Users (null se não reconhecido) |
| Timestamp  | DateTime | Data/hora da tentativa                |
| Success    | bool     | Se autenticação foi bem-sucedida      |
| Confidence | double   | Nível de confiança (0-100%)           |

---

## 🏗️ Arquitetura

```
FaceAuth.API/
├── Controllers/          → Camada de apresentação (API endpoints)
├── Domain/Entities/      → Entidades do domínio (User, AccessLog)
├── Application/
│   ├── DTOs/             → Data Transfer Objects
│   └── Interfaces/       → Contratos dos serviços
└── Infrastructure/
    ├── Data/             → DbContext (Entity Framework)
    ├── Repositories/     → Acesso a dados
    └── Services/         → Implementação dos serviços (FaceService, UserService)
```

---

## 🐳 Docker

Para parar o PostgreSQL:
```bash
docker-compose down
```

Para parar e remover os dados:
```bash
docker-compose down -v
```

---

## ⚠️ Importante

- Todos os modelos de IA rodam **localmente** — nenhuma API externa é utilizada
- Os modelos do Dlib precisam ser baixados separadamente (~120MB total)
- Este projeto foi projetado para Windows, mas funciona em Linux/macOS com os pacotes nativos corretos
