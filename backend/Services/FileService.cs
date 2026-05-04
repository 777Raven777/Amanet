namespace backend.Services
{
    public interface IFileService
    {
        Task<string> SaveFileAsync(IFormFile image, string[] allowedExtensions);
        void DeleteFile(string fileNameWithExtension);
    }

    public class FileService(IWebHostEnvironment environment) : IFileService
    {

        public async Task<string> SaveFileAsync(IFormFile image, string[] allowedExtensions)
        {
            if (image== null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            var currentPath = environment.ContentRootPath;
            var path = Path.Combine(currentPath, "Uploads");

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            var ext = Path.GetExtension(image.FileName);
            if (!allowedExtensions.Contains(ext))
            {
                throw new ArgumentException($"Only {string.Join(",", allowedExtensions)} are allowed.");
            }

            var fileName = $"{Guid.NewGuid().ToString()}{ext}";
            var fileNameWithPath = Path.Combine(path, fileName);
            using var stream = new FileStream(fileNameWithPath, FileMode.Create);
            await image.CopyToAsync(stream);
            return fileName;
        }


        public void DeleteFile(string fileNameWithExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithExtension))
            {
                throw new ArgumentNullException(nameof(fileNameWithExtension));
            }
            var contentPath = environment.ContentRootPath;
            var path = Path.Combine(contentPath, $"Uploads", fileNameWithExtension);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Invalid file path");
            }
            File.Delete(path);
        }
    }
}
