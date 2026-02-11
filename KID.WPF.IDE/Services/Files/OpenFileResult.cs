namespace KID.Services.Files
{
    /// <summary>
    /// Результат открытия файла с кодом.
    /// </summary>
    public class OpenFileResult
    {
        public string Code { get; }
        public string FilePath { get; }

        public OpenFileResult(string code, string filePath)
        {
            Code = code;
            FilePath = filePath;
        }
    }
}
