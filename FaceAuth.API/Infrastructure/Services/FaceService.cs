using FaceAuth.API.Application.DTOs;
using FaceAuth.API.Application.Interfaces;
using DlibDotNet;
using DlibDotNet.Dnn;
using OpenCvSharp;

namespace FaceAuth.API.Infrastructure.Services
{
    /// <summary>
    /// Serviço de reconhecimento facial usando OpenCvSharp (detecção) e DlibDotNet (embedding).
    /// Processa imagens base64, detecta rostos, e gera/compara embeddings faciais de 128 dimensões.
    /// Inclui validação de qualidade de imagem e detecção de spoofing.
    /// </summary>
    public class FaceService : IFaceService, IDisposable
    {
        private readonly FrontalFaceDetector _dlibFaceDetector;
        private readonly ShapePredictor _shapePredictor;
        private readonly LossMetric _faceRecognitionModel;
        private readonly ILogger<FaceService> _logger;
        private readonly IConfiguration _configuration;

        public FaceService(IConfiguration configuration, ILogger<FaceService> logger)
        {
            _logger = logger;
            _configuration = configuration;

            // Inicializar o detector de rostos HOG do Dlib (muito mais preciso e robusto que Haar Cascade)
            _dlibFaceDetector = Dlib.GetFrontalFaceDetector();

            // Carregar o shape predictor do Dlib (68 landmarks)
            var shapePredictorPath = configuration["FaceRecognition:ShapePredictorPath"]
                ?? Path.Combine(AppContext.BaseDirectory, "Models", "shape_predictor_68_face_landmarks.dat");

            if (!File.Exists(shapePredictorPath))
                throw new FileNotFoundException($"Shape Predictor não encontrado em: {shapePredictorPath}");

            _shapePredictor = ShapePredictor.Deserialize(shapePredictorPath);

            // Carregar o modelo de reconhecimento facial do Dlib (ResNet)
            var faceRecognitionModelPath = configuration["FaceRecognition:FaceRecognitionModelPath"]
                ?? Path.Combine(AppContext.BaseDirectory, "Models", "dlib_face_recognition_resnet_model_v1.dat");

            if (!File.Exists(faceRecognitionModelPath))
                throw new FileNotFoundException($"Modelo de reconhecimento facial não encontrado em: {faceRecognitionModelPath}");

            _faceRecognitionModel = LossMetric.Deserialize(faceRecognitionModelPath);

            _logger.LogInformation("FaceService inicializado com sucesso. Modelos carregados.");
        }

        /// <inheritdoc />
        public float[] GetEmbedding(string base64Image)
        {
            _logger.LogInformation("Iniciando extração de embedding facial...");

            // 1. Converter base64 para bytes da imagem
            byte[] imageBytes = Convert.FromBase64String(base64Image);

            // 2. Carregar imagem com OpenCV para processamento de cor
            using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);

            // 3. Converter a imagem para formato Dlib (Array2D<RgbPixel>)
            using var dlibImage = ConvertMatToDlibImage(mat);

            // 4. Detectar rostos usando Dlib HOG Detector (muito mais robusto a rotações e ângulos)
            var faces = _dlibFaceDetector.Operator(dlibImage);

            // 5. Validar número de rostos detectados
            if (faces.Length == 0)
            {
                _logger.LogWarning("Nenhum rosto detectado na imagem.");
                throw new ArgumentException("Nenhum rosto detectado na imagem.");
            }

            if (faces.Length > 1)
            {
                _logger.LogWarning("Mais de um rosto detectado na imagem: {Count} rostos.", faces.Length);
                throw new ArgumentException($"Mais de um rosto detectado na imagem ({faces.Length} rostos). Envie uma imagem com apenas um rosto.");
            }

            _logger.LogInformation("Rosto detectado com sucesso. Extraindo landmarks e embedding...");

            var faceRect = faces[0];

            // 6. Extrair landmarks faciais (68 pontos)
            var shape = _shapePredictor.Detect(dlibImage, faceRect);

            // 7. Gerar embedding facial usando o modelo ResNet
            var faceChipDetail = Dlib.GetFaceChipDetails(shape, 150, 0.25);
            var faceChip = Dlib.ExtractImageChip<RgbPixel>(dlibImage, faceChipDetail);

