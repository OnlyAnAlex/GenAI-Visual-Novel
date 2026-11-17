using System;
using System.IO;

namespace VnNpc
{
    /// <summary>
    /// Lê a chave de fora do projeto: variável de ambiente ou arquivo na pasta do usuário.
    /// Nunca grave a chave em assets, StreamingAssets ou Resources: tudo isso vai para o repositório e para a build.
    /// </summary>
    public static class LocalApiKeyStore
    {
        private const string KeyFolderName = ".vn-npc";
        private const string KeyFileName = "openrouter.key";

        public static string KeyFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), KeyFolderName, KeyFileName);

        /// <summary>A variável de ambiente tem prioridade. O erro nunca contém a chave.</summary>
        public static bool TryGetKey(string environmentVariable, out string key, out string error)
        {
            error = null;
            key = Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrWhiteSpace(key))
            {
                key = key.Trim();
                return true;
            }

            key = null;
            string path = KeyFilePath;
            if (!File.Exists(path))
            {
                error = $"Chave não encontrada. Defina a variável {environmentVariable} e reinicie o Unity Hub, ou crie {path}.";
                return false;
            }

            try
            {
                key = File.ReadAllText(path).Trim();
            }
            catch (IOException exception)
            {
                error = $"Não foi possível ler {path}: {exception.Message}";
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                error = $"Sem permissão para ler {path}: {exception.Message}";
                return false;
            }

            if (key.Length > 0) return true;

            key = null;
            error = $"O arquivo {path} está vazio.";
            return false;
        }
    }
}
