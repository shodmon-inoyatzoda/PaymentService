# 1. Запуск тестов
dotnet test --collect:"XPlat Code Coverage"

# 2. Поиск файла отчета (т.к. dotnet test создает папку с рандомным GUID)
$reportFile = Get-ChildItem -Recurse -Filter "coverage.cobertura.xml" | Select-Object -First 1

if ($reportFile) {
    # 3. Генерация HTML-отчета
    reportgenerator -reports:$reportFile.FullName -targetdir:"coverage-report" -reporttypes:Html
    
    # 4. Открытие отчета в браузере
    Start-Process "./coverage-report/index.html"
} else {
    Write-Host "Файл отчета не найден. Проверьте, прошли ли тесты." -ForegroundColor Red
}