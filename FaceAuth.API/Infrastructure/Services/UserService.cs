using System.Collections.Concurrent;
using System.Text.Json;
using FaceAuth.API.Application.DTOs;
using FaceAuth.API.Application.Interfaces;
using FaceAuth.API.Domain.Entities;
using FaceAuth.API.Infrastructure.Repositories;

namespace FaceAuth.API.Infrastructure.Services
{
    /// <summary>
    /// Serviço de gerenciamento de usuários.
    /// Orquestra o cadastro e a autenticação facial, utilizando o FaceService
    /// para processamento de imagens e o UserRepository para persistência.
    /// Inclui suporte a multi-embedding, anti-spoofing e rate limiting.
    /// </summary>
    public class UserService : IUserService
    {
        private readonly IFaceService _faceService;
        private readonly UserRepository _userRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserService> _logger;

        // Rate limiting: controle de tentativas falhas (em memória)
        private static readonly ConcurrentDictionary<string, (int FailCount, DateTime BlockedUntil)> _rateLimitStore = new();
        private const int MaxFailedAttempts = 5;
        private const int BlockDurationSeconds = 30;

        public UserService(
            IFaceService faceService,
            UserRepository userRepository,
            IConfiguration configuration,
            ILogger<UserService> logger)
        {
            _faceService = faceService;
            _userRepository = userRepository;
            _configuration = configuration;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<User> RegisterAsync(string name, string base64Image)
        {
            // Compatibilidade: converte single image para lista
            return await RegisterAsync(name, new List<string> { base64Image });
        }

        /// <inheritdoc />
        public async Task<User> RegisterAsync(string name, List<string> base64Images)
        {
            _logger.LogInformation("Registrando novo usuário: {Name} com {Count} imagem(ns).", name, base64Images.Count);

            if (base64Images.Count == 0)
                throw new ArgumentException("Envie pelo menos uma imagem facial.");

            var allEmbeddings = new List<float[]>();

            foreach (var image in base64Images)
            {
                // Validar qualidade de cada imagem
                var quality = _faceService.ValidateImageQuality(image);
                if (!quality.IsAcceptable)
                {
                    string warnings = string.Join("; ", quality.Warnings);
                    _logger.LogWarning("Imagem rejeitada por qualidade: {Warnings}", warnings);
                    throw new ArgumentException($"Imagem de baixa qualidade: {warnings}");
                }

                // Extrair embedding facial
                float[] embedding = _faceService.GetEmbedding(image);
                allEmbeddings.Add(embedding);
            }

            // Serializar embeddings
            string embeddingsJson = JsonSerializer.Serialize(allEmbeddings);
            // Manter compatibilidade: o primeiro embedding no campo legacy
            string firstEmbeddingJson = JsonSerializer.Serialize(allEmbeddings[0]);

            var user = new User
            {
                Name = name,
                Embedding = firstEmbeddingJson,
                Embeddings = embeddingsJson,
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user);

            _logger.LogInformation(
                "Usuário '{Name}' registrado com sucesso (Id={Id}) com {Count} embedding(s).",
                user.Name, user.Id, allEmbeddings.Count);

            return user;
        }

        /// <inheritdoc />
        public async Task<AuthenticationResult> AuthenticateAsync(string base64Image)
        {
            _logger.LogInformation("Iniciando autenticação facial simples...");

            // ====== ANTI-SPOOFING (Apenas para exibir no painel, sem bloquear) ======
            double livenessScore = _faceService.DetectSpoofing(base64Image);

            // ====== EXTRAIR EMBEDDING ======
            float[] inputEmbedding = _faceService.GetEmbedding(base64Image);

            // Obter threshold configurável (padrão: 0.65)
            double threshold = _configuration.GetValue<double>("FaceRecognition:Threshold", 0.65);

            // Buscar todos os usuários cadastrados
            var users = await _userRepository.GetAllAsync();

            if (users.Count == 0)
            {
                _logger.LogWarning("Nenhum usuário cadastrado no sistema.");

                await _userRepository.AddAccessLogAsync(new AccessLog
                {
                    UserId = null,
                    Timestamp = DateTime.UtcNow,
                    Success = false,
                    Confidence = 0
                });

                return new AuthenticationResult
                {
                    Success = false,
                    Confidence = 0,
                    UserName = null,
                    LivenessScore = livenessScore,
                    RemainingAttempts = 5,
                    IsBlocked = false
                };
            }

            // ====== COMPARAR COM EMBDEDDINGS ======
            User? bestMatch = null;
            double bestConfidence = 0;
            double bestDistance = double.MaxValue;

            foreach (var user in users)
            {
                List<float[]>? storedEmbeddings = null;

                if (!string.IsNullOrEmpty(user.Embeddings))
                {
                    try
                    {
                        storedEmbeddings = JsonSerializer.Deserialize<List<float[]>>(user.Embeddings);
                    }
                    catch
                    {
                        // Fallback
                    }
                }

                if (storedEmbeddings == null || storedEmbeddings.Count == 0)
                {
                    float[]? singleEmbedding = JsonSerializer.Deserialize<float[]>(user.Embedding);
                    if (singleEmbedding != null)
                    {
                        storedEmbeddings = new List<float[]> { singleEmbedding };
                    }
                    else
                    {
                        continue;
                    }
                }

                foreach (var storedEmbedding in storedEmbeddings)
                {
                    var (isMatch, confidence) = _faceService.Compare(inputEmbedding, storedEmbedding, threshold);
                    double distance = _faceService.CalculateDistance(inputEmbedding, storedEmbedding);

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestConfidence = confidence;
                        if (isMatch)
                        {
                            bestMatch = user;
                        }
                    }
                }
            }

            bool success = bestMatch != null;

            // Registrar log de acesso
            await _userRepository.AddAccessLogAsync(new AccessLog
            {
                UserId = bestMatch?.Id,
                Timestamp = DateTime.UtcNow,
                Success = success,
                Confidence = bestConfidence
            });

            _logger.LogInformation(
                "Autenticação {Result}: usuário={User}, confiança={Confidence:F2}%, liveness={Liveness:F2}%",
                success ? "bem-sucedida" : "falhou",
                bestMatch?.Name ?? "N/A",
                bestConfidence,
                livenessScore);

            return new AuthenticationResult
            {
                Success = success,
                Confidence = Math.Round(bestConfidence, 2),
                UserName = bestMatch?.Name,
                LivenessScore = livenessScore,
                RemainingAttempts = 5,
                IsBlocked = false
            };
        }

        /// <inheritdoc />
        public async Task<(List<AccessLog> Logs, int TotalCount)> GetAccessLogsAsync(int page, int pageSize, bool? successFilter)
        {
            return await _userRepository.GetAccessLogsAsync(page, pageSize, successFilter);
        }

        // ====== Rate Limiting Helpers ======

        private void IncrementFailCount(string key)
        {
            _rateLimitStore.AddOrUpdate(
                key,
                (1, DateTime.MinValue),
                (_, existing) =>
                {
                    int newCount = existing.FailCount + 1;
                    if (newCount >= MaxFailedAttempts)
                    {
                        _logger.LogWarning("Rate limit atingido! Bloqueando por {Seconds}s.", BlockDurationSeconds);
                        return (0, DateTime.UtcNow.AddSeconds(BlockDurationSeconds));
                    }
                    return (newCount, existing.BlockedUntil);
                }
            );
        }

        private int GetRemainingAttempts(string key)
        {
            if (_rateLimitStore.TryGetValue(key, out var info))
            {
                if (DateTime.UtcNow < info.BlockedUntil)
                    return 0;
                return MaxFailedAttempts - info.FailCount;
            }
            return MaxFailedAttempts;
        }
    }
}
