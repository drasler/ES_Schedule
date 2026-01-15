using System;
using System.IO;
using System.Configuration;
using Newtonsoft.Json;
using System.Collections.Generic;
using ES_Schedule.Common;

namespace ES_Schedule.Config
{
    /// <summary>
    /// 配置服務類別，負責載入與解析設定檔資訊
    /// </summary>
    public class AppConfigService
    {
        private static readonly Lazy<AppConfigService> _instance = new Lazy<AppConfigService>(() => new AppConfigService());
        private const string CONFIG_FILENAME = "TransferConfigs.json";
        private const string CONFIG_DIRECTORY = "Settings";

        /// <summary>
        /// 取得配置服務單例實例
        /// </summary>
        public static AppConfigService Instance => _instance.Value;

        /// <summary>
        /// 資料轉移配置列表
        /// </summary>
        public List<TransferConfig> TransferConfigs { get; private set; }

        /// <summary>
        /// 來源資料庫(Oracle)連線字串
        /// </summary>
        public string JHDBConnectionString { get; private set; }

        /// <summary>
        /// 目標資料庫(MSSQL)連線字串
        /// </summary>
        public string MiddleDatabaseConnectionString { get; private set; }

        /// <summary>
        /// 批次處理大小
        /// </summary>
        public int BatchSize { get; private set; }

        /// <summary>
        /// 日誌路徑
        /// </summary>
        public string LogPath { get; private set; }

        private AppConfigService()
        {
            try
            {
                LoadConnectionStrings();
                LoadAppSettings();
                LoadTransferConfigs();
                Logger.Instance.Info("配置服務初始化完成");
            }
            catch (Exception ex)
            {
                Logger.Instance.Fatal($"配置服務初始化失敗: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 載入資料庫連線字串
        /// </summary>
        private void LoadConnectionStrings()
        {
            try
            {
                JHDBConnectionString = ConfigurationManager.ConnectionStrings["JHDB"]?.ConnectionString;
                MiddleDatabaseConnectionString = ConfigurationManager.ConnectionStrings["MiddleDatabase"]?.ConnectionString;

                if (string.IsNullOrEmpty(JHDBConnectionString))
                {
                    Logger.Instance.Warning("找不到來源資料庫連線字串設定 (JHDB)");
                }
                
                if (string.IsNullOrEmpty(MiddleDatabaseConnectionString))
                {
                    Logger.Instance.Warning("找不到目標資料庫連線字串設定 (MiddleDatabase)");
                }

                Logger.Instance.Info("資料庫連線字串載入完成");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("載入資料庫連線字串失敗", ex);
                throw;
            }
        }

        /// <summary>
        /// 載入應用程式設定
        /// </summary>
        private void LoadAppSettings()
        {
            try
            {
                BatchSize = int.Parse(ConfigurationManager.AppSettings["BatchSize"] ?? "1000");
                LogPath = ConfigurationManager.AppSettings["LogPath"] ?? "Logs";

                // 確保日誌目錄存在
                string fullLogPath = LogPath;
                if (!Path.IsPathRooted(fullLogPath))
                {
                    fullLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fullLogPath);
                }
                
                if (!Directory.Exists(fullLogPath))
                {
                    Directory.CreateDirectory(fullLogPath);
                    Logger.Instance.Info($"建立日誌目錄: {fullLogPath}");
                }

                Logger.Instance.Info($"應用程式設定載入成功 - 批次大小: {BatchSize}, 日誌路徑: {LogPath}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("載入應用程式設定失敗", ex);
                throw;
            }
        }

        /// <summary>
        /// 載入資料轉移配置
        /// </summary>
        private void LoadTransferConfigs()
        {
            try
            {
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string configPath = Path.Combine(baseDirectory, CONFIG_DIRECTORY, CONFIG_FILENAME);

                // 確保 Settings 目錄存在
                string settingsDir = Path.Combine(baseDirectory, CONFIG_DIRECTORY);
                if (!Directory.Exists(settingsDir))
                {
                    Directory.CreateDirectory(settingsDir);
                    Logger.Instance.Info($"建立配置目錄: {settingsDir}");
                }

                // 載入配置文件
                LoadConfigFile(configPath);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("載入資料轉移配置失敗", ex);
                throw;
            }
        }

        /// <summary>
        /// 載入配置文件內容
        /// </summary>
        private void LoadConfigFile(string configPath)
        {
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    TransferConfigs = JsonConvert.DeserializeObject<List<TransferConfig>>(json);

                    if (TransferConfigs == null || TransferConfigs.Count == 0)
                    {
                        Logger.Instance.Warning($"配置文件 {configPath} 中沒有找到任何轉移任務");
                        TransferConfigs = new List<TransferConfig>();
                    }
                    else
                    {
                        Logger.Instance.Info($"成功載入 {TransferConfigs.Count} 個轉移任務配置");
                    }
                }
                catch (JsonException ex)
                {
                    Logger.Instance.Error($"解析配置文件失敗: {ex.Message}", ex);
                    TransferConfigs = new List<TransferConfig>();
                }
            }
            else
            {
                Logger.Instance.Warning($"配置文件 {configPath} 不存在");
                TransferConfigs = new List<TransferConfig>();
            }
        }
    }
}
