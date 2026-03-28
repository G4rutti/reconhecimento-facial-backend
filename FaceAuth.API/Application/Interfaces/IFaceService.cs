namespace FaceAuth.API.Application.Interfaces
{
    /// <summary>
    /// Interface para o serviço de reconhecimento facial.
    /// Responsável por detecção de rosto, geração e comparação de embeddings.
    /// </summary>
    public interface IFaceService
    {
        /// <summary>
        /// Converte uma imagem base64, detecta o rosto e gera o embedding facial (128 floats).
        /// </summary>
        /// <param name="base64Image">Imagem codificada em base64.</param>
        /// <returns>Vetor de 128 dimensões representando a face.</returns>
        /// <exception cref="ArgumentException">Se nenhum rosto ou mais de um rosto for detectado.</exception>
        float[] GetEmbedding(string base64Image);

        /// <summary>
        /// Calcula a distância euclidiana entre dois embeddings faciais.
        /// </summary>
        /// <param name="embeddingA">Primeiro embedding.</param>
        /// <param name="embeddingB">Segundo embedding.</param>
        /// <returns>Distância euclidiana entre os dois vetores.</returns>
        double CalculateDistance(float[] embeddingA, float[] embeddingB);

        /// <summary>
        /// Compara dois embeddings e determina se são da mesma pessoa.
        /// </summary>
        /// <param name="embeddingA">Primeiro embedding.</param>
        /// <param name="embeddingB">Segundo embedding.</param>
        /// <param name="threshold">Limiar de distância para considerar como mesma pessoa.</param>
        /// <returns>Tupla com resultado (match) e confiança (0-100%).</returns>
        (bool success, double confidence) Compare(float[] embeddingA, float[] embeddingB, double threshold);
    }
}
