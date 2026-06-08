# 🧠 FaceAuth API — Sistema de Reconhecimento Facial & Anti-Spoofing

[![C#](https://img.shields.io/badge/C%23-178600?style=for-the-badge&logo=c-sharp&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![.NET 9](https://img.shields.io/badge/.NET%209-512BD4?style=for-the-badge&logo=.net&logoColor=white)](https://dotnet.microsoft.com/download)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Docker](https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white)](https://www.docker.com/)

O **FaceAuth API** é um backend robusto e de alta performance desenvolvido com **ASP.NET Core (C#)**, **Dlib**, **OpenCV**, **PostgreSQL** e **Entity Framework Core**. Ele implementa um pipeline avançado de biometria facial, detecção de vivacidade (anti-spoofing) e auditoria de acessos preparado para ambientes locais ou em nuvem.

---

## 💡 Diferenciais Tecnológicos & Algoritmos

Este backend foi atualizado com componentes modernos de visão computacional e inteligência artificial rodando de forma 100% local (sem dependência de APIs externas de terceiros):

*   **Detector HOG + Classificador Linear SVM (Dlib):** Substituição do antigo Haar Cascade por detecção robusta baseada em gradientes orientados e SVM, oferecendo alta tolerância a rotações leves de cabeça e variações de iluminação.
*   **Alinhamento Facial (68 Landmarks):** Detecção de 68 pontos de referência faciais para alinhar, centralizar e recortar a região do rosto (*face chip*) antes de alimentar a rede neural.
*   **Rede Neural ResNet (Dlib):** Mapeia o rosto para um vetor de características de **128 dimensões** (*embedding*) de altíssima precisão. A comparação no banco de dados usa distância euclidiana.
*   **Multi-Embedding por Usuário:** Suporte a múltiplos embeddings de cadastro por usuário (ex: foto frontal, perfil esquerdo, perfil direito) para garantir melhor acurácia na validação diária.
*   **Anti-Spoofing Avançado (Liveness):** Análise multivariada em tempo real para impedir fraudes com fotos impressas ou telas digitais de smartphones:
    1.  *Textura da pele* via Gradiente de Sobel (bloqueia texturas planas de fotos/papéis).
    2.  *Detecção de Moiré* via Filtro Laplaciano (detecta padrões de pixels/frequências de telas digitais).
    3.  *Variância de cores* no espaço HSV (identifica distorções artificiais de saturação).
*   **Validação de Qualidade de Imagem:** Verificação dinâmica de nitidez (*blur*), nível de brilho e tamanho relativo do rosto no frame, enviando feedback em tempo real para guiar o usuário na captura do frontend.
*   **Rate Limiting Dinâmico:** Bloqueio temporário de tentativas de login (em memória) após falhas sucessivas de reconhecimento (evita ataques de força bruta).

---

## 📋 Pré-requisitos

*   [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
*   [Docker & Docker Compose](https://docs.docker.com/get-docker/)
*   [Git](https://git-scm.com/)

---

## 🚀 Como Começar

### 1. Baixar os Modelos Pré-treinados do Dlib
Os pesos das redes do Dlib não são guardados no Git devido ao tamanho. Crie a pasta `Models/` dentro de `FaceAuth.API/` e baixe os arquivos necessários:

```bash
cd FaceAuth.API
mkdir Models
```

Baixe e extraia os seguintes arquivos para a pasta `Models/`:

*   **Shape Predictor 68 Landmarks** (Detecção de pontos):
    *   URL: [shape_predictor_68_face_landmarks.dat.bz2](https://github.com/davisking/dlib-models/raw/master/shape_predictor_68_face_landmarks.dat.bz2)
*   **Dlib Face Recognition ResNet Model v1** (Extração de embeddings):
    *   URL: [dlib_face_recognition_resnet_model_v1.dat.bz2](https://github.com/davisking/dlib-models/raw/master/dlib_face_recognition_resnet_model_v1.dat.bz2)

*(Nota: Descompacte os arquivos `.bz2` usando o 7-Zip no Windows ou `bzip2 -d` no Linux/macOS. Certifique-se de que os nomes finais dentro da pasta sejam exatamente `shape_predictor_68_face_landmarks.dat` e `dlib_face_recognition_resnet_model_v1.dat`).*

### 2. Execução Rápida via Docker Compose
A forma mais recomendada de rodar o ambiente completo é usando o compose configurado na raiz:

```bash
# Na raiz da pasta backend/
docker-compose up -d --build
```

Isso iniciará:
*   A **API** rodando na porta `8080` (redireciona as rotas para o Swagger em `/docs`).
*   O banco **PostgreSQL** na porta `5432` com volume persistente.

---

## 💻 Desenvolvimento Local (Sem Docker)

Se preferir rodar a API de forma nativa e interagir com o código:

### 1. Subir apenas o Banco de Dados
```bash
docker-compose up -d postgres
```

### 2. Configurar as Variáveis de Ambiente
Copie o arquivo `.env.example` para `.env` na raiz do backend e altere os valores se desejar customizar portas ou conexões.

### 3. Aplicar as Migrations e Iniciar
```bash
cd FaceAuth.API

# Instalar a ferramenta de migração se não possuir
dotnet tool install --global dotnet-ef

# Aplicar o banco de dados
dotnet ef database update

# Rodar a aplicação
dotnet run
```
A API local estará disponível em: `http://localhost:5062`

---

## 📡 Endpoints da API

A documentação interativa com testes de endpoints fica disponível em `http://localhost:8080/docs` (ou `http://localhost:5062/docs` se rodando localmente sem Docker).

### 1. POST `/api/auth/register` — Cadastro de Usuário
Cadastra um novo usuário gerando múltiplos embeddings a partir de imagens em base64 (recomendado enviar 3 fotos: frontal, perfil esquerdo, perfil direito).

*   **Request Body:**
```json
{
  "name": "João Silva",
  "imagesBase64": [
    "/9j/4AAQSkZJRg... (foto frontal)",
    "/9j/4AAQSkZJRg... (foto esquerda)",
    "/9j/4AAQSkZJRg... (foto direita)"
  ]
}
```

*   **Response (200 OK):**
```json
{
  "message": "Usuário cadastrado com sucesso!",
  "userId": 1,
  "name": "João Silva",
  "embeddingsCount": 3
}
```

---

### 2. POST `/api/auth/authenticate` — Autenticação Facial & Liveness
Compara a face enviada com as faces do banco de dados e calcula a vivacidade da imagem (liveness) para barrar fotos e telas digitais.

*   **Request Body:**
```json
{
  "imageBase64": "/9j/4AAQSkZJRg... (imagem capturada na câmera)"
}
```

*   **Response (200 OK — Autenticado):**
```json
{
  "success": true,
  "confidence": 88.42,
  "userName": "João Silva",
  "livenessScore": 91.5,
  "remainingAttempts": 5,
  "message": "Bem-vindo, João Silva!"
}
```

*   **Response (401 Unauthorized — Não Reconhecido ou Suspeita de Fraude):**
```json
{
  "success": false,
  "confidence": 32.15,
  "userName": null,
  "livenessScore": 21.0,
  "remainingAttempts": 4,
  "message": "Possível fraude detectada. Use seu rosto real."
}
```

*   **Response (429 Too Many Requests — Bloqueado temporariamente por Rate Limit):**
```json
{
  "success": false,
  "isBlocked": true,
  "blockedSecondsRemaining": 29,
  "remainingAttempts": 0,
  "message": "Muitas tentativas falhas. Tente novamente em 29s."
}
```

---

### 3. POST `/api/auth/validate-image` — Validação de Imagem
Faz uma pré-validação instantânea de foco, brilho e centralização para dar feedback interativo no frontend antes do envio oficial.

*   **Request Body:**
```json
{
  "imageBase64": "/9j/4AAQSkZJRg..."
}
```

*   **Response (200 OK):**
```json
{
  "isAcceptable": true,
  "blurScore": 124.50,
  "brightnessScore": 140.20,
  "faceSizePercent": 25.10,
  "warnings": []
}
```

---

### 4. GET `/api/auth/logs` — Auditoria de Acessos
Retorna a listagem de tentativas de acesso paginadas para auditorias e geração de relatórios de segurança.

*   **Parâmetros Query (Opcionais):** `page` (padrão 1), `pageSize` (padrão 20), `success` (true/false para filtrar).
*   **Response (200 OK):**
```json
{
  "logs": [
    {
      "id": 15,
      "userId": 1,
      "userName": "João Silva",
      "timestamp": "2026-06-08T20:20:00Z",
      "success": true,
      "confidence": 88.42
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

---

## ⚙️ Variáveis de Ambiente & Configurações

O backend aceita parametrização dinâmica de banco de dados e limiares de reconhecimento. As variáveis podem ser configuradas no arquivo `.env` local ou no painel do deploy Cloud (ex: Railway):

| Variável | Descrição | Padrão Local |
| :--- | :--- | :--- |
| `DATABASE_URL` / `CONNECTION_STRING` | URL completa do banco PostgreSQL no padrão Cloud | (Lido do `appsettings.json`) |
| `PGHOST` / `POSTGRES_HOST` | Host do banco de dados (alternativa à ConnectionString) | `localhost` |
| `PGPORT` / `POSTGRES_PORT` | Porta do banco de dados | `5432` |
| `PGDATABASE` / `POSTGRES_DB` | Nome da base de dados | `faceauth` |
| `PGUSER` / `POSTGRES_USER` | Usuário do banco de dados | `postgres` |
| `PGPASSWORD` / `POSTGRES_PASSWORD` | Senha do banco de dados | `postgres` |
| `PORT` | Porta HTTP dinâmica para o WebHost injetada em nuvem | `5062` / `8080` |

As chaves de ajuste fino do modelo permanecem em `appsettings.json`:
```json
"FaceRecognition": {
  "Threshold": 0.65,
  "BlurThreshold": 50.0,
  "MinBrightness": 60.0,
  "MaxBrightness": 220.0,
  "MinFaceSizePercent": 8.0
}
```
*   **Threshold:** Limiar de distância euclidiana. Quanto menor, mais rígido o reconhecimento (padrão recomendado: `0.65`).

---

## 🏗️ Estrutura do Projeto (Arquitetura)

O backend segue os princípios de clean code com a seguinte estrutura de diretórios:

```
FaceAuth.API/
├── Controllers/          → Camada de Apresentação (Endpoints da API HTTP)
├── Domain/
│   └── Entities/         → Entidades principais do domínio (User, AccessLog)
├── Application/
│   ├── DTOs/             → Modelos de Entrada/Saída da API
│   └── Interfaces/       → Definição de contratos (IFaceService, IUserService)
├── Infrastructure/
│   ├── Data/             → Contexto do Entity Framework (AppDbContext)
│   ├── Repositories/     → Acesso direto ao PostgreSQL
│   └── Services/         → Implementação dos serviços (FaceService, UserService)
├── Models/               → Pasta contendo os arquivos .dat do Dlib
└── Migrations/           → Arquivos de migração gerados pelo EF Core
```

---

## 🐳 Docker e Nuvem (Deploy Railway)

O deploy de aplicações que fazem uso de bibliotecas nativas C++ (como OpenCV e Dlib) em ambiente Linux (.NET Core) é historicamente complexo. O `Dockerfile` deste projeto foi altamente otimizado para resolver isso de forma transparente:

1.  **Build Stage:** Usa o container SDK do .NET 9.0 para restaurar e empacotar a aplicação, localizando e extraindo os arquivos nativos `.so` correspondentes à arquitetura Linux do cache NuGet do build (`libOpenCvSharpExtern.so`, etc.).
2.  **Runtime Stage:** Baseado em **Ubuntu 22.04**, garantindo as versões nativas corretas das bibliotecas `libgdiplus`, `libopenblas0`, `liblapack3` e dependências de codecs de imagem necessárias.
3.  **Configuração de Inicialização:** O `Program.cs` lê dinamicamente as variáveis do Railway e aplica as migrações automaticamente na base de dados durante o boot da aplicação.
