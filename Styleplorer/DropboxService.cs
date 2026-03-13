using Dropbox.Api;
using Dropbox.Api.Files;
using Dropbox.Api.Users;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;

public class DropboxService
{
    // Константы для приложения Dropbox
    private const string AppKey = "mm24dyq3kw2e538";
    private const string AppSecret = "ubn2kf4sopfauqr";
    private static string AccessToken = null;
    private static string RedirectUri = "http://localhost:8080/oauth2/callback";

    // Клиент Dropbox для выполнения API вызовов
    public DropboxClient _client;

    // Асинхронный метод для аутентификации пользователя через OAuth2
    public async Task<bool> AuthenticateAsync()
    {
        try
        {
            // Генерация состояния OAuth2
            var oauth2State = Guid.NewGuid().ToString("N");

            // Создание URL для авторизации
            var authorizeUri = $"https://www.dropbox.com/oauth2/authorize?response_type=code&client_id={AppKey}" +
                $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                $"&state={oauth2State}&token_access_type=offline";

            // Открываем браузер по умолчанию для авторизации пользователя
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = authorizeUri.ToString(),
                UseShellExecute = true
            });

            // Создаем и запускаем локальный HTTP-сервер для обработки редиректа
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(RedirectUri + "/");
                listener.Start();

                // Ожидаем ответ от Dropbox
                var context = await listener.GetContextAsync();

                // Получаем код авторизации из URL
                var code = HttpUtility.ParseQueryString(context.Request.Url.Query).Get("code");
                var state = HttpUtility.ParseQueryString(context.Request.Url.Query).Get("state");

                // Проверка состояния OAuth
                if (state != oauth2State)
                {
                    throw new Exception("Недопустимое состояние OAuth");
                }

                // Обмениваем код на токен доступа
                var response = await DropboxOAuth2Helper.ProcessCodeFlowAsync(code, AppKey, AppSecret, RedirectUri);

                // Сохранение токена доступа
                AccessToken = response.AccessToken;

                // Инициализация клиента Dropbox с полученным токеном
                _client = new DropboxClient(AccessToken);

                // Отправляем ответ в браузер
                var buffer = System.Text.Encoding.UTF8.GetBytes("Authorization is successful. You can close this window.");// "Авторизация успешна. Вы можете закрыть это окно.");
                context.Response.ContentLength64 = buffer.Length;
                context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                context.Response.Close();

                return true;
            }
        }
        catch (Exception ex)
        {
            //MessageBox.Show($"Ошибка при авторизации: {ex.Message}");
            return false;
        }
    }

    // Асинхронный метод для получения списка файлов в заданной папке
    public async Task<List<Metadata>> ListFilesAsync(string path = "")
    {
        if (_client == null)
        {
            throw new InvalidOperationException("Клиент Dropbox не инициализирован. Выполните авторизацию.");
        }

        // Получение списка файлов из Dropbox
        var result = await _client.Files.ListFolderAsync(string.IsNullOrEmpty(path) ? "" : path);
        return result.Entries.ToList(); // Возврат списка файлов
    }

    // Асинхронный метод для получения содержимого файла по его идентификатору
    public async Task<string> GetFileContentAsync(string fileId)
    {
        // Скачивание файла из Dropbox и получение его содержимого в виде строки
        using (var response = await _client.Files.DownloadAsync(fileId))
        {
            return await response.GetContentAsStringAsync();
        }
    }

    // Асинхронный метод для получения данных файла в виде массива байтов
    public async Task<byte[]> GetFileDataAsync(string fileId)
    {
        // Скачивание файла из Dropbox и получение его данных в виде массива байтов
        using (var response = await _client.Files.DownloadAsync(fileId))
        {
            return await response.GetContentAsByteArrayAsync();
        }
    }

    // Асинхронный метод для получения информации об использовании пространства в Dropbox
    public async Task<SpaceUsage> GetSpaceUsageAsync()
    {
        if (_client == null)
        {
            throw new InvalidOperationException("Клиент Dropbox не инициализирован. Выполните авторизацию.");
        }

        // Получение информации об использовании пространства
        return await _client.Users.GetSpaceUsageAsync();
    }

    // Асинхронный метод для скачивания файла и сохранения его во временном файле
    public async Task<string> DownloadFileAsync(string fileId)
    {
        // Создание временного файла
        string tempFilePath = Path.GetTempFileName();

        // Скачивание файла и сохранение его во временном файле
        using (var response = await _client.Files.DownloadAsync(fileId))
        using (var fileStream = File.Create(tempFilePath))
        {
            (await response.GetContentAsStreamAsync()).CopyTo(fileStream);
        }

        return tempFilePath; // Возврат пути к временному файлу
    }
}
