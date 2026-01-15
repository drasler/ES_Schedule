using System;
using System.Threading.Tasks;
using ES_Schedule.Common;
using ES_Schedule.Services;

namespace ES_Schedule
{
    /// <summary>
    /// 程式進入點類別
    /// ES_Schedule - 可擴展式多排程任務架構
    /// </summary>
    class Program
    {
        /// <summary>
        /// Exit Code 定義
        /// </summary>
        private static class ExitCodes
        {
            public const int Success = 0;           // 成功執行
            public const int ParameterError = 1;    // 參數錯誤或找不到任務
            public const int ConfigError = 2;       // 配置錯誤
            public const int ExecutionError = 3;    // 執行過程發生例外
        }

        /// <summary>
        /// 主程式進入點
        /// </summary>
        /// <param name="args">命令行參數 - 第一個參數為任務名稱</param>
        /// <returns>Exit Code: 0=成功, 1=參數錯誤, 2=配置錯誤, 3=執行例外</returns>
        static async Task<int> Main(string[] args)
        {
            try
            {
                // 記錄程式啟動
                Logger.Instance.Info("===========================================");
                Logger.Instance.Info($"程式啟動時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Logger.Instance.Info($"工作目錄: {Environment.CurrentDirectory}");
                Logger.Instance.Info($"應用程式目錄: {AppDomain.CurrentDomain.BaseDirectory}");
                
                // 取得命令行參數中的任務名稱
                string jobName = args.Length > 0 ? args[0] : null;
                
                // 如果沒有指定任務名稱
                if (string.IsNullOrEmpty(jobName))
                {
                    Logger.Instance.Info("未指定任務名稱，顯示可用任務清單:");
                    foreach (var availableJobName in JobFactory.GetAvailableJobs())
                    {
                        Logger.Instance.Info($"  - {availableJobName}");
                    }
                    Logger.Instance.Info("程式結束 (未執行任何任務)");
                    return ExitCodes.ParameterError;
                }
                
                Logger.Instance.Info($"指定任務名稱: {jobName}");
                
                // 根據任務名稱建立對應的 Job
                var job = JobFactory.CreateJob(jobName);
                if (job == null)
                {
                    Logger.Instance.Error($"找不到排程任務: {jobName}");
                    Logger.Instance.Info("可用的任務清單:");
                    foreach (var availableJob in JobFactory.GetAvailableJobs())
                    {
                        Logger.Instance.Info($"  - {availableJob}");
                    }
                    return ExitCodes.ParameterError;
                }
                
                Logger.Instance.Info($"任務描述: {job.Description}");
                
                // 執行任務
                int result = await job.ExecuteAsync();
                
                Logger.Instance.Info($"任務執行完成，Exit Code: {result}");
                Logger.Instance.Info("===========================================");
                
                return result;
            }
            catch (Exception ex)
            {
                // 記錄未處理的例外
                Logger.Instance.Exception(ex, "程式執行過程中發生未處理的例外");
                return ExitCodes.ExecutionError;
            }
        }
    }
}
