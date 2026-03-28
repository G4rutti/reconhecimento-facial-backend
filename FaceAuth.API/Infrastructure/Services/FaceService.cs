using FaceAuth.API.Application.Interfaces;
using DlibDotNet;
using DlibDotNet.Dnn;
using OpenCvSharp;

namespace FaceAuth.API.Infrastructure.Services
{
    /// <summary>
    /// Serviço de reconhecimento facial usando OpenCvSharp (detecção) e DlibDotNet (embedding).
    /// Processa imagens base64, detecta rostos, e gera/compara embeddings faciais de 128 dimensões.
    /// </summary>
    public class FaceService : IFaceService, IDisposable
    {
        private readonly CascadeClassifier _faceCascade;
        private readonly ShapePredictor _shapePredictor;
        private readonly LossMetric _faceRecognitionModel;
        private readonly ILogger<FaceService> _logger;

        public FaceService(IConfiguration configuration, ILogger<FaceService> logger)
        {
            _logger = logger;

            // Carregar o Haar Cascade para detecção de rostos (OpenCV)
            var haarCascadePath = configuration["FaceRecognition:HaarCascadePath"]
                ?? Path.Combine(AppContext.BaseDirectory, "Models", "haarcascade_frontalface_default.xml");

            if (!File.Exists(haarCascadePath))
                throw new FileNotFoundException($"Haar Cascade não encontrado em: {haarCascadePath}");

            _faceCascade = new CascadeClassifier(haarCascadePath);

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

            // 2. Carregar imagem com OpenCV para detecção de rosto
            using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
            using var grayMat = new Mat();
            Cv2.CvtColor(mat, grayMat, ColorConversionCodes.BGR2GRAY);
            Cv2.EqualizeHist(grayMat, grayMat);

            // 3. Detectar rostos usando Haar Cascade
            var faces = _faceCascade.DetectMultiScale(
                grayMat,
                scaleFactor: 1.1,
                minNeighbors: 5,
                flags: HaarDetectionTypes.DoCannyPruning,
                minSize: new OpenCvSharp.Size(80, 80)
            );

            // 4. Validar número de rostos detectados
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

            // 5. Converter a imagem para formato Dlib (Array2D<RgbPixel>)
            using var dlibImage = ConvertMatToDlibImage(mat);

            // 6. Criar retângulo Dlib a partir da detecção do OpenCV
            var face = faces[0];
            var dlibRect = new DlibDotNet.Rectangle(
                face.X, face.Y,
                face.X + face.Width, face.Y + face.Height
            );

            // 7. Extrair landmarks faciais (68 pontos)
            var shape = _shapePredictor.Detect(dlibImage, dlibRect);

            // 8. Gerar embedding facial usando o modelo ResNet
            var faceChipDetail = Dlib.GetFaceChipDetails(shape, 150, 0.25);
            var faceChip = Dlib.ExtractImageChip<RgbPixel>(dlibImage, faceChipDetail);

            // 9. Converter Array2D<RgbPixel> para Matrix<RgbPixel> (requerido pelo LossMetric)
            using var matrixChip = new Matrix<RgbPixel>(faceChip);
            var faceChips = new[] { matrixChip };

            // 10. Obter o embedding (vetor de 128 floats)
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
            _faceCascade?.Dispose();
            _shapePredictor?.Dispose();
            _faceRecognitionModel?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