            // 8. Converter Array2D<RgbPixel> para Matrix<RgbPixel> (requerido pelo LossMetric)
            using var matrixChip = new Matrix<RgbPixel>(faceChip);
            var faceChips = new[] { matrixChip };

            // 9. Obter o embedding (vetor de 128 floats)
            var descriptors = _faceRecognitionModel.Operator(faceChips);
            var embedding = descriptors[0].ToArray();

            // Limpar recursos do Dlib
            faceChip.Dispose();
            faceChipDetail.Dispose();
            shape.Dispose();
            foreach (var d in descriptors) d.Dispose();

            _logger.LogInformation("Embedding facial extraído com sucesso ({Dimensions} dimensões).", embedding.Length);

            return embedding;
        }

        /// <inheritdoc />
        public double CalculateDistance(float[] embeddingA, float[] embeddingB)
        {
            if (embeddingA.Length != embeddingB.Length)
                throw new ArgumentException("Os embeddings devem ter o mesmo tamanho.");

            // Cálculo manual da distância euclidiana: sqrt(sum((a[i] - b[i])^2))
            double sumSquaredDifferences = 0;
            for (int i = 0; i < embeddingA.Length; i++)
            {
                double diff = embeddingA[i] - embeddingB[i];
                sumSquaredDifferences += diff * diff;
            }

            return Math.Sqrt(sumSquaredDifferences);
        }

        /// <inheritdoc />
        public (bool success, double confidence) Compare(float[] embeddingA, float[] embeddingB, double threshold)
        {
            double distance = CalculateDistance(embeddingA, embeddingB);

            // Confiança: (1 - distância) * 100, limitada entre 0 e 100
            double confidence = Math.Max(0, (1 - distance) * 100);
            bool success = distance < threshold;

            _logger.LogInformation(
                "Comparação facial: distância={Distance:F4}, threshold={Threshold}, confiança={Confidence:F2}%, match={Match}",
                distance, threshold, confidence, success);

            return (success, confidence);
        }

        /// <inheritdoc />
        public ImageQualityResult ValidateImageQuality(string base64Image)
        {
            _logger.LogInformation("Validando qualidade da imagem facial...");

            var result = new ImageQualityResult();

            try
            {
                byte[] imageBytes = Convert.FromBase64String(base64Image);
                using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
                using var grayMat = new Mat();
                Cv2.CvtColor(mat, grayMat, ColorConversionCodes.BGR2GRAY);

                // ====== 1. Verificar NITIDEZ (Blur) via Variância do Laplaciano ======
                using var laplacian = new Mat();
                Cv2.Laplacian(grayMat, laplacian, MatType.CV_64F);
                Cv2.MeanStdDev(laplacian, out var mean, out var stddev);
                double blurScore = stddev.Val0 * stddev.Val0; // Variância
                result.BlurScore = Math.Round(blurScore, 2);

                double blurThreshold = _configuration.GetValue<double>("FaceRecognition:BlurThreshold", 50.0);
                if (blurScore < blurThreshold)
                {
                    // Desativado a pedido do usuário: result.Warnings.Add("Imagem muito borrada. Mantenha o dispositivo estável.");
                }

                // ====== 2. Verificar BRILHO via histograma ======
                double brightness = Cv2.Mean(grayMat).Val0;
                result.BrightnessScore = Math.Round(brightness, 2);

                double minBrightness = _configuration.GetValue<double>("FaceRecognition:MinBrightness", 60.0);
                double maxBrightness = _configuration.GetValue<double>("FaceRecognition:MaxBrightness", 220.0);
                if (brightness < minBrightness)
                {
                    // Desativado a pedido do usuário: result.Warnings.Add("Imagem muito escura. Melhore a iluminação.");
                }
                else if (brightness > maxBrightness)
                {
                    // Desativado a pedido do usuário: result.Warnings.Add("Imagem muito clara. Reduza a iluminação direta.");
                }

                // ====== 3. Verificar TAMANHO DO ROSTO ======
                using var dlibImage = ConvertMatToDlibImage(mat);
                var faces = _dlibFaceDetector.Operator(dlibImage);

                if (faces.Length == 0)
                {
                    result.FaceSizePercent = 0;
                    result.Warnings.Add("Nenhum rosto detectado. Centralize seu rosto na câmera.");
                }
                else if (faces.Length > 1)
                {
                    result.Warnings.Add("Múltiplos rostos detectados. Certifique-se de estar sozinho.");
                }
                else
                {
                    var face = faces[0];
                    double faceArea = face.Width * face.Height;
                    double imageArea = mat.Width * mat.Height;
                    double faceSizePercent = (faceArea / imageArea) * 100;
                    result.FaceSizePercent = Math.Round(faceSizePercent, 2);

                    double minFacePercent = _configuration.GetValue<double>("FaceRecognition:MinFaceSizePercent", 8.0);
                    if (faceSizePercent < minFacePercent)
                    {
                        // Desativado a pedido do usuário: result.Warnings.Add("Rosto muito pequeno. Aproxime-se da câmera.");
                    }
                }

                result.IsAcceptable = result.Warnings.Count == 0;

                _logger.LogInformation(
                    "Qualidade da imagem: blur={BlurScore}, brilho={Brightness}, rosto={FaceSize}%, aceitável={IsOk}",
                    result.BlurScore, result.BrightnessScore, result.FaceSizePercent, result.IsAcceptable);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao validar qualidade da imagem.");
                result.IsAcceptable = false;
                result.Warnings.Add("Erro ao processar a imagem. Tente novamente.");
            }

            return result;
        }

