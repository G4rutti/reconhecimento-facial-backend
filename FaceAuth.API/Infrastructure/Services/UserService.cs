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
    /// </summary>
    public class UserService : IUserService
    {
        private readonly IFaceService _faceService;
        private readonly UserRepository _userRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserService> _logger;

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
            _logger.LogInformation("Registrando novo usuário: {Name}", name);

            // 1. Extrair embedding facial da imagem
            float[] embedding = _faceService.GetEmbedding(base64Image);

            // 2. Serializar embedding como JSON para persistência
            string embeddingJson = JsonSerializer.Serialize(embedding);

            // 3. Criar entidade User e salvar no banco
            var user = new User
            {
                Name = name,
                Embedding = embeddingJson
            };

            await _userRepository.AddAsync(user);

            _logger.LogInformation("Usuário '{Name}' registrado com sucesso (Id={Id}).", user.Name, user.Id);

            return user;
        }

        /// <inheritdoc />
        public async Task<AuthenticationResult> AuthenticateAsync(string base64Image)
        {
            _logger.LogInformation("Iniciando autenticação facial...");

            // 1. Extrair embedding da imagem recebida
            float[] inputEmbedding = _faceService.GetEmbedding(base64Image);

            // 2. Obter threshold configurável (padrão: 0.6)
            double threshold = _configuration.GetValue<double>("FaceRecognition:Threshold", 0.6);

            // 3. Buscar todos os usuários cadastrados
            var users = await _userRepository.GetAllAsync();

            if (users.Count == 0)
            {
                _logger.LogWarning("Nenhum usuário cadastrado no sistema.");

                // Registrar log de acesso (falha)
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
                    UserName = null
                };
            }

            // 4. Comparar com cada usuário e encontrar o mais próximo
            User? bestMatch = null;
            double bestConfidence = 0;
            double bestDistance = double.MaxValue;

            foreach (var user in users)
            {
                // Deserializar embedding do banco
                float[]? storedEmbedding = JsonSerializer.Deserialize<float[]>(user.Embedding);
                if (storedEmbedding == null) continue;

                // Comparar embeddings
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

            bool success = bestMatch != null;

            // 5. Registrar log de acesso
            await _userRepository.AddAccessLogAsync(new AccessLog
            {
                UserId = bestMatch?.Id,
                Timestamp = DateTime.UtcNow,
                Success = success,
                Confidence = bestConfidence
            });

            _logger.LogInformation(
                "Autenticação {Result}: usuário={User}, confiança={Confidence:F2}%",
                success ? "bem-sucedida" : "falhou",
                bestMatch?.Name ?? "N/A",
                bestConfidence);

            return new AuthenticationResult
            {
                Success = success,
                Confidence = Math.Round(bestConfidence, 2),
                UserName = bestMatch?.Name
            };
        }
    }
}
