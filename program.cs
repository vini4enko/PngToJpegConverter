using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ImageMagick;

namespace PngToJpegConverter
{
    class Program
    {
        // Счётчики для потокобезопасной статистики
        private static int _successCount;
        private static int _failedCount;
        private static int _processedCount;
        private static readonly object ConsoleLock = new();

        [STAThread]
        static void Main(string[] args)
        {
            Console.WriteLine("=== Пакетная конвертация PNG → JPEG (параллельная) ===");

            string? sourceFolder = args.Length > 0 ? args[0] : SelectFolderDialog();

            if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            {
                Console.WriteLine("Папка не выбрана или не существует. Выход.");
                return;
            }

            Console.Write("Включать подпапки? (y/N): ");
            bool recursive = Console.ReadLine()?.Trim().ToLower() == "y";

            Console.Write("Качество JPEG (1–100, по умолчанию 90): ");
            string? qualityInput = Console.ReadLine();
            int quality = int.TryParse(qualityInput, out var q) && q >= 1 && q <= 100 ? q : 90;

            // Определяем степень параллелизма: по умолчанию — число ядер,
            // но не более 8, чтобы не перегружать память большими изображениями.
            int maxDegree = Math.Min(Environment.ProcessorCount, 8);

            Console.Write($"Степень параллелизма (по умолчанию {maxDegree}): ");
            string? degreeInput = Console.ReadLine();
            if (int.TryParse(degreeInput, out var d) && d >= 1)
                maxDegree = d;

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] pngFiles = Directory.GetFiles(sourceFolder, "*.png", searchOption);

            if (pngFiles.Length == 0)
            {
                Console.WriteLine("PNG-файлы не найдены.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"Найдено файлов: {pngFiles.Length}");
            Console.WriteLine($"Качество JPEG: {quality}");
            Console.WriteLine($"Параллельных потоков: {maxDegree}");
            Console.WriteLine();

            var stopwatch = Stopwatch.StartNew();

            // Параллельная обработка
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegree
            };

            Parallel.ForEach(pngFiles, parallelOptions, pngPath =>
            {
                string jpgPath = Path.ChangeExtension(pngPath, ".jpg");

                try
                {
                    ConvertPngToJpeg(pngPath, jpgPath, quality);

                    Interlocked.Increment(ref _successCount);

                    lock (ConsoleLock)
                    {
                        Console.WriteLine($"[OK]   {Path.GetFileName(pngPath)} → {Path.GetFileName(jpgPath)}");
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _failedCount);

                    lock (ConsoleLock)
                    {
                        Console.WriteLine($"[FAIL] {Path.GetFileName(pngPath)}: {ex.Message}");
                    }
                }
                finally
                {
                    int processed = Interlocked.Increment(ref _processedCount);
                    lock (ConsoleLock)
                    {
                        // Прогресс в одной строке с возвратом каретки
                        Console.Write($"\rПрогресс: {processed}/{pngFiles.Length}");
                    }
                }
            });

            stopwatch.Stop();

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine($"Готово за {stopwatch.Elapsed.TotalSeconds:F2} сек.");
            Console.WriteLine($"Успешно: {_successCount}, с ошибками: {_failedCount}");
            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadKey();
        }

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

        private static void ConvertPngToJpeg(string inputPath, string outputPath, int quality)
        {
            using var image = new MagickImage(inputPath);

            image.ColorAlpha(MagickColors.White);
            image.Format = MagickFormat.Jpeg;
            image.Quality = (uint)quality;

            image.Write(outputPath);
        }
    }
}