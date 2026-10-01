using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ImageMagick;

namespace PngToJpegConverter
{
    class Program
    {
        [STAThread] // обязательно для работы FolderBrowserDialog
        static void Main(string[] args)
        {
            Console.WriteLine("=== Пакетная конвертация PNG → JPEG ===");

            string? sourceFolder = args.Length > 0 ? args[0] : SelectFolderDialog();

            if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            {
                Console.WriteLine("Папка не выбрана или не существует. Выход.");
                return;
            }

            // Спросим, искать ли в подпапках
            Console.Write("Включать подпапки? (y/N): ");
            bool recursive = Console.ReadLine()?.Trim().ToLower() == "y";

            Console.Write("Качество JPEG (1–100, по умолчанию 90): ");
            string? qualityInput = Console.ReadLine();
            int quality = int.TryParse(qualityInput, out var q) && q >= 1 && q <= 100 ? q : 90;

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] pngFiles = Directory.GetFiles(sourceFolder, "*.png", searchOption);

            if (pngFiles.Length == 0)
            {
                Console.WriteLine("PNG-файлы не найдены.");
                return;
            }

            Console.WriteLine($"Найдено файлов: {pngFiles.Length}");
            Console.WriteLine($"Качество JPEG: {quality}");
            Console.WriteLine();

            int success = 0;
            int failed = 0;

            foreach (string pngPath in pngFiles)
            {
                string jpgPath = Path.ChangeExtension(pngPath, ".jpg");

                try
                {
                    ConvertPngToJpeg(pngPath, jpgPath, quality);
                    success++;
                    Console.WriteLine($"[OK]   {Path.GetFileName(pngPath)} → {Path.GetFileName(jpgPath)}");
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine($"[FAIL] {Path.GetFileName(pngPath)}: {ex.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Готово. Успешно: {success}, с ошибками: {failed}");
            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

        /// <summary>
        /// Открывает диалог выбора папки.
        /// </summary>
        private static string? SelectFolderDialog()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Выберите папку с PNG-файлами",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false
            };

            return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
        }

        /// <summary>
        /// Конвертирует один PNG-файл в JPEG.
        /// </summary>
        private static void ConvertPngToJpeg(string inputPath, string outputPath, int quality)
        {
            using var image = new MagickImage(inputPath);

            // Заменяем прозрачность на белый фон (JPEG не поддерживает альфа-канал)
            image.ColorAlpha(MagickColors.White);

            // Явно задаём формат и качество
            image.Format = MagickFormat.Jpeg;
            image.Quality = (uint)quality;

            // Записываем результат
            image.Write(outputPath);
        }
    }
}