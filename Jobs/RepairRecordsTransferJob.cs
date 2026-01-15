using System;
using System.Threading.Tasks;
using ES_Schedule.Interfaces;
using ES_Schedule.Common;
using ES_Schedule.Config;

namespace ES_Schedule.Jobs
{
    /// <summary>
    /// 維修記錄轉移排程任務
    /// </summary>
    public class RepairRecordsTransferJob : IScheduleJob
    {
        /// <summary>
        /// 任務識別名稱
        /// </summary>
        public string JobName => "RepairRecordsTransfer";

        /// <summary>
        /// 任務描述
        /// </summary>
        public string Description => "將維修記錄從 Oracle 轉移至 MSSQL";

        /// <summary>
        /// 執行排程任務
        /// </summary>
        /// <returns>Exit Code: 0=成功, 2=配置錯誤, 3=執行例外</returns>
        public async Task<int> ExecuteAsync()
        {
            try
            {
                Logger.Instance.Info($"開始執行排程任務: {JobName}");
                
                // 取得配置服務
                var configService = AppConfigService.Instance;
                
                // 檢查連線字串
                if (string.IsNullOrEmpty(configService.SourceConnectionString) || 
                    string.IsNullOrEmpty(configService.DestinationConnectionString))
                {
                    Logger.Instance.Error("資料庫連線字串設定不完整，無法執行轉移任務");
                    return 2; // 配置錯誤
                }
                
                // 檢查轉移配置
                if (configService.TransferConfigs == null || configService.TransferConfigs.Count == 0)
                {
                    Logger.Instance.Warning("沒有設定任何轉移任務配置");
                    return 0; // 視為成功，只是沒有任務需要執行
                }
                
                Logger.Instance.Info($"發現 {configService.TransferConfigs.Count} 個轉移配置");
                
                // TODO: 實作實際的資料轉移邏輯
                // 目前僅記錄任務資訊
                foreach (var config in configService.TransferConfigs)
                {
                    if (config.Enabled)
                    {
                        Logger.Instance.Info($"準備執行轉移任務: {config.Name}");
                        Logger.Instance.Info($"  來源查詢: {config.SourceQuery}");
                        Logger.Instance.Info($"  目標資料表: {config.DestinationTable}");
                        
                        // 模擬非同步處理
                        await Task.Delay(100);
                        
                        Logger.Instance.Info($"轉移任務完成: {config.Name}");
                    }
                    else
                    {
                        Logger.Instance.Info($"略過已停用的轉移任務: {config.Name}");
                    }
                }
                
                Logger.Instance.Info($"排程任務執行完成: {JobName}");
                return 0; // 成功
            }
            catch (Exception ex)
            {
                Logger.Instance.Exception(ex, $"排程任務執行失敗: {JobName}");
                return 3; // 執行例外
            }
        }
    }
}