        /// <inheritdoc />
        public double DetectSpoofing(string base64Image)
        {
            _logger.LogInformation("Executando detecção de spoofing (anti-fraude)...");

            try
            {
                byte[] imageBytes = Convert.FromBase64String(base64Image);
                using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);

                // Detectar rosto para análise localizada usando Dlib HOG Detector
                using var dlibImage = ConvertMatToDlibImage(mat);
                var faces = _dlibFaceDetector.Operator(dlibImage);

                if (faces.Length == 0)
                {
                    return 0; // Sem rosto, sem liveness
                }

                var faceRect = faces[0];

                // Limitar retângulo dentro da imagem OpenCV
                int left = Math.Max(0, faceRect.Left);
                int top = Math.Max(0, faceRect.Top);
                int width = Math.Min(mat.Width - left, (int)faceRect.Width);
                int height = Math.Min(mat.Height - top, (int)faceRect.Height);

                var openCvRect = new OpenCvSharp.Rect(left, top, width, height);

                // Recortar região do rosto para análise
                using var faceRoi = new Mat(mat, openCvRect);
                using var faceGray = new Mat();
                Cv2.CvtColor(faceRoi, faceGray, ColorConversionCodes.BGR2GRAY);

                double score = 0;

                // ====== 1. Análise de Textura (LBP - Local Binary Patterns) ======
                // Rostos reais têm textura de pele com variação natural
                // Fotos de tela/impressas têm padrão uniforme ou de pixels
                double textureScore = CalculateTextureScore(faceGray);
                score += textureScore * 0.4; // Peso de 40%

                // ====== 2. Variância de gradientes (Detecção de moiré/pixels) ======
                // Telas de dispositivos geram padrões de moiré detectáveis
                double gradientScore = CalculateGradientVariance(faceGray);
                score += gradientScore * 0.3; // Peso de 30%

                // ====== 3. Análise de distribuição de cor (Color variance) ======
                // Rostos reais têm distribuição de cor natural, fotos impressas são mais uniformes
                double colorScore = CalculateColorDistribution(faceRoi);
                score += colorScore * 0.3; // Peso de 30%

                // Normalizar entre 0-100
                double livenessScore = Math.Max(0, Math.Min(100, score * 100));

                _logger.LogInformation(
                    "Spoofing detection: textura={Texture:F2}, gradiente={Gradient:F2}, cor={Color:F2}, liveness={Liveness:F2}%",
                    textureScore * 100, gradientScore * 100, colorScore * 100, livenessScore);

                return Math.Round(livenessScore, 2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro na detecção de spoofing.");
                return 50; // Score neutro em caso de erro
            }
        }

