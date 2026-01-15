using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Configuration;

namespace ES_Schedule.Common
{
    /// <summary>
    /// 日誌級別枚舉
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// 除錯資訊
        /// </summary>
        Debug,
        
        /// <summary>
        /// 一般資訊
        /// </summary>
        Info,
        
        /// <summary>
        /// 警告資訊
        /// </summary>
        Warning,
        
        /// <summary>
        /// 錯誤資訊
        /// </summary>
        Error,
        
        /// <summary>
        /// 嚴重錯誤資訊
        /// </summary>
        Fatal
    }

    /// <summary>
    /// 日誌服務類別，用於記錄程式執行情況
    /// 採用 Singleton 模式，Thread-Safe，無 Console 輸出
    /// </summary>
    public class Logger
    {
        private static readonly Lazy<Logger> _instance = new Lazy<Logger>(() => new Logger());
        private static readonly object _lock = new object();
        private readonly string _logPath;

        /// <summary>
        /// 取得日誌服務單例實例
        /// </summary>
        public static Logger Instance => _instance.Value;

        private Logger()
        {
            // 從 App.config 讀取 LogPath 設定
            _logPath = ConfigurationManager.AppSettings["LogPath"] ?? "Logs";
            
            // 確保日誌目錄存在
            try
            {
                if (!Path.IsPathRooted(_logPath))
                {
                    _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _logPath);
                }
                
                if (!Directory.Exists(_logPath))
                {
                    Directory.CreateDirectory(_logPath);
                }
            }
            catch
            {
                // 如果無法建立目錄，使用程式目錄
                _logPath = AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        /// <summary>
        /// 記錄除錯資訊
        /// </summary>
        /// <param name="message">日誌訊息</param>
        public void Debug(string message) => Log(LogLevel.Debug, message);

        /// <summary>
        /// 記錄一般資訊
        /// </summary>
        /// <param name="message">日誌訊息</param>
        public void Info(string message) => Log(LogLevel.Info, message);

        /// <summary>
        /// 記錄警告資訊
        /// </summary>
        /// <param name="message">日誌訊息</param>
        public void Warning(string message) => Log(LogLevel.Warning, message);

        /// <summary>
        /// 記錄錯誤資訊
        /// </summary>
        /// <param name="message">日誌訊息</param>
        public void Error(string message) => Log(LogLevel.Error, message);

        /// <summary>
        /// 記錄錯誤資訊（包含例外）
        /// </summary>
        /// <param name="message">日誌訊息</param>
        /// <param name="ex">例外物件</param>
        public void Error(string message, Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(message);
            sb.AppendLine($"例外類型: {ex.GetType().Name}");
            sb.AppendLine($"例外訊息: {ex.Message}");
            
            if (ex.StackTrace != null)
            {
                sb.AppendLine($"堆疊追蹤: {ex.StackTrace}");
            }
            
            Log(LogLevel.Error, sb.ToString());
        }

        /// <summary>
        /// 記錄嚴重錯誤資訊
        /// </summary>
        /// <param name="message">日誌訊息</param>
        public void Fatal(string message) => Log(LogLevel.Fatal, message);

        /// <summary>
        /// 記錄嚴重錯誤資訊（包含例外）
        /// </summary>
        /// <param name="message">日誌訊息</param>
        /// <param name="ex">例外物件</param>
        public void Fatal(string message, Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(message);
            sb.AppendLine($"例外類型: {ex.GetType().Name}");
            sb.AppendLine($"例外訊息: {ex.Message}");
            
            if (ex.StackTrace != null)
            {
                sb.AppendLine($"堆疊追蹤: {ex.StackTrace}");
            }
            
            Log(LogLevel.Fatal, sb.ToString());
        }

        /// <summary>
        /// 記錄例外資訊
        /// </summary>
        /// <param name="ex">例外物件</param>
        /// <param name="message">附加訊息</param>
        public void Exception(Exception ex, string message = null)
        {
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(message))
            {
                sb.AppendLine(message);
            }
            
            sb.AppendLine($"例外類型: {ex.GetType().Name}");
            sb.AppendLine($"例外訊息: {ex.Message}");
            
            if (ex.StackTrace != null)
            {
                sb.AppendLine($"堆疊追蹤: {ex.StackTrace}");
            }
            
            // 記錄內部例外
            if (ex.InnerException != null)
            {
                sb.AppendLine("內部例外:");
                sb.AppendLine($"  類型: {ex.InnerException.GetType().Name}");
                sb.AppendLine($"  訊息: {ex.InnerException.Message}");
                
                if (ex.InnerException.StackTrace != null)
                {
                    sb.AppendLine($"  堆疊追蹤: {ex.InnerException.StackTrace}");
                }
            }
            
            Log(LogLevel.Error, sb.ToString());
        }

        /// <summary>
        /// 記錄日誌 (無 Console 輸出，僅寫入檔案)
        /// </summary>
        /// <param name="level">日誌級別</param>
        /// <param name="message">日誌訊息</param>
        private void Log(LogLevel level, string message)
        {
            try
            {
                DateTime now = DateTime.Now;
                string logFileName = $"{now:yyyy-MM-dd}.log";
                string logFilePath = Path.Combine(_logPath, logFileName);
                string threadId = Thread.CurrentThread.ManagedThreadId.ToString();
                string formattedMessage = $"[{now:yyyy-MM-dd HH:mm:ss.fff}] [{level,-7}] [Thread-{threadId}] {message}";
                
                // 注意：不使用 Console.WriteLine，僅寫入檔案
                
                lock (_lock)
                {
                    try
                    {
                        using (StreamWriter writer = new StreamWriter(logFilePath, true, Encoding.UTF8))
                        {
                            writer.WriteLine(formattedMessage);
                        }
                    }
                    catch
                    {
                        // 寫入日誌檔案失敗時，嘗試寫入 Windows 事件日誌或忽略
                        // 作為排程任務，不應輸出至 Console
                    }
                }
            }
            catch
            {
                // 記錄日誌時發生例外，靜默處理
            }
        }
    }
}
