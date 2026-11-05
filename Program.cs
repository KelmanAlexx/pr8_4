using System.Globalization;
using SatBackupToolkit.Models;
using SatBackupToolkit.Services;

namespace SatBackupToolkit;

internal static class Program
{
    private static readonly BackupService Backup = new();
    private static readonly SatProtocolService Protocols = new();

    private static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length > 0)
        {
            RunCommand(args);
            return;
        }

        while (true)
        {
            Console.Clear();
            Console.WriteLine("============================================");
            Console.WriteLine("       SAT BACKUP TOOLKIT");
            Console.WriteLine("============================================");
            Console.WriteLine("1. Создать резервную копию");
            Console.WriteLine("2. Проверить резервную копию (SHA-256)");
            Console.WriteLine("3. Восстановить резервную копию");
            Console.WriteLine("4. Создать SAT-протокол Markdown");
            Console.WriteLine("0. Выход");
            Console.Write("Выберите действие: ");

            string choice = Console.ReadLine() ?? string.Empty;
            Console.WriteLine();

            try
            {
                switch (choice)
                {
                    case "1": CreateBackupInteractive(); break;
                    case "2": VerifyBackupInteractive(); break;
                    case "3": RestoreBackupInteractive(); break;
                    case "4": CreateProtocolInteractive(); break;
                    case "0": return;
                    default: Console.WriteLine("Неизвестная команда."); break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка: " + ex.Message);
            }

            Console.WriteLine();
            Console.WriteLine("Нажмите Enter, чтобы вернуться в меню...");
            Console.ReadLine();
        }
    }

    private static void RunCommand(string[] args)
    {
        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "backup" when args.Length >= 3:
                    Console.WriteLine(Backup.CreateBackup(args[1], args[2]));
                    break;
                case "verify" when args.Length >= 2:
                    PrintVerification(Backup.VerifyBackup(args[1]));
                    break;
                case "restore" when args.Length >= 3:
                    Backup.RestoreBackup(args[1], args[2]);
                    Console.WriteLine("Восстановление завершено.");
                    break;
                case "sat":
                    CreateProtocolInteractive();
                    break;
                default:
                    Console.WriteLine("Команды:");
                    Console.WriteLine("  backup <source> <destination>");
                    Console.WriteLine("  verify <archive.zip>");
                    Console.WriteLine("  restore <archive.zip> <empty-target>");
                    Console.WriteLine("  sat");
                    Environment.ExitCode = 1;
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка: " + ex.Message);
            Environment.ExitCode = 1;
        }
    }

    private static void CreateBackupInteractive()
    {
        string source = ReadWithDefault("Папка проекта", "ExampleData");
        string destination = ReadWithDefault("Папка для архивов", "Backups");
        string archivePath = Backup.CreateBackup(source, destination);
        Console.WriteLine("Резервная копия создана:");
        Console.WriteLine(archivePath);
        Console.WriteLine("Файлов добавлено: " + Backup.VerifyBackup(archivePath).FilesChecked);
    }

    private static void VerifyBackupInteractive()
    {
        string path = ReadRequired("Путь к ZIP-архиву");
        PrintVerification(Backup.VerifyBackup(path));
    }

    private static void RestoreBackupInteractive()
    {
        string archive = ReadRequired("Путь к ZIP-архиву");
        string destination = ReadWithDefault("Папка восстановления (должна быть пустой)", "RestoredProject");
        Backup.RestoreBackup(archive, destination);
        Console.WriteLine("Файлы восстановлены в: " + Path.GetFullPath(destination));
    }

    private static void PrintVerification(BackupVerificationResult result)
    {
        foreach (string message in result.Messages)
            Console.WriteLine(message);
        Console.WriteLine($"Проверено файлов: {result.FilesChecked}");
        Console.WriteLine(result.IsValid ? "Итог: архив прошёл проверку." : "Итог: архив не прошёл проверку.");
    }

    private static void CreateProtocolInteractive()
    {
        var protocol = new SatProtocol
        {
            ProjectName = ReadRequired("Название проекта"),
            SystemName = ReadRequired("Название системы"),
            Version = ReadWithDefault("Версия системы", "1.0.0"),
            Environment = ReadWithDefault("Окружение", "Тестовый стенд"),
            Site = ReadWithDefault("Площадка / адрес", "Не указано"),
            Tester = ReadRequired("Исполнитель испытаний"),
            CustomerRepresentative = ReadWithDefault("Представитель заказчика", "Не указан"),
            TestDate = ReadDate()
        };

        Console.WriteLine();
        Console.WriteLine("Добавление тест-кейсов. Для каждого теста заполняются шаги и результаты.");
        int number = 1;
        bool addMore = true;
        while (addMore)
        {
            Console.WriteLine();
            Console.WriteLine($"--- Тест SAT-{number:000} ---");
            var test = new SatTestCase
            {
                Id = $"SAT-{number:000}",
                Title = ReadRequired("Название теста"),
                Preconditions = ReadWithDefault("Предварительные условия", "Система запущена и доступна."),
                ExpectedResult = ReadRequired("Ожидаемый результат"),
                ActualResult = ReadRequired("Фактический результат"),
                Result = ReadResult(),
                Comments = ReadWithDefault("Комментарий", "")
            };

            Console.WriteLine("Введите шаги через точку с запятой (;).");
            string steps = ReadRequired("Шаги");
            test.Steps = steps.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
            protocol.TestCases.Add(test);
            number++;
            addMore = ReadWithDefault("Добавить ещё один тест? (д/н)", "н").Trim().ToLowerInvariant() is "д" or "да" or "y" or "yes";
        }

        string outputDirectory = ReadWithDefault("Папка для Markdown-протоколов", "Protocols");
        string filePath = Protocols.SaveMarkdown(protocol, outputDirectory);
        Console.WriteLine();
        Console.WriteLine("SAT-протокол создан:");
        Console.WriteLine(Path.GetFullPath(filePath));
        Console.WriteLine("Тестов: " + protocol.TestCases.Count);
    }

    private static TestResult ReadResult()
    {
        Console.WriteLine("Статус теста:");
        Console.WriteLine("1. Пройдено");
        Console.WriteLine("2. Не пройдено");
        Console.WriteLine("3. Заблокировано");
        Console.WriteLine("4. Не запускалось");
        while (true)
        {
            Console.Write("Выберите статус: ");
            switch (Console.ReadLine())
            {
                case "1": return TestResult.Passed;
                case "2": return TestResult.Failed;
                case "3": return TestResult.Blocked;
                case "4": return TestResult.NotRun;
                default: Console.WriteLine("Введите число от 1 до 4."); break;
            }
        }
    }

    private static DateTime ReadDate()
    {
        while (true)
        {
            string value = ReadWithDefault("Дата испытаний (ДД.ММ.ГГГГ)", DateTime.Today.ToString("dd.MM.yyyy"));
            if (DateTime.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime date))
                return date;
            Console.WriteLine("Неверная дата. Пример: 05.11.2026");
        }
    }

    private static string ReadRequired(string prompt)
    {
        while (true)
        {
            Console.Write(prompt + ": ");
            string value = Console.ReadLine() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
            Console.WriteLine("Поле не должно быть пустым.");
        }
    }

    private static string ReadWithDefault(string prompt, string defaultValue)
    {
        Console.Write($"{prompt} [{defaultValue}]: ");
        string value = Console.ReadLine() ?? string.Empty;
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }
}