        /// <summary>
        /// Calcula o score de textura usando variância do LBP simplificado.
        /// Rostos reais têm alta variação de textura; fotos/telas são mais uniformes.
        /// </summary>
        private static double CalculateTextureScore(Mat grayFace)
        {
            // Calcular gradientes em X e Y usando Sobel
            using var sobelX = new Mat();
            using var sobelY = new Mat();
            Cv2.Sobel(grayFace, sobelX, MatType.CV_64F, 1, 0, ksize: 3);
            Cv2.Sobel(grayFace, sobelY, MatType.CV_64F, 0, 1, ksize: 3);

            // Magnitude do gradiente
            using var magnitude = new Mat();
            Cv2.Magnitude(sobelX, sobelY, magnitude);

            // Variância da magnitude — rostos reais têm valores moderados
            Cv2.MeanStdDev(magnitude, out var mean, out var stddev);
            double variance = stddev.Val0 * stddev.Val0;

            // Rostos reais típicos: variância entre 200-2000
            // Fotos de tela: variância muito alta (bordas de pixels) ou muito baixa (blur)
            if (variance < 50) return 0.2; // Muito suave — suspeito
            if (variance > 3000) return 0.4; // Muito afiado — suspeito (moiré)
            
            // Normalizar para score entre 0.6 e 1.0
            double normalized = Math.Min(1.0, (variance - 50) / 1500.0);
            return 0.6 + normalized * 0.4;
        }

        /// <summary>
        /// Calcula a variância dos gradientes de alta frequência.
        /// Telas geram padrões periódicos detectáveis.
        /// </summary>
        private static double CalculateGradientVariance(Mat grayFace)
        {
            // Laplaciano para detectar mudanças bruscas
            using var laplacian = new Mat();
            Cv2.Laplacian(grayFace, laplacian, MatType.CV_64F);
            
            Cv2.MeanStdDev(laplacian, out var mean, out var stddev);
            double variance = stddev.Val0 * stddev.Val0;

            // Rostos reais: variância moderada (50-500)
            // Fotos/telas: muito alta ou muito baixa
            if (variance < 10) return 0.2;
            if (variance > 800) return 0.5;
            
            double normalized = Math.Min(1.0, (variance - 10) / 300.0);
            return 0.5 + normalized * 0.5;
        }

        /// <summary>
        /// Analisa a distribuição de cor da região do rosto.
        /// Rostos reais têm transições suaves; fotos impressas/telas têm distribuição diferente.
        /// </summary>
        private static double CalculateColorDistribution(Mat colorFace)
        {
            // Converter para HSV para analisar saturação e matiz
            using var hsv = new Mat();
            Cv2.CvtColor(colorFace, hsv, ColorConversionCodes.BGR2HSV);

            // Extrair canal de saturação
            var channels = Cv2.Split(hsv);
            try
            {
                using var saturation = channels[1];
                Cv2.MeanStdDev(saturation, out var mean, out var stddev);
                double satVariance = stddev.Val0;

                // Rostos reais têm variação natural de saturação de pele
                // Fotos/telas podem ter saturação uniforme ou anormal
                if (satVariance < 5) return 0.3; // Muito uniforme — suspeito
                if (satVariance > 80) return 0.5; // Saturação extrema

                double normalized = Math.Min(1.0, (satVariance - 5) / 50.0);
                return 0.5 + normalized * 0.5;
            }
            finally
            {
                foreach (var ch in channels) ch.Dispose();
            }
        }

        /// <summary>
        /// Converte uma imagem OpenCV (Mat) para o formato Dlib (Array2D de RgbPixel).
        /// </summary>
        private static Array2D<RgbPixel> ConvertMatToDlibImage(Mat mat)
        {
            var rows = mat.Rows;
            var cols = mat.Cols;
            var dlibImage = new Array2D<RgbPixel>(rows, cols);

            // OpenCV usa BGR, Dlib usa RGB — converter pixel a pixel
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    var pixel = mat.At<Vec3b>(y, x);
                    dlibImage[y][x] = new RgbPixel(pixel.Item2, pixel.Item1, pixel.Item0); // BGR -> RGB
                }
            }

            return dlibImage;
        }

        /// <summary>
        /// Libera os recursos nativos dos modelos Dlib e OpenCV.
        /// </summary>
        public void Dispose()
        {
            _dlibFaceDetector?.Dispose();
            _shapePredictor?.Dispose();
            _faceRecognitionModel?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
