using System.Text;
using SatBackupToolkit.Models;

namespace SatBackupToolkit.Services;

public sealed class SatProtocolService
{
    public string GenerateMarkdown(SatProtocol protocol)
    {
        var text = new StringBuilder();
        int passed = protocol.TestCases.Count(test => test.Result == TestResult.Passed);
        int failed = protocol.TestCases.Count(test => test.Result == TestResult.Failed);
        int blocked = protocol.TestCases.Count(test => test.Result == TestResult.Blocked);
        int notRun = protocol.TestCases.Count(test => test.Result == TestResult.NotRun);
        int total = protocol.TestCases.Count;

        text.AppendLine("# Протокол SAT (Site Acceptance Test)");
        text.AppendLine();
        text.AppendLine("## 1. Общая информация");
        text.AppendLine();
        text.AppendLine("| Поле | Значение |");
        text.AppendLine("|---|---|");
        AddTableRow(text, "Проект", protocol.ProjectName);
        AddTableRow(text, "Система", protocol.SystemName);
        AddTableRow(text, "Версия", protocol.Version);
        AddTableRow(text, "Окружение", protocol.Environment);
        AddTableRow(text, "Площадка", protocol.Site);
        AddTableRow(text, "Исполнитель испытаний", protocol.Tester);
        AddTableRow(text, "Представитель заказчика", protocol.CustomerRepresentative);
        AddTableRow(text, "Дата испытаний", protocol.TestDate.ToString("dd.MM.yyyy"));
        text.AppendLine();

        text.AppendLine("## 2. Сводка результатов");
        text.AppendLine();
        text.AppendLine("| Всего | Пройдено | Не пройдено | Заблокировано | Не запускалось |");
        text.AppendLine("|---:|---:|---:|---:|---:|");
        text.AppendLine($"| {total} | {passed} | {failed} | {blocked} | {notRun} |");
        text.AppendLine();
        text.AppendLine($"**Доля успешных тестов:** {(total == 0 ? 0 : passed * 100.0 / total):F1}%.");
        text.AppendLine();

        text.AppendLine("## 3. Результаты испытаний");
        text.AppendLine();
        foreach (SatTestCase test in protocol.TestCases)
        {
            text.AppendLine($"### {EscapeHeading(test.Id)} — {EscapeHeading(test.Title)}");
            text.AppendLine();
            text.AppendLine($"**Статус:** {GetResultLabel(test.Result)}");
            text.AppendLine();
            text.AppendLine("**Предварительные условия:** " + EscapeInline(test.Preconditions));
            text.AppendLine();
            text.AppendLine("**Шаги выполнения:**");
            text.AppendLine();
            if (test.Steps.Count == 0)
                text.AppendLine("Шаги не указаны.");
            else
            {
                for (int i = 0; i < test.Steps.Count; i++)
                    text.AppendLine($"{i + 1}. {EscapeInline(test.Steps[i])}");
            }
            text.AppendLine();
            text.AppendLine("**Ожидаемый результат:** " + EscapeInline(test.ExpectedResult));
            text.AppendLine();
            text.AppendLine("**Фактический результат:** " + EscapeInline(test.ActualResult));
            text.AppendLine();
            if (!string.IsNullOrWhiteSpace(test.Comments))
            {
                text.AppendLine("**Примечание:** " + EscapeInline(test.Comments));
                text.AppendLine();
            }
            text.AppendLine("---");
            text.AppendLine();
        }

        text.AppendLine("## 4. Итоговое заключение");
        text.AppendLine();
        if (failed == 0 && blocked == 0 && notRun == 0 && total > 0)
            text.AppendLine("Все запланированные испытания успешно пройдены. Система может быть рекомендована к приёмке при согласовании сторонами.");
        else if (failed > 0)
            text.AppendLine("Обнаружены тесты со статусом «Не пройдено». Необходимо устранить замечания и провести повторные испытания.");
        else
            text.AppendLine("Испытания завершены не полностью. Для окончательного решения требуется выполнить заблокированные и незапущенные тесты.");

        text.AppendLine();
        text.AppendLine("## 5. Подписи сторон");
        text.AppendLine();
        text.AppendLine("| Роль | ФИО | Подпись | Дата |");
        text.AppendLine("|---|---|---|---|");
        text.AppendLine("| Исполнитель |  |  |  |");
        text.AppendLine("| Представитель заказчика |  |  |  |");
        text.AppendLine();
        text.AppendLine("> Документ сформирован программой SatBackupToolkit. Результаты должны быть проверены и утверждены ответственными лицами.");

        return text.ToString();
    }

    public string SaveMarkdown(SatProtocol protocol, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        string baseName = SanitizeFileName($"SAT_{protocol.ProjectName}_{protocol.TestDate:yyyyMMdd}");
        string path = Path.Combine(outputDirectory, baseName + ".md");
        int counter = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(outputDirectory, baseName + $"_{counter}.md");
            counter++;
        }

        File.WriteAllText(path, GenerateMarkdown(protocol), new UTF8Encoding(false));
        return path;
    }

    private static void AddTableRow(StringBuilder text, string name, string value)
    {
        text.AppendLine($"| {EscapeTable(name)} | {EscapeTable(value)} |");
    }

    private static string EscapeTable(string value) =>
        (value ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    private static string EscapeInline(string value) =>
        string.IsNullOrWhiteSpace(value) ? "Не указано" : value.Replace("\r\n", " / ").Replace("\n", " / ").Replace("\r", " / ");

    private static string EscapeHeading(string value) =>
        string.IsNullOrWhiteSpace(value) ? "Без названия" : value.Replace("#", "\\#").Replace("\r", " ").Replace("\n", " ");

    private static string GetResultLabel(TestResult result) => result switch
    {
        TestResult.Passed => "Пройдено",
        TestResult.Failed => "Не пройдено",
        TestResult.Blocked => "Заблокировано",
        _ => "Не запускалось"
    };

    private static string SanitizeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');
        return value.Trim().Replace(' ', '_');
    }
}
